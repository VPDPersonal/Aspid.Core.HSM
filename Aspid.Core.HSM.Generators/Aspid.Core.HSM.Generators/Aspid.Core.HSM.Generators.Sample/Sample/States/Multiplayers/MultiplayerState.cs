namespace Aspid.Core.HSM.Generators.Sample.Sample;

[ControllerGroup]
public partial class MultiplayerState : IState
{
    public MultiplayerState()
    {
        AddControllers(
            new PlayerController(),
            new SomeUpdateController(),
            new InputControllerGroup());
    }

    public void Enter()
    {
        // Не обязательно определять этот метод.
    }

    public void Exit()
    {
        // Не обязательно определять этот метод.
    }
}