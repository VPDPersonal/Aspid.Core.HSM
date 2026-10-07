using Aspid.Core.HSM;
using UnityEngine;

namespace Aspid.Core.HSM.Samples.GameLoop.Controllers
{
    public class GameHUDController : IEnterController, IExitController
    {
        public void OnEnter()
        {
            Debug.Log("[HSM] GameHUD: HUD shown");
        }

        public void OnExit()
        {
            Debug.Log("[HSM] GameHUD: HUD hidden");
        }
    }
}
