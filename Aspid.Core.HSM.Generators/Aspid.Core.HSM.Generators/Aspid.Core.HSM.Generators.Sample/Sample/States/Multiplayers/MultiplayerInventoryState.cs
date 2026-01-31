using System;

namespace Aspid.Core.HSM.Generators.Sample.Sample;

[ControllerGroup]
[ParentState(typeof(MultiplayerState))]
public partial class MultiplayerInventoryState : IState
{
    public MultiplayerInventoryState()
    {
        AddControllers(new SomeUpdateController());
    }
}
