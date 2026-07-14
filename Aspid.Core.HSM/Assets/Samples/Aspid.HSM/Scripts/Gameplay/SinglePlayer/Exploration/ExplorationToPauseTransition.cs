using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    // Pausing from Combat/Dialogue/Match has no registered transition and falls back to a
    // plain state change — only Exploration → Pause runs these hooks.
    [Transition(typeof(ExplorationState), typeof(PauseState))]
    public partial class ExplorationToPauseTransition : ITransition
    {
        public void OnBeforeTransition()
        {
            Debug.Log("[HSM] ExplorationToPause: saving quick state...");
        }

        public void OnAfterTransition()
        {
            Debug.Log("[HSM] ExplorationToPause: pause menu opened");
        }
    }
}
