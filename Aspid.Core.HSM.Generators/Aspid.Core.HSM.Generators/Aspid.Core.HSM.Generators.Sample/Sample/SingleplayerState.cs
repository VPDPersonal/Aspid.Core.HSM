// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM.Generators.Sample;

[ControllerGroup]
public partial class SingleplayerState : IState
{
    public SingleplayerState()
    {
        AddControllers(new PlayerControllerGroup());
    }
}

// Generated
public partial class SingleplayerState : IController
{
    public void AddControllers(params IController[] controllers)
    {
        
    }
}