using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    // Interface style: ITransition<TSource, TTarget> supplies SourceState/TargetState via
    // default interface members — used for guarded transitions that carry dependencies.
    public class CombatToExplorationTransition : ITransition<CombatState, ExplorationState>
    {
        private readonly GameSession _session;

        public CombatToExplorationTransition(GameSession session) => _session = session;

        public bool CanTransition()
        {
            if (_session.Combat.Result != CombatResult.Victory)
            {
                Debug.LogWarning("[HSM] CombatToExploration: blocked — battle is not won yet");
                return false;
            }

            return true;
        }

        public void OnBeforeTransition()
        {
            Debug.Log("[HSM] CombatToExploration: collecting loot...");
        }

        public void OnAfterTransition()
        {
            Debug.Log("[HSM] CombatToExploration: back to free roam");
        }
    }
}
