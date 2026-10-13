using Aspid.Core.HSM;
using Aspid.Core.HSM.Samples.GameLoop.States;
using UnityEngine;

namespace Aspid.Core.HSM.Samples.GameLoop.Transitions
{
    // The menu reaches gameplay through LoadingState, so the guarded edge leaves the loading screen.
    [Transition(typeof(LoadingState), typeof(SinglePlayerState))]
    public class MenuToGameplayTransition : ITransition<LoadingState, SinglePlayerState>
    {
        private bool _profileLoaded = true;

        public bool ProfileLoaded
        {
            get => _profileLoaded;
            set => _profileLoaded = value;
        }

        public bool CanTransition()
        {
            if (!_profileLoaded)
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
