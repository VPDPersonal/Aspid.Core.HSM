using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    [Transition(typeof(MatchState), typeof(GameOverState))]
    public partial class MatchToGameOverTransition : ITransition
    {
        public void OnBeforeTransition()
        {
            Debug.Log("[HSM] MatchToGameOver: uploading match results...");
        }

        public void OnAfterTransition()
        {
            Debug.Log("[HSM] MatchToGameOver: results screen shown");
        }
    }
}
