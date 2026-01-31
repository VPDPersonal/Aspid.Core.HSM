namespace Aspid.Core.HSM.Generators.Sample.Sample;

[ControllerGroup]
public partial class PlayerController : IUpdateController, IEnterController
{
    public PlayerController()
    {
        AddControllers(
            new SomeUpdateController(),
            new SomeUpdateController());
    }
}
