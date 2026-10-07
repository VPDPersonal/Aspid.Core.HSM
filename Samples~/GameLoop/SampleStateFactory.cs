using System;
using Aspid.Core.HSM;
using Aspid.Core.HSM.Samples.GameLoop.Extensions;
using Aspid.Core.HSM.Samples.GameLoop.States;

namespace Aspid.Core.HSM.Samples.GameLoop
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
