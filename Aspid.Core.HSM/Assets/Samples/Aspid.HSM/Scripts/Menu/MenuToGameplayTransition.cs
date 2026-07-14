using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    // Interface style: ITransition<TSource, TTarget> supplies SourceState/TargetState via
    // default interface members — no attribute, no partial and no generator involved.
    public class MenuToGameplayTransition : ITransition<MainMenuState, ExplorationState>
    {
        public bool ProfileLoaded { get; set; } = true;

        public bool CanTransition()
        {
            if (!ProfileLoaded)
            {
                Debug.LogWarning("[HSM] MenuToGameplay: blocked — profile not loaded");
                return false;
            }

            return true;
        }

        public void OnBeforeTransition()
        {
            Debug.Log("[HSM] MenuToGameplay: preparing gameplay session...");
        }

        public void OnAfterTransition()
        {
            Debug.Log("[HSM] MenuToGameplay: gameplay session ready");
        }
    }
}
