using System.Threading;
using Cysharp.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public partial class StateMachineBase
    {
        public async UniTask ChangeStateAsync<TState>(CancellationToken cancellationToken = default)
            where TState : IState
        {
            var previous = _activeTransitionCts;
            if (previous is not null)
                previous.Cancel();

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _activeTransitionCts = linked;
            try
            {
                OnChangingState();
                {
                    var newChain = _stateFactory.CreateState<TState>(_currentStates);
                    var divergeIndex = FindDivergeIndex(newChain);

                    for (var i = _currentStates.Count - 1; i >= divergeIndex; i--)
                    {
                        await ExitStateAsync(_currentStates[i], linked.Token);
                        _currentStates.RemoveAt(i);
                    }

                    for (var i = divergeIndex; i < newChain.Count; i++)
                    {
                        linked.Token.ThrowIfCancellationRequested();
                        var state = newChain[i];
                        _currentStates.Add(state);
                        await EnterStateAsync(state, linked.Token);
                    }
                }
                OnChangedState();
            }
            finally
            {
                if (ReferenceEquals(_activeTransitionCts, linked))
                    _activeTransitionCts = null;
            }
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
