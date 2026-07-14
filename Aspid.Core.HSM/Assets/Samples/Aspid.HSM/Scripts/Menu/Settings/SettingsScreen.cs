using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    public class SettingsScreen : IGuiController
    {
        private readonly SettingsData _settings;
        private readonly IGameActions _actions;

        public SettingsScreen(SettingsData settings, IGameActions actions)
        {
            _settings = settings;
            _actions = actions;
        }

        public void OnGui()
        {
            GameStyles.DrawBackground(GameStyles.Night);

            var panel = GameStyles.Centered(420, 300, Screen.height * 0.25f);
            GameStyles.DrawRect(panel, GameStyles.Panel);

            GUI.Label(new Rect(panel.x, panel.y + 16, panel.width, 34), "Settings", GameStyles.Heading);

            DrawSlider(panel, 90, "Music", ref _settings.MusicVolume);
            DrawSlider(panel, 150, "Effects", ref _settings.SfxVolume);

            if (GUI.Button(new Rect(panel.x + (panel.width - 200) / 2f, panel.yMax - 70, 200, 44), "< Back", GameStyles.Button))
                _actions.BackToMenu();
        }

        private static void DrawSlider(Rect panel, float y, string label, ref float value)
        {
            GUI.Label(new Rect(panel.x + 30, panel.y + y, 100, 24), label, GameStyles.Body);
            value = GUI.HorizontalSlider(new Rect(panel.x + 140, panel.y + y + 8, 200, 20), value, 0f, 1f);
            GUI.Label(new Rect(panel.x + 348, panel.y + y, 50, 24), $"{value:P0}", GameStyles.Hint);
        }
    }
}
