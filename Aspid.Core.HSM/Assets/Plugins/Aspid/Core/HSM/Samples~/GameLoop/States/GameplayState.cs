using Aspid.Core.HSM;
using Aspid.Core.HSM.Samples.GameLoop.Controllers;
using UnityEngine;

namespace Aspid.Core.HSM.Samples.GameLoop.States
{
    [ControllerGroup]
    [ScopeLifetime(ScopeLifetime.Cached)]
    public partial class GameplayState : IState, IChildState<RootState>
    {
        public GameplayState()
        {
            AddControllers(
                new PlayerInputController(),
                new GameHUDController());
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
