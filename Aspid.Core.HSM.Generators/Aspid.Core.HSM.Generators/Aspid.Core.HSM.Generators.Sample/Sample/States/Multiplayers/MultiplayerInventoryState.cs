namespace Aspid.Core.HSM.Generators.Sample.Sample;

[ControllerGroup]
public partial class MultiplayerInventoryState : IState, IChildState<MultiplayerState>
{
    public MultiplayerInventoryState()
    {
        AddControllers(new SomeUpdateController());
    }
}
