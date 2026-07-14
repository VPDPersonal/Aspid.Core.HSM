using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    [ControllerGroup]
    [ScopeLifetime(ScopeLifetime.Transient)]
    public partial class CombatState : IState, IChildState<SinglePlayerState>
    {
        public CombatState(GameSession session, IGameActions actions)
        {
            // Registration order matters twice: OnEnter runs setup before AI/log,
            // and GUI controllers draw in order — screen first, HUD on top of it.
            AddControllers(
                new BattleSetupController(session),
                new PlayerCombatController(session),
                new EnemyAIController(session),
                new HitFlashController(session),
                new CombatLogController(session),
                new CombatScreen(session, actions),
                new GameHUDController(session, actions));
        }

        public void Enter()
        {
            Debug.Log("[HSM] CombatState.Enter — battle begins");
        }

        public void Exit()
        {
            Debug.Log("[HSM] CombatState.Exit — battle over");
        }
    }
}
