using System;
using System.Collections.Generic;

namespace Aspid.Core.HSM.Sample
{
    public class SampleStateFactory : StateFactory<IState>
    {
        private readonly Dictionary<Type, Func<IState>> _creators;

        public SampleStateFactory(GameSession session, IGameActions actions)
        {
            _creators = new Dictionary<Type, Func<IState>>
            {
                // Root & flow
                [typeof(RootState)] = () => new RootState(),
                [typeof(LoadingState)] = () => new LoadingState(),
                [typeof(GameOverState)] = () => new GameOverState(session, actions),

                // Menu branch
                [typeof(MainMenuState)] = () => new MainMenuState(actions),
                [typeof(SettingsState)] = () => new SettingsState(session, actions),
                [typeof(CreditsState)] = () => new CreditsState(actions),

                // Gameplay branch
                [typeof(GameplayState)] = () => new GameplayState(),
                [typeof(PauseState)] = () => new PauseState(actions),

                // Single player branch
                [typeof(SinglePlayerState)] = () => new SinglePlayerState(),
                [typeof(ExplorationState)] = () => new ExplorationState(session, actions),
                [typeof(CombatState)] = () => new CombatState(session, actions),
                [typeof(DialogueState)] = () => new DialogueState(session),

                // Multiplayer branch
                [typeof(MultiplayerState)] = () => new MultiplayerState(),
                [typeof(LobbyState)] = () => new LobbyState(session, actions),
                [typeof(MatchState)] = () => new MatchState(session, actions),

                // Extensions
                [typeof(DebugOverlayExtension)] = () => new DebugOverlayExtension(session),
                [typeof(FpsCounterExtension)] = () => new FpsCounterExtension(),
                [typeof(SlowMotionExtension)] = () => new SlowMotionExtension(),
            };
        }

        protected override IState CreateStateInternal(Type type)
        {
            return _creators.TryGetValue(type, out var create)
                ? create()
                : throw new ArgumentException($"Unknown state type: {type.Name}");
        }
    }
}
