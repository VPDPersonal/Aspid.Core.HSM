using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    // Visual-only decay runs in LateUpdate, after all combat logic has raised the flashes
    // for the current frame — a natural fit for ILateUpdateController.
    public class HitFlashController : ILateUpdateController
    {
        private const float FadeSpeed = 3f;

        private readonly GameSession _session;

        public HitFlashController(GameSession session) => _session = session;

        public void LateUpdate(float deltaTime)
        {
            var combat = _session.Combat;
            combat.EnemyHitFlash = Mathf.Max(0f, combat.EnemyHitFlash - deltaTime * FadeSpeed);
            combat.PlayerHitFlash = Mathf.Max(0f, combat.PlayerHitFlash - deltaTime * FadeSpeed);
        }
    }
}
