using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    [ControllerGroup]
    public partial class PauseState : IState, IChildState<GameplayState>
    {
        public PauseState(IGameActions actions)
        {
            AddControllers(
                new TimeScaleController(),
                new PauseScreen(actions));
        }

        public void Enter()
        {
            Debug.Log("[HSM] PauseState.Enter — game paused");
        }

        public void Exit()
        {
            Debug.Log("[HSM] PauseState.Exit — game resumed");
        }
    }
}
