using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    public class LobbyScreen : IGuiController
    {
        private readonly LobbyData _lobby;
        private readonly IGameActions _actions;

        public LobbyScreen(LobbyData lobby, IGameActions actions)
        {
            _lobby = lobby;
            _actions = actions;
        }

        public void OnGui()
        {
            GameStyles.DrawBackground(GameStyles.Night);

            var panel = GameStyles.Centered(440, 300, Screen.height * 0.25f);
            GameStyles.DrawRect(panel, GameStyles.Panel);

            GUI.Label(new Rect(panel.x, panel.y + 18, panel.width, 34), "Multiplayer Lobby", GameStyles.Heading);

            var barRect = new Rect(panel.x + 40, panel.y + 90, panel.width - 80, 26);

            if (_lobby.MatchReady)
            {
                GameStyles.DrawBar(barRect, 1f, GameStyles.HpGreen, "Match found!");

                if (GUI.Button(new Rect(panel.x + (panel.width - 220) / 2f, panel.y + 150, 220, 48),
                        "Join match", GameStyles.Button))
                    _actions.JoinMatch();
            }
            else
            {
                GameStyles.DrawBar(barRect, _lobby.MatchmakingProgress, GameStyles.SkyBlue,
                    $"Searching for players... {_lobby.MatchmakingProgress:P0}");

                GUI.Label(new Rect(panel.x, panel.y + 150, panel.width, 24),
                    "Hang tight — worthy opponents incoming", GameStyles.Hint);
            }

            if (GUI.Button(new Rect(panel.x + (panel.width - 160) / 2f, panel.yMax - 66, 160, 40),
                    "< Leave", GameStyles.Button))
                _actions.BackToMenu();
        }
    }
}
