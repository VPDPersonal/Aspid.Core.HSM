using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    // Attribute style: the source generator implements IExtensionState.CanAttachTo for the
    // partial class from the [ExtensionFor] list. Extensions that need custom attach logic
    // implement CanAttachTo by hand instead (see SlowMotionExtension, DebugOverlayExtension).
    [ExtensionFor(typeof(ExplorationState), typeof(CombatState), typeof(DialogueState), typeof(MatchState), typeof(PauseState))]
    public partial class FpsCounterExtension : IExtensionState, IUpdateController, IGuiController
    {
        private int _frameCount;
        private float _elapsed;
        private float _currentFps;

        public void OnAttached(IState hostState)
        {
            Debug.Log($"[HSM] FpsCounter: attached to {hostState.GetType().Name}");
        }

        public void OnDetached(IState hostState)
        {
            Debug.Log($"[HSM] FpsCounter: detached (was {_currentFps:F1} FPS)");
        }

        public void Enter()
        {
            _frameCount = 0;
            _elapsed = 0;
            _currentFps = 0;
        }

        public void Update(float deltaTime)
        {
            _frameCount++;
            _elapsed += deltaTime;

            if (_elapsed >= 1f)
            {
                _currentFps = _frameCount / _elapsed;
                _frameCount = 0;
                _elapsed = 0;
            }
        }

        public void OnGui()
        {
            var rect = new Rect(Screen.width - 110, Screen.height - 36, 100, 26);
            GameStyles.DrawRect(rect, new Color(0f, 0f, 0f, 0.6f));
            GUI.Label(rect, $"{_currentFps:F0} FPS", GameStyles.Hint);
        }

        public void Exit()
        {
            Debug.Log("[HSM] FpsCounter: stopped");
        }
    }
}
