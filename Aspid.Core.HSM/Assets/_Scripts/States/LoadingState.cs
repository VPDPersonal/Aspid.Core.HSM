using System.Threading;
using Aspid.Core.HSM;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _Scripts.States
{
    [ScopeLifetime(ScopeLifetime.Transient)]
    public class LoadingState : IState, IChildState<RootState>, IAsyncEnterController, IExitController
    {
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
