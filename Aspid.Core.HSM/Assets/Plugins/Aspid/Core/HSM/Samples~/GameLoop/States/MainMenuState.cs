using Aspid.Core.HSM;
using Aspid.Core.HSM.Samples.GameLoop.Controllers;
using UnityEngine;

namespace Aspid.Core.HSM.Samples.GameLoop.States
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
