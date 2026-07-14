using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    [Transition(typeof(PauseState), typeof(ExplorationState))]
    public partial class PauseToExplorationTransition : ITransition
    {
        public void OnBeforeTransition()
        {
            Debug.Log("[HSM] PauseToExploration: resuming...");
        }

        public void OnAfterTransition()
        {
            Debug.Log("[HSM] PauseToExploration: gameplay resumed");
        }
    }
}
