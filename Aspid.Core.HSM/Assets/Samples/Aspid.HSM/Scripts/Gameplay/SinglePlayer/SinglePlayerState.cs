using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    // A state may implement controller interfaces itself — reserved in this sample for
    // trivial behavior like this playtime tracker; anything bigger goes into a controller
    // group (see ExplorationState).
    public class SinglePlayerState : IState, IChildState<GameplayState>, IUpdateController, IEnterController
    {
        private float _elapsed;

        public void Enter()
        {
            Debug.Log("[HSM] SinglePlayerState.Enter");
            _elapsed = 0f;
        }

        public void OnEnter()
        {
            Debug.Log("[HSM] SinglePlayerState.OnEnter — single player session started");
        }

        public void Update(float deltaTime)
        {
            _elapsed += deltaTime;
        }

        public void Exit()
        {
            Debug.Log($"[HSM] SinglePlayerState.Exit — session lasted {_elapsed:F0}s");
        }
    }
}
