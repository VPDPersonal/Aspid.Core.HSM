using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    [ControllerGroup]
    [ScopeLifetime(ScopeLifetime.Transient)]
    public partial class GameOverState : IState, IChildState<RootState>
    {
        private readonly GameSession _session;

        public GameOverState(GameSession session, IGameActions actions)
        {
            _session = session;
            AddControllers(new GameOverScreen(session, actions));
        }

        public void Enter()
        {
            Debug.Log($"[HSM] GameOverState.Enter — {_session.GameOverReason} | final score: {_session.Player.Score}");
        }

        public void Exit()
        {
            Debug.Log("[HSM] GameOverState.Exit");
        }
    }
}
