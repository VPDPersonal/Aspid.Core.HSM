using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    // Reads IsMoving that PlayerMovementController wrote earlier this frame — controllers in a
    // group run in registration order, so ordering-sensitive logic just registers later.
    public class WildEncounterController : IUpdateController, IEnterController
    {
        private readonly GameSession _session;
        private float _encounterTimer;

        public WildEncounterController(GameSession session) => _session = session;

        public void OnEnter()
        {
            _session.Exploration.EncounterTriggered = false;
            _encounterTimer = Random.Range(5f, 9f);
        }

        public void Update(float deltaTime)
        {
            var exploration = _session.Exploration;

            // Monsters only strike while you wander — standing still is safe
            if (!exploration.IsMoving || exploration.EncounterTriggered || exploration.NearNpc)
                return;

            _encounterTimer -= deltaTime;
            if (_encounterTimer <= 0f)
            {
                exploration.EncounterTriggered = true;
                Debug.Log("[HSM] Exploration: a wild monster appears!");
            }
        }
    }
}
