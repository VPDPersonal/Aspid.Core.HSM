using Aspid.Core.HSM;
using UnityEngine;

namespace _Scripts.Controllers
{
    public class MenuUIController : IEnterController, IExitController
    {
        public void OnEnter()
        {
            Debug.Log("[HSM] MenuUI: UI enabled");
        }

        public void OnExit()
        {
            Debug.Log("[HSM] MenuUI: UI disabled");
        }
    }
}
