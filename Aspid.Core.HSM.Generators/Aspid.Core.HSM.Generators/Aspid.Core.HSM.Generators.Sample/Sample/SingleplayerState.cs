namespace Aspid.Core.HSM.Generators.Sample.Sample;

[ControllerGroup]
public partial class SingleplayerState : IState
{
    public SingleplayerState()
    {
        AddControllers(new Sample.PlayerControllerGroup());
    }
}