using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    public class EnemyAIController : IUpdateController, IEnterController
    {
        private readonly GameSession _session;
        private float _attackTimer;

        public EnemyAIController(GameSession session) => _session = session;

        public void OnEnter()
        {
            _attackTimer = 2f;
        }

        public void Update(float deltaTime)
        {
            var combat = _session.Combat;
            if (combat.Result != CombatResult.None) return;

            _attackTimer -= deltaTime;
            if (_attackTimer > 0f) return;

            _attackTimer = Random.Range(1.5f, 3f);
            var damage = Random.Range(5, 16);
            _session.Player.Hp -= damage;
            combat.PlayerHitFlash = 1f;
            combat.Log = $"Monster hits you for {damage}!";
            Debug.Log($"[HSM] Combat: enemy hits for {damage}, player HP = {Mathf.Max(0, _session.Player.Hp)}");

            if (_session.Player.Hp <= 0)
            {
                combat.Result = CombatResult.Defeat;
                _session.GameOverReason = "You fell in battle";
                combat.Log = "You have been defeated...";
                Debug.Log("[HSM] Combat: DEFEAT");
            }
        }
    }
}
