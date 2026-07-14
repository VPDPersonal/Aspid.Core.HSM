using UnityEngine;
using UnityEngine.InputSystem;

namespace Aspid.Core.HSM.Sample
{
    // One controller on two dispatch loops: the wander simulation ticks on the fixed
    // timestep (IFixedUpdateController), interaction polling runs per frame (IUpdateController).
    public class NpcController : IUpdateController, IFixedUpdateController, IEnterController
    {
        private const float TalkDistance = 0.09f;
        private const float WanderRadius = 0.04f;
        private static readonly Vector2 Anchor = new(0.75f, 0.35f);

        private readonly GameSession _session;
        private float _wanderAngle;

        public NpcController(GameSession session) => _session = session;

        public void OnEnter()
        {
            _session.Exploration.DialogueRequested = false;
        }

        public void FixedUpdate(float deltaTime)
        {
            _wanderAngle += deltaTime * 0.6f;
            _session.Exploration.NpcPos = Anchor +
                new Vector2(Mathf.Cos(_wanderAngle), Mathf.Sin(_wanderAngle)) * WanderRadius;
        }

        public void Update(float deltaTime)
        {
            var exploration = _session.Exploration;
            exploration.NearNpc = Vector2.Distance(exploration.PlayerPos, exploration.NpcPos) < TalkDistance;

            var keyboard = Keyboard.current;
            if (exploration.NearNpc && keyboard != null && keyboard.eKey.wasPressedThisFrame)
                exploration.DialogueRequested = true;
        }
    }
}
