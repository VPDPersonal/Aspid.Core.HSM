using Aspid.Core.HSM;
using Aspid.Core.HSM.Samples.GameLoop.States;
using UnityEngine;

namespace Aspid.Core.HSM.Samples.GameLoop.Transitions
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
