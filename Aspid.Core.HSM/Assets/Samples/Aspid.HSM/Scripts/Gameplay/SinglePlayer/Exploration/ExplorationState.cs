using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    // The state is a pure composition point: behavior lives in the controllers,
    // dispatched in registration order (movement writes IsMoving, encounter reads it;
    // the screen draws first, the HUD on top).
    [ControllerGroup]
    public partial class ExplorationState : IState, IChildState<SinglePlayerState>
    {
        public ExplorationState(GameSession session, IGameActions actions)
        {
            AddControllers(
                new PlayerMovementController(session),
                new WildEncounterController(session),
                new NpcController(session),
                new ExplorationScreen(session),
                new GameHUDController(session, actions));
        }

        public void Enter()
        {
            Debug.Log("[HSM] ExplorationState.Enter — free roam");
        }

        public void Exit()
        {
            Debug.Log("[HSM] ExplorationState.Exit");
        }
    }
}
