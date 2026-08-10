using Aspid.Core.HSM;
using UnityEngine;

namespace Aspid.Core.HSM.Samples.GameLoop.Extensions
{
    public class DebugOverlayExtension : IExtensionState, IUpdateController
    {
        public bool CanAttachTo(IState hostState) => true;

        public void OnAttached(IState hostState)
        {
            Debug.Log($"[HSM] DebugOverlay: attached to {hostState.GetType().Name}");
        }

        public void OnDetached(IState hostState)
        {
            Debug.Log($"[HSM] DebugOverlay: detached from {hostState.GetType().Name}");
        }

        public void Enter()
        {
            Debug.Log("[HSM] DebugOverlay: enabled");
        }

        public void Update(float deltaTime)
        {
            // In a real game this would render debug info via IMGUI or UIToolkit
        }

        public void Exit()
        {
            Debug.Log("[HSM] DebugOverlay: disabled");
        }
    }
}
