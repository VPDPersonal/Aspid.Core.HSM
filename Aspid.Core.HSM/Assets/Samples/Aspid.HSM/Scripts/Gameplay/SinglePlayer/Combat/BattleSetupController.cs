using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    // Registered first in CombatState's group so the battle exists before the AI and log
    // controllers run their own OnEnter.
    public class BattleSetupController : IEnterController
    {
        private readonly GameSession _session;

        public BattleSetupController(GameSession session) => _session = session;

        public void OnEnter()
        {
            var combat = _session.Combat;

            // The state instance is Transient, but the fight lives in the session:
            // a live enemy with no result means we are resuming from pause, not starting a new battle.
            if (combat.Result != CombatResult.None || combat.EnemyHp <= 0)
            {
                combat.EnemyMaxHp = Random.Range(40, 81);
                combat.EnemyHp = combat.EnemyMaxHp;
                combat.Result = CombatResult.None;
                Debug.Log($"[HSM] BattleSetup: monster with {combat.EnemyHp} HP");
            }
            else
            {
                Debug.Log($"[HSM] BattleSetup: resuming battle, monster at {combat.EnemyHp}/{combat.EnemyMaxHp} HP");
            }

            combat.AttackRequested = false;
        }
    }
}
