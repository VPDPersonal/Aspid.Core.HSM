using UnityEngine;
using UnityEngine.InputSystem;

namespace Aspid.Core.HSM.Sample
{
    public class PlayerCombatController : IUpdateController
    {
        private readonly GameSession _session;

        public PlayerCombatController(GameSession session) => _session = session;

        public void Update(float deltaTime)
        {
            var combat = _session.Combat;
            if (combat.Result != CombatResult.None) return;

            var keyboard = Keyboard.current;
            var attack = combat.AttackRequested ||
                         (keyboard != null && keyboard.spaceKey.wasPressedThisFrame);
            combat.AttackRequested = false;

            if (!attack) return;

            var damage = Random.Range(10, 26);
            combat.EnemyHp -= damage;
            combat.EnemyHitFlash = 1f;
            combat.Log = $"You hit for {damage}!";
            Debug.Log($"[HSM] Combat: player hits for {damage}, enemy HP = {Mathf.Max(0, combat.EnemyHp)}");

            if (combat.EnemyHp <= 0)
            {
                _session.Player.Score += 100;
                combat.Result = CombatResult.Victory;
                combat.Log = "Monster defeated! +100 score";
                Debug.Log("[HSM] Combat: VICTORY");
            }
        }
    }
}
