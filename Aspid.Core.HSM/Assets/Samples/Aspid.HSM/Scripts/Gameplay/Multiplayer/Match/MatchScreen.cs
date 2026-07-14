using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    public class MatchScreen : IGuiController
    {
        private readonly MatchData _match;

        public MatchScreen(MatchData match)
        {
            _match = match;
        }

        public void OnGui()
        {
            GameStyles.DrawBackground(new Color(0.10f, 0.22f, 0.14f));

            // Pitch markings
            var pitch = new Rect(70, 80, Screen.width - 140, Screen.height - 170);
            GameStyles.DrawRect(pitch, new Color(0.13f, 0.28f, 0.17f));
            GameStyles.DrawRect(new Rect(pitch.center.x - 2, pitch.y, 4, pitch.height), new Color(1f, 1f, 1f, 0.35f));

            // Timer
            var timeFill = _match.Duration > 0f ? _match.TimeLeft / _match.Duration : 0f;
            GameStyles.DrawBar(GameStyles.Centered(300, 26, 92), timeFill,
                timeFill < 0.25f ? GameStyles.HpRed : GameStyles.SkyBlue,
                $"{_match.TimeLeft:F0}s");

            GUI.Label(GameStyles.Centered(300, 50, 130), $"{_match.Goals} : 0",
                new GUIStyle(GameStyles.Title) { fontSize = 36 });

            if (GUI.Button(GameStyles.Centered(240, 60, Screen.height * 0.62f), "SHOOT!  (Space)", GameStyles.Button))
                _match.GoalRequested = true;
        }
    }
}
