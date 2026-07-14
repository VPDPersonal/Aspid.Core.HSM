using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    [ControllerGroup]
    [ScopeLifetime(ScopeLifetime.Transient)]
    public partial class MatchState : IState, IChildState<MultiplayerState>
    {
        public MatchState(GameSession session, IGameActions actions)
        {
            // GUI controllers draw in registration order: screen first, HUD on top of it
            AddControllers(
                new MatchTimerController(session),
                new MatchScreen(session.Match),
                new GameHUDController(session, actions));
        }

        public void Enter()
        {
            Debug.Log("[HSM] MatchState.Enter — match in progress");
        }

        public void Exit()
        {
            Debug.Log("[HSM] MatchState.Exit — leaving match");
        }
    }
}
