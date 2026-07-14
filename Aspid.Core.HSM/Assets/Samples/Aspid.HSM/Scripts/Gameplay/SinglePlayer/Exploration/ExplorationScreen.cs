using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    public class ExplorationScreen : IGuiController
    {
        private readonly GameSession _session;

        public ExplorationScreen(GameSession session) => _session = session;

        public void OnGui()
        {
            var exploration = _session.Exploration;

            GameStyles.DrawBackground(GameStyles.Grass);

            var field = FieldRect();
            GameStyles.DrawRect(field, GameStyles.GrassLight);

            // A few decorative "bushes" at fixed spots
            DrawDot(field, new Vector2(0.1f, 0.2f), 18, new Color(0.10f, 0.24f, 0.12f));
            DrawDot(field, new Vector2(0.5f, 0.8f), 24, new Color(0.10f, 0.24f, 0.12f));
            DrawDot(field, new Vector2(0.88f, 0.65f), 16, new Color(0.10f, 0.24f, 0.12f));

            // NPC
            var npcRect = DrawDot(field, exploration.NpcPos, 26, GameStyles.SkyBlue);
            GUI.Label(new Rect(npcRect.x - 30, npcRect.y - 26, 86, 22), "Stranger", GameStyles.Hint);

            // Player
            var playerRect = DrawDot(field, exploration.PlayerPos, 26, GameStyles.Accent);

            if (exploration.NearNpc)
            {
                GUI.Label(new Rect(playerRect.x - 60, playerRect.yMax + 4, 146, 24),
                    "<b>E</b> — talk", GameStyles.Body);
            }

            GUI.Label(GameStyles.Centered(500, 24, Screen.height - 34),
                "WASD — move   |   monsters lurk while you wander...", GameStyles.Hint);
        }

        private static Rect FieldRect()
        {
            var margin = 70f;
            return new Rect(margin, margin, Screen.width - margin * 2, Screen.height - margin * 2);
        }

        private static Rect DrawDot(Rect field, Vector2 normalizedPos, float size, Color color)
        {
            var rect = new Rect(
                field.x + normalizedPos.x * field.width - size / 2f,
                field.y + normalizedPos.y * field.height - size / 2f,
                size, size);
            GameStyles.DrawRect(rect, color);
            return rect;
        }
    }
}
