using _Scripts.Controllers;
using Aspid.Core.HSM;
using UnityEngine;

namespace _Scripts.States
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
