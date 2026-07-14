using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    public class GameOverScreen : IGuiController
    {
        private readonly GameSession _session;
        private readonly IGameActions _actions;

        public GameOverScreen(GameSession session, IGameActions actions)
        {
            _session = session;
            _actions = actions;
        }

        public void OnGui()
        {
            GameStyles.DrawBackground(new Color(0.05f, 0.04f, 0.06f));

            GUI.Label(GameStyles.Centered(500, 60, Screen.height * 0.22f), "GAME OVER",
                new GUIStyle(GameStyles.Title) { normal = { textColor = GameStyles.HpRed } });

            GUI.Label(GameStyles.Centered(500, 30, Screen.height * 0.36f), _session.GameOverReason, GameStyles.Body);
            GUI.Label(GameStyles.Centered(500, 30, Screen.height * 0.42f), $"Final score: {_session.Player.Score}", GameStyles.Heading);

            var clicked = GameStyles.ButtonColumn(Screen.height * 0.55f, 240,
                "Try again",
                "Main menu");

            switch (clicked)
            {
                case 0: _actions.Retry(); break;
                case 1: _actions.BackToMenu(); break;
            }
        }
    }
}
