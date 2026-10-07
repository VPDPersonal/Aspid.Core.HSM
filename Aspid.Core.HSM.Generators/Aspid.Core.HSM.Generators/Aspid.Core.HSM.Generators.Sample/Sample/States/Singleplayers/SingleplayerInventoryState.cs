namespace Aspid.Core.HSM.Generators.Sample.Sample;

[ControllerGroup]
public partial class SingleplayerInventoryState : IState, IChildState<SingleplayerState>
{
    public SingleplayerInventoryState()
    {
        AddControllers();
    }
}