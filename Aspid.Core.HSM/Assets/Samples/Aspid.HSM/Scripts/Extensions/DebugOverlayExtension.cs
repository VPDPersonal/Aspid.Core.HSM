using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    // Manual style: attaches to any host state, so CanAttachTo is a hand-written `true`.
    public class DebugOverlayExtension : IExtensionState, IGuiController
    {
        private readonly GameSession _session;
        private string _hostName = "?";

        public DebugOverlayExtension(GameSession session) => _session = session;

        public bool CanAttachTo(IState hostState) => true;

        public void OnAttached(IState hostState)
        {
            _hostName = hostState.GetType().Name;
            Debug.Log($"[HSM] DebugOverlay: attached to {_hostName}");
        }

        public void OnDetached(IState hostState)
        {
            Debug.Log($"[HSM] DebugOverlay: detached from {hostState.GetType().Name}");
        }

        public void Enter()
        {
            Debug.Log("[HSM] DebugOverlay: enabled");
        }

        public void OnGui()
        {
            var rect = new Rect(10, Screen.height - 96, 300, 86);
            GameStyles.DrawRect(rect, new Color(0f, 0f, 0f, 0.6f));

            var exploration = _session.Exploration;
            GUI.Label(new Rect(rect.x + 10, rect.y + 6, rect.width - 20, 22),
                $"DEBUG  |  host: {_hostName}", GameStyles.Hint);
            GUI.Label(new Rect(rect.x + 10, rect.y + 28, rect.width - 20, 22),
                $"pos: ({exploration.PlayerPos.x:F2}, {exploration.PlayerPos.y:F2})  hp: {_session.Player.Hp}", GameStyles.Hint);
            GUI.Label(new Rect(rect.x + 10, rect.y + 50, rect.width - 20, 22),
                $"score: {_session.Player.Score}  walked: {exploration.DistanceWalked:F0}m", GameStyles.Hint);
        }

        public void Exit()
        {
            Debug.Log("[HSM] DebugOverlay: disabled");
        }
    }
}
