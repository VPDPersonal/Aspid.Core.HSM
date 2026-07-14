using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    public class MenuInputController : IUpdateController, IEnterController
    {
        private const float IdleHintDelay = 20f;

        private float _idleTime;
        private bool _hintShown;

        public void OnEnter()
        {
            _idleTime = 0f;
            _hintShown = false;
        }

        public void Update(float deltaTime)
        {
            if (_hintShown) return;

            _idleTime += deltaTime;
            if (_idleTime >= IdleHintDelay)
            {
                _hintShown = true;
                Debug.Log("[HSM] MenuInput: still here? Click Adventure to set off!");
            }
        }
    }
}
