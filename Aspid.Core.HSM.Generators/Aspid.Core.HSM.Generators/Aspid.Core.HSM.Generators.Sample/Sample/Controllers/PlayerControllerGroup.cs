namespace Aspid.Core.HSM.Generators.Sample.Sample;

[ControllerGroup]
public partial class PlayerControllerGroup : IUpdateController
{
    public PlayerControllerGroup()
    {
        AddControllers(new SomeUpdateController());
    }
}