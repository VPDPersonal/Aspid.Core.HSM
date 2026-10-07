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

        /// <summary>
        /// Asynchronously transitions to <typeparamref name="TState"/>. Cancels any in-progress
        /// async transition and waits for it to unwind before mutating the state chain, so two
        /// overlapping calls never corrupt <c>_currentStates</c>. States implementing
        /// <see cref="IAsyncEnterController"/> or <see cref="IAsyncExitController"/> are awaited;
        /// others fall back to synchronous controllers.
        /// </summary>
        /// <typeparam name="TState">The target leaf state type.</typeparam>
        /// <param name="cancellationToken">Cancellation token for the transition.</param>
        public async UniTask ChangeStateAsync<TState>(CancellationToken cancellationToken = default)
            where TState : IState
        {
            if (!IsStateEnabled(typeof(TState)))
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

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _activeTransitionCts = linked;
            var task = ChangeStateCoreAsync<TState>(linked.Token).Preserve();
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

        private async UniTask ChangeStateCoreAsync<TState>(CancellationToken token)
            where TState : IState
        {
            OnChangingState();
            {
                var newChain = _stateFactory.CreateState<TState>(_currentStates);
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
