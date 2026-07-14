using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    [Transition(typeof(DialogueState), typeof(ExplorationState))]
    public partial class DialogueToExplorationTransition : ITransition
    {
        public void OnBeforeTransition()
        {
            Debug.Log("[HSM] DialogueToExploration: waving goodbye...");
        }

        public void OnAfterTransition()
        {
            Debug.Log("[HSM] DialogueToExploration: back on the road");
        }
    }
}
