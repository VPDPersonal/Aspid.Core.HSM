using _Scripts.States;
using Aspid.Core.HSM;
using UnityEngine;

namespace _Scripts.Transitions
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
