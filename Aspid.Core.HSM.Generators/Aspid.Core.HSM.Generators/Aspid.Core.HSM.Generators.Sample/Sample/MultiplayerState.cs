// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM.Generators.Sample;

[ControllerGroup]
public partial class MultiplayerState : IState
{
    public MultiplayerState()
    {
        AddControllers(new PlayerControllerGroup());
    }
}

// Generated
public partial class MultiplayerState : IController
{
    public void AddControllers(params IController[] controllers)
    {
        
    }
}
