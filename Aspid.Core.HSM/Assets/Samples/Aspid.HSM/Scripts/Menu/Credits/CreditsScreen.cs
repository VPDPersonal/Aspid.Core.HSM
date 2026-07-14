using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    public class CreditsScreen : IGuiController
    {
        private static readonly string[] Lines =
        {
            "ASPID QUEST",
            "",
            "A tiny adventure powered by",
            "Aspid.Core.HSM",
            "",
            "Hierarchical states",
            "Guarded transitions",
            "Controller groups",
            "State extensions",
            "",
            "Thanks for playing!",
        };

        private readonly CreditsRollController _roll;
        private readonly IGameActions _actions;

        public CreditsScreen(CreditsRollController roll, IGameActions actions)
        {
            _roll = roll;
            _actions = actions;
        }

        public void OnGui()
        {
            GameStyles.DrawBackground(GameStyles.Night);

            // Text block scrolls from below the screen up and out as progress goes 0 → 1
            var blockHeight = Lines.Length * 34f;
            var y = Mathf.Lerp(Screen.height, -blockHeight, _roll.Progress);

            for (var i = 0; i < Lines.Length; i++)
            {
                var style = i == 0 ? GameStyles.Heading : GameStyles.Body;
                GUI.Label(GameStyles.Centered(600, 34, y + i * 34f), Lines[i], style);
            }

            if (GUI.Button(new Rect(Screen.width - 130, Screen.height - 60, 110, 40), "Skip >", GameStyles.Button))
                _actions.BackToMenu();
        }
    }
}
