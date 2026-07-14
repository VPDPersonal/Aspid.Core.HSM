using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    // The root is part of every active chain, so its IDisposableController is guaranteed
    // to run when the machine is disposed (SampleGameManager.OnDestroy).
    public class RootState : IState, IDisposableController
    {
        public void Enter()
        {
            Debug.Log("[HSM] RootState.Enter");
        }

        public void Exit()
        {
            Debug.Log("[HSM] RootState.Exit");
        }

        public void Dispose()
        {
            Debug.Log("[HSM] RootState.Dispose — core services shut down");
        }
    }
}
