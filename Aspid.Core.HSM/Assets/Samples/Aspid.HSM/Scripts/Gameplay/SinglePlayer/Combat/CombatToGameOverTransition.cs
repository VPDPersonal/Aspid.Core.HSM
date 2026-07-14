using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    [Transition(typeof(CombatState), typeof(GameOverState))]
    public partial class CombatToGameOverTransition : ITransition
    {
        public void OnBeforeTransition()
        {
            Debug.Log("[HSM] CombatToGameOver: fade to black...");
        }

        public void OnAfterTransition()
        {
            Debug.Log("[HSM] CombatToGameOver: game over screen shown");
        }
    }
}
