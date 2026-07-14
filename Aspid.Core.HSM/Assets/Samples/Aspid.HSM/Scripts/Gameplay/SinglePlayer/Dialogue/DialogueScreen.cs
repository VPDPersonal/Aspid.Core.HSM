using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    public class DialogueScreen : IGuiController
    {
        private readonly ConversationController _conversation;

        public DialogueScreen(ConversationController conversation) => _conversation = conversation;

        public void OnGui()
        {
            // Same scenery as exploration, dimmed — we're still in the field, just talking
            GameStyles.DrawBackground(GameStyles.Grass);
            GameStyles.DrawBackground(new Color(0f, 0f, 0f, 0.45f));

            var panel = new Rect(60, Screen.height - 190, Screen.width - 120, 150);
            GameStyles.DrawRect(panel, GameStyles.Panel);

            // Portrait
            var portrait = new Rect(panel.x + 16, panel.y + 16, 70, 70);
            GameStyles.DrawRect(portrait, GameStyles.SkyBlue);

            // Friendly face
            GameStyles.DrawRect(new Rect(portrait.x + 16, portrait.y + 22, 10, 10), GameStyles.Night);
            GameStyles.DrawRect(new Rect(portrait.xMax - 26, portrait.y + 22, 10, 10), GameStyles.Night);
            GameStyles.DrawRect(new Rect(portrait.x + 18, portrait.yMax - 22, portrait.width - 36, 6), GameStyles.Night);

            GUI.Label(new Rect(portrait.xMax + 16, panel.y + 14, panel.width - 200, 26),
                _conversation.CurrentLine.Split(':')[0], GameStyles.Heading);

            GUI.Label(new Rect(portrait.xMax + 16, panel.y + 48, panel.width - 220, 60),
                _conversation.CurrentLine, new GUIStyle(GameStyles.Body) { alignment = TextAnchor.UpperLeft });

            GUI.Label(new Rect(panel.x + 16, panel.yMax - 30, 100, 22),
                $"{_conversation.LineIndex + 1}/{_conversation.LineCount}", GameStyles.Hint);

            if (GUI.Button(new Rect(panel.xMax - 150, panel.yMax - 56, 134, 40), "Next >", GameStyles.Button))
                _conversation.RequestAdvance();
        }
    }
}
