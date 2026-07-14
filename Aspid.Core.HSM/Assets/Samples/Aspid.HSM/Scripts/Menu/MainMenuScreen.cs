using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    public class MainMenuScreen : IGuiController
    {
        private readonly IGameActions _actions;

        public MainMenuScreen(IGameActions actions) => _actions = actions;

        public void OnGui()
        {
            GameStyles.DrawBackground(GameStyles.Night);
            GameStyles.DrawRect(new Rect(0, Screen.height * 0.2f + 70, Screen.width, 3), GameStyles.Accent);

            GUI.Label(GameStyles.Centered(600, 60, Screen.height * 0.2f), "ASPID QUEST", GameStyles.Title);

            var clicked = GameStyles.ButtonColumn(Screen.height * 0.42f, 280,
                "Adventure",
                "Multiplayer",
                "Settings",
                "Credits");

            switch (clicked)
            {
                case 0: _actions.StartAdventure(); break;
                case 1: _actions.OpenMultiplayer(); break;
                case 2: _actions.OpenSettings(); break;
                case 3: _actions.OpenCredits(); break;
            }

            GUI.Label(GameStyles.Centered(500, 24, Screen.height - 40),
                "Open Window > Aspid > State Tree to watch the HSM live", GameStyles.Hint);
        }
    }
}
