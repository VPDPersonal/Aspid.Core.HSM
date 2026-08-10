using Aspid.Core.HSM;
using Aspid.Core.HSM.Samples.GameLoop.States;
using UnityEngine;

namespace Aspid.Core.HSM.Samples.GameLoop.Transitions
{
    [Transition(typeof(PauseState), typeof(SinglePlayerState))]
    public class PauseToGameplayTransition : ITransition<PauseState, SinglePlayerState>
    {
        public void OnBeforeTransition()
        {
            Debug.Log("[HSM] PauseToGameplay: resuming...");
        }

        public void OnAfterTransition()
        {
            Debug.Log("[HSM] PauseToGameplay: gameplay resumed");
        }
    }
}
