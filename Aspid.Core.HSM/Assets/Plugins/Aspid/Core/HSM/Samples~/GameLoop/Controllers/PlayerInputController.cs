using Aspid.Core.HSM;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aspid.Core.HSM.Samples.GameLoop.Controllers
{
    public class PlayerInputController : IUpdateController, IEnterController
    {
        public void OnEnter()
        {
            Debug.Log("[HSM] PlayerInput: controls activated");
        }

        public void Update(float deltaTime)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame)
                Debug.Log("[HSM] PlayerInput: Escape pressed — open pause");

            if (keyboard.f1Key.wasPressedThisFrame)
                Debug.Log("[HSM] PlayerInput: F1 pressed — toggle debug overlay");
        }
    }
}
