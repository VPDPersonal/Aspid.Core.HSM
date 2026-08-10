using Aspid.Core.HSM;
using UnityEngine;

namespace Aspid.Core.HSM.Samples.GameLoop.States
{
    public class PauseState : IState, IChildState<GameplayState>, IEnterController, IExitController
    {
        public void Enter()
        {
            Debug.Log("[HSM] PauseState.Enter — game paused");
        }

        public void OnEnter()
        {
            Time.timeScale = 0f;
            Debug.Log("[HSM] PauseState: timeScale set to 0");
        }

        public void OnExit()
        {
            Time.timeScale = 1f;
            Debug.Log("[HSM] PauseState: timeScale restored to 1");
        }

        public void Exit()
        {
            Debug.Log("[HSM] PauseState.Exit — game resumed");
        }
    }
}
