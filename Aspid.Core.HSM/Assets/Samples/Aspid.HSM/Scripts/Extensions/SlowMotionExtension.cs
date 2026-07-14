using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    // Manual style: CanAttachTo is implemented by hand — the [ExtensionFor] generator
    // alternative is shown in FpsCounterExtension.
    public class SlowMotionExtension : IExtensionState
    {
        private const float SlowScale = 0.4f;

        public bool CanAttachTo(IState hostState) => hostState is CombatState;

        public void OnAttached(IState hostState)
        {
            Debug.Log($"[HSM] SlowMotion: attached to {hostState.GetType().Name}");
        }

        public void OnDetached(IState hostState)
        {
            Debug.Log("[HSM] SlowMotion: detached");
        }

        public void Enter()
        {
            Time.timeScale = SlowScale;
            Debug.Log("[HSM] SlowMotion: bullet time ON (timeScale = 0.4)");
        }

        public void Exit()
        {
            // Auto-detach fires after the new state has entered (e.g. Pause already set 0) —
            // only restore the scale if it is still ours.
            if (Mathf.Approximately(Time.timeScale, SlowScale))
                Time.timeScale = 1f;

            Debug.Log("[HSM] SlowMotion: bullet time OFF");
        }
    }
}
