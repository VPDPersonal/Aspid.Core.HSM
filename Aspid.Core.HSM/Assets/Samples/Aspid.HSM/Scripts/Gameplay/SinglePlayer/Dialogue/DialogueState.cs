using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    [ControllerGroup]
    [ScopeLifetime(ScopeLifetime.Transient)]
    public partial class DialogueState : IState, IChildState<SinglePlayerState>
    {
        private readonly ConversationController _conversation;

        public DialogueState(GameSession session)
        {
            _conversation = new ConversationController(session);
            AddControllers(
                _conversation,
                new DialogueScreen(_conversation));
        }

        // Forwarded so SampleGameManager can poll the typed leaf state
        public bool IsFinished => _conversation.IsFinished;

        public void Enter()
        {
            Debug.Log("[HSM] DialogueState.Enter — conversation started");
        }

        public void Exit()
        {
            Debug.Log("[HSM] DialogueState.Exit");
        }
    }
}
