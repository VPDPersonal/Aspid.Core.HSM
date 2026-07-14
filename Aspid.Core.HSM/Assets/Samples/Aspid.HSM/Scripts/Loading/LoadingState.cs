using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    // The group's enter phase is async (AssetLoadingController), so this state must be
    // entered via ChangeStateAsync — SampleGameManager.BootAsync does exactly that.
    [ControllerGroup]
    [ScopeLifetime(ScopeLifetime.Transient)]
    public partial class LoadingState : IState, IChildState<RootState>
    {
        public LoadingState()
        {
            var loading = new AssetLoadingController();
            AddControllers(
                loading,
                new LoadingScreen(loading));
        }

        public void Enter()
        {
            Debug.Log("[HSM] LoadingState.Enter — loading screen shown");
        }

        public void Exit()
        {
            Debug.Log("[HSM] LoadingState.Exit — loading screen hidden");
        }
    }
}
