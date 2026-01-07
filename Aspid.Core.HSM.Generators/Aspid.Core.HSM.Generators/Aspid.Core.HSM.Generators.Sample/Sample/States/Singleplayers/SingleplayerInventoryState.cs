namespace Aspid.Core.HSM.Generators.Sample.Sample;

[ControllerGroup]
[ParentState(typeof(SingleplayerState))]
public partial class SingleplayerInventoryState : IState
{
    public SingleplayerInventoryState()
    {
        AddControllers();
    }
}