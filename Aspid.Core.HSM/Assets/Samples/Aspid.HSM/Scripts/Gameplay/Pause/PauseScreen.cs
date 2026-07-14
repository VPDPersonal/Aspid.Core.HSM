using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    public class PauseScreen : IGuiController
    {
        private readonly IGameActions _actions;

        public PauseScreen(IGameActions actions) => _actions = actions;

        public void OnGui()
        {
            GameStyles.DrawBackground(new Color(0f, 0f, 0f, 0.72f));

            GUI.Label(GameStyles.Centered(400, 60, Screen.height * 0.28f), "PAUSED", GameStyles.Title);

            var clicked = GameStyles.ButtonColumn(Screen.height * 0.45f, 240,
                "Resume",
                "Main menu");

            switch (clicked)
            {
                case 0: _actions.ResumeGame(); break;
                case 1: _actions.BackToMenu(); break;
            }
        }
    }
}
