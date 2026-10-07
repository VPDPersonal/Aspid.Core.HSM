using _Scripts.States;
using Aspid.Core.HSM;
using UnityEngine;

namespace _Scripts.Transitions
{
    [Transition(typeof(SinglePlayerState), typeof(PauseState))]
    public class GameplayToPauseTransition : ITransition<SinglePlayerState, PauseState>
    {
        public void OnBeforeTransition()
        {
            Debug.Log("[HSM] GameplayToPause: saving quick state...");
        }

        public void OnAfterTransition()
        {
            Debug.Log("[HSM] GameplayToPause: pause menu opened");
        }
    }
}
