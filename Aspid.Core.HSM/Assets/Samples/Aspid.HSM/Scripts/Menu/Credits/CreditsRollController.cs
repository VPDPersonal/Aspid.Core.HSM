using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    public class CreditsRollController : IUpdateController, IEnterController
    {
        private const float Duration = 8f;

        private float _elapsed;

        public bool IsFinished { get; private set; }

        public float Progress => Mathf.Clamp01(_elapsed / Duration);

        public void OnEnter()
        {
            _elapsed = 0f;
            IsFinished = false;
        }

        public void Update(float deltaTime)
        {
            _elapsed += deltaTime;
            if (_elapsed >= Duration && !IsFinished)
            {
                IsFinished = true;
                Debug.Log("[HSM] Credits: finished rolling");
            }
        }
    }
}
