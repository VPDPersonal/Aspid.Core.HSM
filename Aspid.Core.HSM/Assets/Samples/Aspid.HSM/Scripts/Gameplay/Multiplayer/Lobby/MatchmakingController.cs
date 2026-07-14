using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    public class MatchmakingController : IUpdateController, IEnterController
    {
        private const float MatchmakingDuration = 3f;

        private readonly GameSession _session;

        public MatchmakingController(GameSession session) => _session = session;

        public void OnEnter()
        {
            _session.Lobby.MatchmakingProgress = 0f;
            _session.Lobby.MatchReady = false;
            Debug.Log("[HSM] Matchmaking: search started...");
        }

        public void Update(float deltaTime)
        {
            var lobby = _session.Lobby;
            if (lobby.MatchReady) return;

            lobby.MatchmakingProgress += deltaTime / MatchmakingDuration;

            if (lobby.MatchmakingProgress >= 1f)
            {
                lobby.MatchmakingProgress = 1f;
                lobby.MatchReady = true;
                Debug.Log("[HSM] Matchmaking: match found! Press Enter to join");
            }
        }
    }
}
