using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    // Actual hotkey handling lives in SampleGameManager.HandleHotkeys — this controller
    // only demonstrates enabling/disabling an input layer with the gameplay branch.
    public class PlayerInputController : IEnterController, IExitController
    {
        public void OnEnter()
        {
            Debug.Log("[HSM] PlayerInput: gameplay controls enabled");
        }

        public void OnExit()
        {
            Debug.Log("[HSM] PlayerInput: gameplay controls disabled");
        }
    }
}
