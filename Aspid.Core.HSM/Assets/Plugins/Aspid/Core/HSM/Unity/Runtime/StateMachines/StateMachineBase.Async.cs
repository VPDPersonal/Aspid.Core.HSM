using System;
using System.Threading;
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

        // True only while the async core is running its exit/enter callbacks. A transition started from
        // inside one of those callbacks would await the very task it is running on, so it is rejected
        // with a diagnosable exception instead of deadlocking.
        private bool _isChangingStateAsync;

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
            if (_isChangingStateAsync)
                throw new InvalidOperationException(
                    "ChangeStateAsync was called from inside the async enter/exit callbacks of the transition " +
                    "that is currently running. Awaiting it would deadlock, because the running transition " +
                    "cannot unwind until this call returns. Start the follow-up transition after the current " +
                    "one completes, or use the synchronous ChangeState, which queues re-entrant requests.");

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
                var newChain = RentChainBuffer();
                _isChangingStateAsync = true;
                try
                {
                    _stateFactory.CreateState(stateType, _currentStates, newChain);
                    var divergeIndex = FindDivergeIndex(newChain);

                    for (var i = _currentStates.Count - 1; i >= divergeIndex; i--)
                    {
                        await ExitStateAsync(_currentStates[i], token);
                        _currentStates.RemoveAt(i);
                    }

                    for (var i = divergeIndex; i < newChain.Count; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        var state = newChain[i];
                        _currentStates.Add(state);
                        await EnterStateAsync(state, token);
                    }
                }
                finally
                {
                    _isChangingStateAsync = false;
                    ReturnChainBuffer(newChain);
                }
            }
            OnChangedState();
            AutoDetachIncompatibleExtensions();
        }

        private async UniTask ExitStateAsync(IState state, CancellationToken cancellationToken)
        {
            OnExitingState(state);
            {
                if (state is IAsyncExitController asyncExit)
                    await asyncExit.OnExitAsync(cancellationToken);
                else
                    state.GetController<IExitController>()?.OnExit();
                state.Exit();
            }
            OnExitedState(state);

            _stateFactory.Release(state);
        }

        private async UniTask EnterStateAsync(IState state, CancellationToken cancellationToken)
        {
            OnEnteringState(state);
            {
                _stateFactory.MarkInitialized(state);
                state.Enter();
                if (state is IAsyncEnterController asyncEnter)
                    await asyncEnter.OnEnterAsync(cancellationToken);
                else
                    state.GetController<IEnterController>()?.OnEnter();
            }
            OnEnteredState(state);
        }
    }
}
