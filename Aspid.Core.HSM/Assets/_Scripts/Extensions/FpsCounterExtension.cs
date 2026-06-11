using _Scripts.States;
using Aspid.Core.HSM;
using UnityEngine;

namespace _Scripts.Extensions
{
    [ExtensionFor(typeof(GameplayState), typeof(SinglePlayerState), typeof(PauseState))]
    public class FpsCounterExtension : IExtensionState, IUpdateController
    {
        private float _frameCount;
        private float _elapsed;
        private float _currentFps;

        public bool CanAttachTo(IState hostState) =>
            hostState is GameplayState or SinglePlayerState or PauseState;

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
                Debug.Log($"[HSM] FPS: {_currentFps:F1}");
                _frameCount = 0;
                _elapsed = 0;
            }
        }

        public void Exit()
        {
            Debug.Log("[HSM] FpsCounter: stopped");
        }
    }
}
