namespace Aspid.Core.HSM.Generators.Sample.Sample;

[ControllerGroup]
public partial class MultiplayerState : IState
{
    public MultiplayerState()
    {
        AddControllers(new PlayerControllerGroup());
    }
}