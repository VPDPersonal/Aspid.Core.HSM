using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

#region Test Helpers

public class InnerGroupController : IEnterController, IUpdateController
{
    public bool OnEnterCalled { get; private set; }

    public int UpdateCallCount { get; private set; }

    public float LastDeltaTime { get; private set; }

    public void OnEnter() => OnEnterCalled = true;

    public void Update(float deltaTime)
    {
        UpdateCallCount++;
        LastDeltaTime = deltaTime;
    }
}

public class OuterGroupState : BaseTestState, IEnterController, IUpdateController
{
    public InnerGroupController Inner { get; } = new();

    public void OnEnter() => Inner.OnEnter();

    public void Update(float deltaTime) => Inner.Update(deltaTime);
}

#endregion

public class NestedControllerGroupTests
{
    #region Update Tests

    [Fact]
    public void Update_dispatches_through_nested_controller_group()
    {
        var factory = new TestStateFactory();
        var state = new OuterGroupState();
        factory.RegisterState(creator: () => state);
        var stateMachine = new TestableStateMachine(factory);

        stateMachine.ChangeState<OuterGroupState>();
        stateMachine.CallUpdate(deltaTime: 0.016f);

        Assert.Equal(1, state.Inner.UpdateCallCount);
        Assert.Equal(0.016f, state.Inner.LastDeltaTime);
    }

    #endregion

    #region Enter Tests

    [Fact]
    public void Enter_dispatches_through_nested_controller_group()
    {
        var factory = new TestStateFactory();
        var state = new OuterGroupState();
        factory.RegisterState(creator: () => state);
        var stateMachine = new TestableStateMachine(factory);

        stateMachine.ChangeState<OuterGroupState>();

        Assert.True(state.Inner.OnEnterCalled);
    }

    #endregion

    #region Multiple Updates Tests

    [Fact]
    public void Multiple_updates_dispatch_correctly_through_nesting()
    {
        var factory = new TestStateFactory();
        var state = new OuterGroupState();
        factory.RegisterState(creator: () => state);
        var stateMachine = new TestableStateMachine(factory);

        stateMachine.ChangeState<OuterGroupState>();
        stateMachine.CallUpdate(deltaTime: 0.016f);
        stateMachine.CallUpdate(deltaTime: 0.032f);
        stateMachine.CallUpdate(deltaTime: 0.048f);

        Assert.Equal(3, state.Inner.UpdateCallCount);
        Assert.Equal(0.048f, state.Inner.LastDeltaTime);
    }

    #endregion
}
