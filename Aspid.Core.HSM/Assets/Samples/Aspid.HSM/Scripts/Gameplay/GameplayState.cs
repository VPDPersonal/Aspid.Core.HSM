using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    [ControllerGroup]
    [ScopeLifetime(ScopeLifetime.Cached)]
    public partial class GameplayState : IState, IChildState<RootState>
    {
        public GameplayState()
        {
            AddControllers(
                new PlayerInputController(),
                new GameplayAmbienceController());
        }

        public void Enter()
        {
            Debug.Log("[HSM] GameplayState.Enter — gameplay started");
        }

        public void Exit()
        {
            Debug.Log("[HSM] GameplayState.Exit — gameplay stopped");
        }
    }
}
