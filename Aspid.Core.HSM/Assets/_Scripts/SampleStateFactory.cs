using System;
using _Scripts.Extensions;
using _Scripts.States;
using Aspid.Core.HSM;

namespace _Scripts
{
    public class SampleStateFactory : StateFactory<IState>
    {
        protected override IState CreateStateInternal(Type type)
        {
            if (type == typeof(RootState)) return new RootState();
            if (type == typeof(MainMenuState)) return new MainMenuState();
            if (type == typeof(LoadingState)) return new LoadingState();
            if (type == typeof(GameplayState)) return new GameplayState();
            if (type == typeof(SinglePlayerState)) return new SinglePlayerState();
            if (type == typeof(PauseState)) return new PauseState();
            if (type == typeof(DebugOverlayExtension)) return new DebugOverlayExtension();
            if (type == typeof(FpsCounterExtension)) return new FpsCounterExtension();

            throw new ArgumentException($"Unknown state type: {type.Name}");
        }
    }
}
