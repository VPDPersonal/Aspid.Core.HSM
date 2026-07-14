using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    public class SettingsPersistenceController : IEnterController, IExitController
    {
        public void OnEnter()
        {
            Debug.Log("[HSM] SettingsPersistence: loading saved preferences...");
        }

        public void OnExit()
        {
            Debug.Log("[HSM] SettingsPersistence: preferences saved");
        }
    }
}
