using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    public class CombatLogController : IEnterController, IExitController
    {
        private readonly GameSession _session;

        public CombatLogController(GameSession session) => _session = session;

        public void OnEnter()
        {
            _session.Combat.Log = "Battle started! Press Space to attack";
            Debug.Log("[HSM] CombatLog: battle log opened");
        }

        public void OnExit()
        {
            Debug.Log($"[HSM] CombatLog: battle log closed (last entry: \"{_session.Combat.Log}\")");
        }
    }
}
