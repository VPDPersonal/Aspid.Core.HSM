using System;
using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public partial class StateMachineBase
    {
        // Completes once the in-flight core transition has unwound. It is assigned together with
        // _activeTransitionCts and before the core starts, so a call made from inside the core's first
        // synchronous segment also waits for the unwind. default(UniTask) is an already-completed task.
        private UniTask _activeTransitionUnwound;

        /// <summary>
        /// Asynchronously transitions to <typeparamref name="TState"/>. Cancels any in-progress
        /// async transition and waits for it to unwind before mutating the state chain, so two
        /// overlapping calls never corrupt <c>_currentStates</c>. States implementing
        /// <see cref="IAsyncEnterController"/> or <see cref="IAsyncExitController"/> are awaited;
        /// others fall back to synchronous controllers.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A cancelled transition stops waiting for its async enter/exit callback at once, even if the callback
        /// ignores the token. The callback keeps running on its own, so it must observe the token to stop early.
        /// </para>
        /// <para>
        /// For the same reason, an async enter callback can redirect the machine by awaiting this method:
        /// the new call supersedes the transition that runs the callback.
        /// </para>
        /// <para>
        /// A cancelled or failed transition rolls back: an exit that has started completes and releases its state,
        /// and every state below the point where the old and new chains diverge is exited and released. The chain
        /// ends at the ancestors both chains share, or at <see cref="EmptyState"/>.
        /// </para>
        /// </remarks>
        /// <typeparam name="TState">The target leaf state type.</typeparam>
        /// <param name="cancellationToken">Cancellation token for the transition.</param>
        /// <exception cref="InvalidOperationException">
        /// A synchronous state change is in progress, for example when called from a synchronous enter or exit callback.
        /// </exception>
        public UniTask ChangeStateAsync<TState>(CancellationToken cancellationToken = default)
            where TState : IState =>
            ChangeStateAsync(typeof(TState), cancellationToken);

        /// <inheritdoc cref="ChangeStateAsync{TState}"/>
        /// <param name="stateType">The target leaf state type.</param>
        /// <param name="cancellationToken">Cancellation token for the transition.</param>
        public async UniTask ChangeStateAsync(Type stateType, CancellationToken cancellationToken = default)
        {
            ThrowIfSyncChangeInProgress();

            if (!IsStateEnabled(stateType))
                return;

            await SupersedeActiveTransitionAsync();

            // Read the leaf only after the superseded transition has unwound: the edge being guarded
            // is the one actually taken, not the one that was current when this call was made.
            if (!IsTransitionEnabled(_currentStates[^1].GetType(), stateType))
                return;

            await RunTransitionCoreAsync(stateType, cancellationToken);
        }

        private async UniTask SupersedeActiveTransitionAsync()
        {
            // A loop, not a single check: several callers can wait for the same unwind. The first one to
            // resume starts a new transition, which the next one must supersede in turn.
            while (_activeTransitionCts is { } previous)
            {
                // Read before Cancel: the core can unwind inside Cancel and reset the field.
                var unwound = _activeTransitionUnwound;
                previous.Cancel();
                await unwound;
            }
        }

        // Runs the core with no guard checks: every caller has checked the guards already.
        private async UniTask RunTransitionCoreAsync(Type stateType, CancellationToken cancellationToken)
        {
            // Guards and transition hooks run between the caller's supersede and this point.
            // If one of them started a transition, supersede it too: two cores must never run at once.
            await SupersedeActiveTransitionAsync();

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var unwound = new UniTaskCompletionSource();
            _activeTransitionCts = linked;
            _activeTransitionUnwound = unwound.Task;
            try
            {
                await ChangeStateCoreAsync(stateType, linked.Token);
            }
            finally
            {
                if (ReferenceEquals(_activeTransitionCts, linked))
                {
                    _activeTransitionCts = null;
                    _activeTransitionUnwound = default;
                }

                // Signal last: a waiting superseder resumes inside this call and must find the machine idle.
                unwound.TrySetResult();
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
                catch (Exception exception)
                {
                    // A chain that failed to build left the active chain as it was: nothing to roll back,
                    // but the change is still closed.
                    await RollBackAsync(divergeIndex >= 0 ? divergeIndex : _currentStates.Count, exception);
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

        // Undoes a transition that was cancelled or threw: every state from divergeIndex down — old states not
        // yet exited and new states entered so far, including the one whose enter was interrupted — is exited
        // from the tail and released. What remains are the ancestors the old and new chains share, all fully
        // entered. Rollback exits are not cancellable, so no state is left half-exited. A state whose rollback
        // exit throws is still removed and released. The change is then closed the same way as a failed sync one.
        private async UniTask RollBackAsync(int divergeIndex, Exception cause)
        {
            List<Exception>? failures = null;

            for (var i = _currentStates.Count - 1; i >= divergeIndex; i--)
            {
                try { await ExitStateAsync(_currentStates[i], CancellationToken.None); }
                catch (Exception exception) { (failures ??= new List<Exception>()).Add(exception); }
                finally { _currentStates.RemoveAt(i); }
            }

            CloseFailedChange(cause, failures);
        }

        // An exit, once started, always completes: IState.Exit, OnExitedState and Release run even when the bound
        // extensions' detach, OnExitingState or the exit controllers are cancelled or throw. The callbacks that
        // did not get to run are not called again.
        private async UniTask ExitStateAsync(IState state, CancellationToken cancellationToken)
        {
            var isExited = false;
            try
            {
                DetachExtensionsBoundTo(state);
                OnExitingState(state);

                if (state is IAsyncExitController asyncExit)
                {
                    // Stop waiting on cancellation even if the callback ignores the token. A callback that awaits
                    // the superseding transition would otherwise deadlock: that transition waits for this unwind.
                    await asyncExit.OnExitAsync(cancellationToken).AttachExternalCancellation(cancellationToken);
                }
                else
                {
                    // No await here, so one sample, the same as the synchronous ExitState.
#if ENABLE_PROFILER
                    using (GetMarkers(state).Exit.Auto())
#endif
                    {
                        try
                        {
                            state.GetController<IExitController>()?.OnExit();
                        }
                        finally
                        {
                            isExited = true;
                            state.Exit();
                        }
                    }
                }
            }
            finally
            {
                try
                {
                    if (!isExited)
                    {
                        // Only synchronous segments are measured: a profiler sample cannot span an await.
#if ENABLE_PROFILER
                        using (GetMarkers(state).Exit.Auto())
#endif
                        state.Exit();
                    }
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
                if (state is IAsyncEnterController asyncEnter)
                {
                    // Only synchronous segments are measured: a profiler sample cannot span an await.
#if ENABLE_PROFILER
                    using (GetMarkers(state).Enter.Auto())
#endif
                    {
                        _stateFactory.MarkInitialized(state);
                        state.Enter();
                    }

                    // Same as in ExitStateAsync: a redirecting callback awaits the transition that supersedes this one.
                    await asyncEnter.OnEnterAsync(cancellationToken).AttachExternalCancellation(cancellationToken);
                }
                else
                {
                    // No await here, so one sample, the same as the synchronous EnterState.
#if ENABLE_PROFILER
                    using (GetMarkers(state).Enter.Auto())
#endif
                    {
                        _stateFactory.MarkInitialized(state);
                        state.Enter();
                        state.GetController<IEnterController>()?.OnEnter();
                    }
                }
            }
            OnEnteredState(state);
        }
    }
}
