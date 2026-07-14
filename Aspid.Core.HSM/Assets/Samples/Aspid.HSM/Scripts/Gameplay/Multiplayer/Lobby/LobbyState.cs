using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    [ControllerGroup]
    [ScopeLifetime(ScopeLifetime.Transient)]
    public partial class LobbyState : IState, IChildState<MultiplayerState>
    {
        public LobbyState(GameSession session, IGameActions actions)
        {
            AddControllers(
                new MatchmakingController(session),
                new LobbyScreen(session.Lobby, actions));
        }

        public void Enter()
        {
            Debug.Log("[HSM] LobbyState.Enter — multiplayer lobby");
        }

        public void Exit()
        {
            Debug.Log("[HSM] LobbyState.Exit");
        }
    }
}
