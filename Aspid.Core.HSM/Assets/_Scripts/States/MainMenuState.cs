using _Scripts.Controllers;
using Aspid.Core.HSM;
using UnityEngine;

namespace _Scripts.States
{
    [ControllerGroup]
    public partial class MainMenuState : IState, IChildState<RootState>
    {
        public MainMenuState()
        {
            AddControllers(
                new MenuInputController(),
                new MenuUIController());
        }

        public void Enter()
        {
            Debug.Log("[HSM] MainMenuState.Enter — showing main menu");
        }

        public void Exit()
        {
            Debug.Log("[HSM] MainMenuState.Exit — hiding main menu");
        }
    }
}
