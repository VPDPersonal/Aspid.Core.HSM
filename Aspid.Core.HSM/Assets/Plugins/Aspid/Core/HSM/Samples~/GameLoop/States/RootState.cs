using Aspid.Core.HSM;
using UnityEngine;

namespace Aspid.Core.HSM.Samples.GameLoop.States
{
    public class RootState : IState
    {
        public void Enter()
        {
            Debug.Log("[HSM] RootState.Enter");
        }

        public void Exit()
        {
            Debug.Log("[HSM] RootState.Exit");
        }
    }
}
