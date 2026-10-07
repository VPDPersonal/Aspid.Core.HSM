using Aspid.Core.HSM;
using UnityEngine;

namespace _Scripts.States
{
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
            if (_elapsed >= 5f)
            {
                _elapsed = 0f;
                Debug.Log("[HSM] SinglePlayerState: 5 seconds elapsed");
            }
        }

        public void Exit()
        {
            Debug.Log("[HSM] SinglePlayerState.Exit");
        }
    }
}
