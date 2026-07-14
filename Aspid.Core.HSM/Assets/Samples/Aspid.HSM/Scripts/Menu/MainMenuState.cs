using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    [ControllerGroup]
    public partial class MainMenuState : IState, IChildState<RootState>
    {
        public MainMenuState(IGameActions actions)
        {
            AddControllers(
                new MenuInputController(),
                new MenuUIController(),
                new MainMenuScreen(actions));
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
