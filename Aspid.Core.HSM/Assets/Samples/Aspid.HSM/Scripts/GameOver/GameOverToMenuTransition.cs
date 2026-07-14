using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    public class GameOverToMenuTransition : ITransition<GameOverState, MainMenuState>
    {
        private readonly GameSession _session;

        public GameOverToMenuTransition(GameSession session) => _session = session;

        public void OnBeforeTransition()
        {
            Debug.Log("[HSM] GameOverToMenu: resetting run...");
            _session.ResetRun();
        }

        public void OnAfterTransition()
        {
            Debug.Log("[HSM] GameOverToMenu: back at main menu");
        }
    }
}
