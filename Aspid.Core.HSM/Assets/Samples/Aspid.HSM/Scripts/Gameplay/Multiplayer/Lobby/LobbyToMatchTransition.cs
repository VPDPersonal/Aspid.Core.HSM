using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    // SampleGameManager.JoinMatch triggers this one via TransitionVia<LobbyToMatchTransition>()
    // — the registry is searched by transition type instead of the source/target pair.
    public class LobbyToMatchTransition : ITransition<LobbyState, MatchState>
    {
        private readonly GameSession _session;

        public LobbyToMatchTransition(GameSession session) => _session = session;

        public bool CanTransition()
        {
            if (!_session.Lobby.MatchReady)
            {
                Debug.LogWarning("[HSM] LobbyToMatch: blocked — matchmaking still in progress");
                return false;
            }

            return true;
        }

        public void OnBeforeTransition()
        {
            Debug.Log("[HSM] LobbyToMatch: loading arena...");
        }

        public void OnAfterTransition()
        {
            Debug.Log("[HSM] LobbyToMatch: all players spawned");
        }
    }
}
