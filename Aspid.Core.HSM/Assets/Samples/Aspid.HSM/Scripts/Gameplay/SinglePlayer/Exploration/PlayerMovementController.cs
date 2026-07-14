using UnityEngine;
using UnityEngine.InputSystem;

namespace Aspid.Core.HSM.Sample
{
    public class PlayerMovementController : IUpdateController, IExitController
    {
        private const float MoveSpeed = 0.25f; // normalized field units per second

        private readonly GameSession _session;

        public PlayerMovementController(GameSession session) => _session = session;

        public void Update(float deltaTime)
        {
            var exploration = _session.Exploration;
            exploration.IsMoving = false;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            var move = Vector2.zero;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y -= 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1f;

            if (move == Vector2.zero) return;

            move = move.normalized * (MoveSpeed * deltaTime);
            var pos = exploration.PlayerPos + move;
            pos.x = Mathf.Clamp01(pos.x);
            pos.y = Mathf.Clamp01(pos.y);
            exploration.PlayerPos = pos;
            exploration.DistanceWalked += move.magnitude * 100f;
            exploration.IsMoving = true;
        }

        public void OnExit()
        {
            Debug.Log($"[HSM] Movement: walked {_session.Exploration.DistanceWalked:F0}m total");
        }
    }
}
