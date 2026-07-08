using Aspid.Core.HSM;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Scripts.Controllers
{
    public class MenuInputController : IUpdateController
    {
        public void Update(float deltaTime)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.enterKey.wasPressedThisFrame)
                Debug.Log("[HSM] MenuInput: Enter pressed — start game");

            if (keyboard.escapeKey.wasPressedThisFrame)
                Debug.Log("[HSM] MenuInput: Escape pressed — quit");
        }
    }
}
