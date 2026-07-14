using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    [ControllerGroup]
    [ScopeLifetime(ScopeLifetime.Transient)]
    public partial class CreditsState : IState, IChildState<MainMenuState>
    {
        private readonly CreditsRollController _roll;

        public CreditsState(IGameActions actions)
        {
            _roll = new CreditsRollController();
            AddControllers(
                _roll,
                new CreditsScreen(_roll, actions));
        }

        // Forwarded so SampleGameManager can poll the typed leaf state
        public bool IsFinished => _roll.IsFinished;

        public void Enter()
        {
            Debug.Log("[HSM] CreditsState.Enter — rolling credits");
        }

        public void Exit()
        {
            Debug.Log("[HSM] CreditsState.Exit");
        }
    }
}
