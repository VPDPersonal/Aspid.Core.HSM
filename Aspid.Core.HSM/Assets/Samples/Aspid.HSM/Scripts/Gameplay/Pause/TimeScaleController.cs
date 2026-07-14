using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    public class TimeScaleController : IEnterController, IExitController
    {
        public void OnEnter()
        {
            Time.timeScale = 0f;
            Debug.Log("[HSM] TimeScale: set to 0");
        }

        public void OnExit()
        {
            Time.timeScale = 1f;
            Debug.Log("[HSM] TimeScale: restored to 1");
        }
    }
}
