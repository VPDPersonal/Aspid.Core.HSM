using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    // Trivial connect/disconnect logs — small enough to stay a state-as-its-own-controller
    [ScopeLifetime(ScopeLifetime.Cached)]
    public class MultiplayerState : IState, IChildState<GameplayState>, IEnterController, IExitController
    {
        public void Enter()
        {
            Debug.Log("[HSM] MultiplayerState.Enter — online session");
        }

        public void OnEnter()
        {
            Debug.Log("[HSM] Multiplayer: connecting to server...");
        }

        public void OnExit()
        {
            Debug.Log("[HSM] Multiplayer: disconnected from server");
        }

        public void Exit()
        {
            Debug.Log("[HSM] MultiplayerState.Exit");
        }
    }
}
