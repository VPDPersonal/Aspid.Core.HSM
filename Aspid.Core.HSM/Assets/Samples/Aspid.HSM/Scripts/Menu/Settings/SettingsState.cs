using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    [ControllerGroup]
    public partial class SettingsState : IState, IChildState<MainMenuState>
    {
        public SettingsState(GameSession session, IGameActions actions)
        {
            AddControllers(
                new SettingsPersistenceController(),
                new SettingsScreen(session.Settings, actions));
        }

        public void Enter()
        {
            Debug.Log("[HSM] SettingsState.Enter — settings screen opened");
        }

        public void Exit()
        {
            Debug.Log("[HSM] SettingsState.Exit — settings screen closed");
        }
    }
}
