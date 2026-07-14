using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    // Attribute style: the source generator implements ITransition.SourceState/TargetState
    // for the partial class. Used here for stateless hook-only transitions; guarded transitions
    // implement ITransition<TSource, TTarget> by hand instead (see CombatToExplorationTransition).
    [Transition(typeof(ExplorationState), typeof(CombatState))]
    public partial class ExplorationToCombatTransition : ITransition
    {
        public void OnBeforeTransition()
        {
            Debug.Log("[HSM] ExplorationToCombat: battle music fades in...");
        }

        public void OnAfterTransition()
        {
            Debug.Log("[HSM] ExplorationToCombat: battle arena ready");
        }
    }
}
