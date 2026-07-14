using UnityEngine;
using UnityEngine.InputSystem;

namespace Aspid.Core.HSM.Sample
{
    // Owns the whole conversation: lines, cursor, completion. The screen reads it and pushes
    // the "next" intent back through RequestAdvance(); DialogueState only forwards IsFinished.
    public class ConversationController : IUpdateController, IEnterController
    {
        private static readonly string[] Lines =
        {
            "Stranger: You look lost, traveler.",
            "You: Just exploring these lands.",
            "Stranger: Beware — monsters roam when you wander.",
            "You: Thanks for the warning.",
            "Stranger: Safe travels!",
        };

        private readonly GameSession _session;
        private int _lineIndex;
        private bool _advanceRequested;

        public ConversationController(GameSession session) => _session = session;

        public string CurrentLine => Lines[Mathf.Min(_lineIndex, Lines.Length - 1)];

        public int LineIndex => _lineIndex;

        public int LineCount => Lines.Length;

        public bool IsFinished { get; private set; }

        public void RequestAdvance() => _advanceRequested = true;

        public void OnEnter()
        {
            _lineIndex = 0;
            _advanceRequested = false;
            IsFinished = false;
            Debug.Log($"[HSM] Dialogue: \"{CurrentLine}\" (Space = next)");
        }

        public void Update(float deltaTime)
        {
            if (IsFinished) return;

            var keyboard = Keyboard.current;
            var advance = _advanceRequested ||
                          (keyboard != null && keyboard.spaceKey.wasPressedThisFrame);
            _advanceRequested = false;

            if (!advance) return;

            _lineIndex++;

            if (_lineIndex >= Lines.Length)
            {
                IsFinished = true;
                _session.Player.Score += 25;
                Debug.Log("[HSM] Dialogue: conversation finished, +25 score");
            }
            else
            {
                Debug.Log($"[HSM] Dialogue: \"{CurrentLine}\"");
            }
        }
    }
}
