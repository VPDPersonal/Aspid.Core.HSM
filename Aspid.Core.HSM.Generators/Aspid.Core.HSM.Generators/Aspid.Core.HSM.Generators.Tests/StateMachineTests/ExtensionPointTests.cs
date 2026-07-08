using System;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

public class ConfigurableStateMachine : StateMachineBase
{
    public Func<IController, IState, bool>? ControllerEnabledCheck { get; set; }
    public Func<Type, bool>? StateEnabledCheck { get; set; }

    public ConfigurableStateMachine(StateFactory factory) : base(factory) { }

    public void CallUpdate(float dt) => Update(dt);

    protected override bool IsControllerEnabled(IController controller, IState state)
        => ControllerEnabledCheck?.Invoke(controller, state) ?? true;

    protected override bool IsStateEnabled(Type stateType)
        => StateEnabledCheck?.Invoke(stateType) ?? true;
}

public class ExtensionPointTests
{
    [Fact]
    public void IsControllerEnabled_false_skips_update_dispatch()
    {
        var factory = new TestStateFactory();
        var testState = new UpdateableTestState();
        factory.RegisterState(creator: () => testState);
        var sm = new ConfigurableStateMachine(factory);

        sm.ChangeState<UpdateableTestState>();
        sm.ControllerEnabledCheck = (_, _) => false;

        sm.CallUpdate(0.016f);

        Assert.Equal(0, testState.UpdateCallCount);
    }

    [Fact]
    public void IsStateEnabled_false_prevents_state_change()
    {
        var factory = new TestStateFactory();
        var firstState = new SimpleTestState();
        var blockedState = new AnotherTestState();
        factory.RegisterState(creator: () => firstState);
        factory.RegisterState(creator: () => blockedState);
        var sm = new ConfigurableStateMachine(factory);

        sm.ChangeState<SimpleTestState>();
        sm.StateEnabledCheck = type => type != typeof(AnotherTestState);

        sm.ChangeState<AnotherTestState>();

        Assert.Contains(firstState, sm.CurrentStates);
        Assert.DoesNotContain(blockedState, sm.CurrentStates);
        Assert.Equal(0, blockedState.EnterCalled);
    }
}
