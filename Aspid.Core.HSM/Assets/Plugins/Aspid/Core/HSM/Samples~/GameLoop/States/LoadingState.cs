using System.Threading;
using Aspid.Core.HSM;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Aspid.Core.HSM.Samples.GameLoop.States
{
    // Entered with ChangeStateAsync: the transition awaits the loading. OnEntered then moves on to gameplay,
    // as a new transition, once the one that entered this state has completed.
    [ScopeLifetime(ScopeLifetime.Transient)]
    public class LoadingState : IState, IChildState<RootState>, IAsyncEnterController, IEnteredController, IExitController
    {
        private readonly IStateMachine _stateMachine;

        public LoadingState(IStateMachine stateMachine)
        {
            _stateMachine = stateMachine;
        }

        public void Enter()
        {
            Debug.Log("[HSM] LoadingState.Enter — loading screen shown");
        }

        public async UniTask OnEnterAsync(CancellationToken cancellationToken)
        {
            Debug.Log("[HSM] LoadingState: loading assets...");
            await UniTask.Delay(1500, cancellationToken: cancellationToken);
            Debug.Log("[HSM] LoadingState: assets loaded");
        }

        public void OnEntered(CancellationToken stateLifetime)
        {
            Debug.Log("[HSM] LoadingState: transition complete, starting the game");
            _stateMachine.TransitionTo<SinglePlayerState>();
        }

        public void OnExit()
        {
            Debug.Log("[HSM] LoadingState.OnExit — loading screen hidden");
        }

        public void Exit()
        {
            Debug.Log("[HSM] LoadingState.Exit");
        }
    }
}
