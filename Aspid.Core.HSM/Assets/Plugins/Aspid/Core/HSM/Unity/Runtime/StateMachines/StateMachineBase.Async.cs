using System;
using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public partial class StateMachineBase
    {
        // The in-flight core transition, kept so a superseding call can await its unwind before
        // mutating _currentStates. Preserved so it can be awaited by both the original caller and
        // the superseder. default(UniTask) is an already-completed task, so no null check is needed.
        private UniTask _activeTransitionTask;

        // True only while an enter/exit callback of the async core is being invoked — not while the core awaits
        // the task a callback returned. A transition started from inside a callback would await the very task it
        // is running on, so it is rejected with a diagnosable exception instead of deadlocking. A transition
        // started from anywhere else while the core is awaiting supersedes it: it cancels it and waits for its
        // rollback. (Raising the flag for the whole transition rejected those superseding calls too.)
        private bool _isInTransitionCallback;

        /// <summary>
        /// Asynchronously transitions to <typeparamref name="TState"/>. Cancels any in-progress
        /// async transition and waits for it to unwind before mutating the state chain, so two
        /// overlapping calls never corrupt <c>_currentStates</c>. States implementing
        /// <see cref="IAsyncEnterController"/> or <see cref="IAsyncExitController"/> are awaited;
        /// others fall back to synchronous controllers.
        /// </summary>
        /// <typeparam name="TState">The target leaf state type.</typeparam>
        /// <param name="cancellationToken">Cancellation token for the transition.</param>
        public UniTask ChangeStateAsync<TState>(CancellationToken cancellationToken = default)
            where TState : IState =>
            ChangeStateAsync(typeof(TState), cancellationToken);

        /// <inheritdoc cref="ChangeStateAsync{TState}"/>
        /// <param name="stateType">The target leaf state type.</param>
        /// <param name="cancellationToken">Cancellation token for the transition.</param>
        /// <exception cref="InvalidOperationException">
        /// Called from inside an async enter/exit callback of the transition already running.
        /// </exception>
        public async UniTask ChangeStateAsync(Type stateType, CancellationToken cancellationToken = default)
        {
            if (_isInTransitionCallback)
                throw new InvalidOperationException(
                    "ChangeStateAsync was called from inside an enter/exit callback of the transition that is " +
                    "currently running. Awaiting it would deadlock, because the running transition cannot unwind " +
                    "until this call returns. Start the follow-up transition after the current one completes.");

            if (!IsStateEnabled(stateType))
                return;

            var previous = _activeTransitionCts;
            if (previous is not null)
            {
                previous.Cancel();
                // Wait for the superseded transition to fully unwind before we touch _currentStates.
                // Its outcome (including faults) is surfaced to its own caller, so swallow it here.
                try { await _activeTransitionTask; }
                catch { /* superseded transition's result belongs to its original caller */ }
            }

            // Re-read the leaf only after the superseded transition has unwound: the edge being guarded
            // is the one actually taken, not the one that was current when this call was made.
            if (!IsTransitionEnabled(_currentStates[^1].GetType(), stateType))
                return;

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _activeTransitionCts = linked;
            var task = ChangeStateCoreAsync(stateType, linked.Token).Preserve();
            _activeTransitionTask = task;
            try
            {
                await task;
            }
            finally
            {
                if (ReferenceEquals(_activeTransitionCts, linked))
                {
                    _activeTransitionCts = null;
                    _activeTransitionTask = default;
                }
            }
        }

        private async UniTask ChangeStateCoreAsync(Type stateType, CancellationToken token)
        {
            OnChangingState();
            {
                // Rented per in-flight transition: the chain is read across awaits, so a buffer shared
                // with the factory or with another transition would be rewritten underneath this loop.
                var newChain = RentTypeChainBuffer();
                var divergeIndex = -1;
                try
                {
                    _stateFactory.BuildTypeChain(stateType, newChain);
                    divergeIndex = FindDivergeIndex(newChain);

                    for (var i = _currentStates.Count - 1; i >= divergeIndex; i--)
                    {
                        // An exit that has started always completes (see ExitStateAsync), so the state
                        // leaves the chain even when its exit is cancelled or throws.
                        try { await ExitStateAsync(_currentStates[i], token); }
                        finally { _currentStates.RemoveAt(i); }
                    }

                    for (var i = divergeIndex; i < newChain.Count; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        var state = _stateFactory.CreateState(newChain[i]);
                        _currentStates.Add(state);
                        await EnterStateAsync(state, token);
                    }
                }
                catch (Exception exception) when (divergeIndex >= 0)
                {
                    await RollBackAsync(divergeIndex, exception);
                    throw;
                }
                finally
                {
                    ReturnTypeChainBuffer(newChain);
                }
            }
            OnChangedState();
            AutoDetachIncompatibleExtensions();
        }

        /// <summary>
        /// Undoes a transition that was cancelled or threw: every state from <paramref name="divergeIndex"/>
        /// down — old states not yet exited and new states entered so far, including the one whose enter was
        /// interrupted — is exited from the tail and released. What remains are the ancestors the old and new
        /// chains share, all fully entered; a chain left empty falls back to <see cref="EmptyState"/>.
        /// </summary>
        /// <remarks>
        /// Rollback exits are not cancellable: they run to completion so no state is left half-exited. A state
        /// whose rollback exit throws is still removed and released, and the failures are reported together
        /// with <paramref name="cause"/> in an <see cref="AggregateException"/>.
        /// </remarks>
        private async UniTask RollBackAsync(int divergeIndex, Exception cause)
        {
            List<Exception>? failures = null;

            for (var i = _currentStates.Count - 1; i >= divergeIndex; i--)
            {
                try { await ExitStateAsync(_currentStates[i], CancellationToken.None); }
                catch (Exception exception) { (failures ??= new List<Exception>()).Add(exception); }
                finally { _currentStates.RemoveAt(i); }
            }

            if (_currentStates.Count == 0)
                _currentStates.Add(new EmptyState());

            AutoDetachIncompatibleExtensions();

            if (failures is not null)
            {
                failures.Insert(0, cause);
                throw new AggregateException(
                    "A state change failed, and exiting its states during the rollback failed as well.", failures);
            }
        }

        // An exit, once started, always completes: IState.Exit, OnExitedState and Release run even when the exit
        // controllers are cancelled or throw. The controllers that did not get to run are not called again.
        private async UniTask ExitStateAsync(IState state, CancellationToken cancellationToken)
        {
            DetachExtensionsBoundTo(state);

            OnExitingState(state);
            try
            {
                var pending = default(UniTask);
                using (EnterTransitionCallback())
                {
                    if (state is IAsyncExitController asyncExit)
                        pending = asyncExit.OnExitAsync(cancellationToken);
                    else
                    {
#if ENABLE_PROFILER
                        using (GetMarkers(state).Exit.Auto())
#endif
                        state.GetController<IExitController>()?.OnExit();
                    }
                }

                await pending;
            }
            finally
            {
                try
                {
                    // Only synchronous segments are measured: a profiler sample cannot span an await.
                    using (EnterTransitionCallback())
#if ENABLE_PROFILER
                    using (GetMarkers(state).Exit.Auto())
#endif
                    state.Exit();
                }
                finally
                {
                    try { OnExitedState(state); }
                    finally { _stateFactory.Release(state); }
                }
            }
        }

        private async UniTask EnterStateAsync(IState state, CancellationToken cancellationToken)
        {
            OnEnteringState(state);
            {
                var pending = default(UniTask);
                using (EnterTransitionCallback())
                {
                    // Only synchronous segments are measured: a profiler sample cannot span an await.
#if ENABLE_PROFILER
                    using (GetMarkers(state).Enter.Auto())
#endif
                    {
                        _stateFactory.MarkInitialized(state);
                        state.Enter();
                    }

                    if (state is IAsyncEnterController asyncEnter)
                        pending = asyncEnter.OnEnterAsync(cancellationToken);
                    else
                    {
#if ENABLE_PROFILER
                        using (GetMarkers(state).Enter.Auto())
#endif
                        state.GetController<IEnterController>()?.OnEnter();
                    }
                }

                await pending;
            }
            OnEnteredState(state);
        }

        private TransitionCallbackScope EnterTransitionCallback()
        {
            var scope = new TransitionCallbackScope(this, _isInTransitionCallback);
            _isInTransitionCallback = true;
            return scope;
        }

        private readonly struct TransitionCallbackScope : IDisposable
        {
            private readonly StateMachineBase _machine;
            private readonly bool _wasInCallback;

            public TransitionCallbackScope(StateMachineBase machine, bool wasInCallback)
            {
                _machine = machine;
                _wasInCallback = wasInCallback;
            }

            public void Dispose() => _machine._isInTransitionCallback = _wasInCallback;
        }
    }
}
