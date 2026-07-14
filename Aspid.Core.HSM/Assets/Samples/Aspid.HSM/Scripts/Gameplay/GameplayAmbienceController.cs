using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    public class GameplayAmbienceController : IEnterController, IExitController
    {
        public void OnEnter()
        {
            Debug.Log("[HSM] Ambience: gameplay music fades in");
        }

        public void OnExit()
        {
            Debug.Log("[HSM] Ambience: gameplay music fades out");
        }
    }
}
