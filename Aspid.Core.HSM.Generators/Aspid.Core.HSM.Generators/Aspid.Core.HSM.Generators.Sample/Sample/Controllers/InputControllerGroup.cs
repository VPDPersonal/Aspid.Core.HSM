namespace Aspid.Core.HSM.Generators.Sample.Sample;

[ControllerGroup]
public partial class InputControllerGroup : IEnterController, IUpdateController
{
    public InputControllerGroup()
    {
        AddControllers(new SomeUpdateController(), new SomeUpdateController());
    }
}
