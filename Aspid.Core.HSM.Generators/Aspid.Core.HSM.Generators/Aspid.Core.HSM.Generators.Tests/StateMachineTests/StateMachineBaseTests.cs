using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

public class StateMachineBaseTests
{
    #region Initialization Tests
    [Fact]
    public void Constructor_ShouldInitializeWithEmptyState()
    {
        var factory = new TestStateFactory();
        var stateMachine = new TestableStateMachine(factory);
        
        Assert.Single(stateMachine.CurrentStates);
        Assert.IsType<EmptyState>(stateMachine.CurrentStates[0]);
    }
    #endregion

    #region ChangeState Tests
    [Fact]
    public void ChangeState_ToSimpleState_ShouldEnterNewState()
    {
        var factory = new TestStateFactory();
        var testState = new SimpleTestState();
        factory.RegisterState(creator: () => testState);
        var stateMachine = new TestableStateMachine(factory);
        
        stateMachine.ChangeState<SimpleTestState>();
        
        Assert.True(testState.EnterCalled is 1);
        Assert.Contains(testState, stateMachine.CurrentStates);
    }

    [Fact]
    public void ChangeState_ToAnotherState_ShouldExitPreviousState()
    {
        var factory = new TestStateFactory();
        var firstState = new SimpleTestState();
        var secondState = new AnotherTestState();
        factory.RegisterState(creator: () => firstState);
        factory.RegisterState(creator: () => secondState);
        var stateMachine = new TestableStateMachine(factory);
        
        stateMachine.ChangeState<SimpleTestState>();
        stateMachine.ChangeState<AnotherTestState>();
        
        Assert.True(firstState.EnterCalled is 1);
        Assert.True(firstState.ExitCalled is 1);
        Assert.True(secondState.EnterCalled is 1);
        Assert.Contains(secondState, stateMachine.CurrentStates);
        Assert.DoesNotContain(firstState, stateMachine.CurrentStates);
    }

    [Fact]
    public void ChangeState_ShouldReleaseExitedState()
    {
        var factory = new TestStateFactory();
        var firstState = new SimpleTestState();
        var secondState = new AnotherTestState();
        factory.RegisterState(creator: () => firstState);
        factory.RegisterState(creator: () => secondState);
        var stateMachine = new TestableStateMachine(factory);
        
        stateMachine.ChangeState<SimpleTestState>();
        stateMachine.ChangeState<AnotherTestState>();
        
        Assert.Contains(firstState, factory.ReleasedStates);
    }

    [Fact]
    public void ChangeState_ShouldTriggerOnChangingAndOnChangedCallbacks()
    {
        var factory = new TestStateFactory();
        factory.RegisterState<SimpleTestState>();
        var stateMachine = new TestableStateMachine(factory);
        
        stateMachine.ChangeState<SimpleTestState>();
        
        Assert.Equal(1, stateMachine.ChangingStateCallCount);
        Assert.Equal(1, stateMachine.ChangedStateCallCount);
    }

    [Fact]
    public void ChangeState_ShouldTriggerOnEnteringAndOnEnteredCallbacks()
    {
        // Arrange
        var factory = new TestStateFactory();
        var testState = new SimpleTestState();
        factory.RegisterState(() => testState);
        var stateMachine = new TestableStateMachine(factory);
        
        stateMachine.ChangeState<SimpleTestState>();
        
        Assert.Equal(1, stateMachine.EnteringStateCallCount);
        Assert.Equal(1, stateMachine.EnteredStateCallCount);
        Assert.Same(testState, stateMachine.LastEnteringState);
        Assert.Same(testState, stateMachine.LastEnteredState);
    }

    [Fact]
    public void ChangeState_ShouldTriggerOnExitingAndOnExitedCallbacks()
    {
        var factory = new TestStateFactory();
        var firstState = new SimpleTestState();
        var secondState = new AnotherTestState();
        factory.RegisterState(creator: () => firstState);
        factory.RegisterState(creator: () => secondState);
        var stateMachine = new TestableStateMachine(factory);
        
        stateMachine.ChangeState<SimpleTestState>();
        stateMachine.ChangeState<AnotherTestState>();
        
        // EmptyState + firstState
        Assert.Equal(2, stateMachine.ExitingStateCallCount);
        Assert.Equal(2, stateMachine.ExitedStateCallCount);
        Assert.Same(firstState, stateMachine.LastExitingState);
        Assert.Same(firstState, stateMachine.LastExitedState);
    }
    #endregion

    #region Controller Tests
    [Fact]
    public void ChangeState_WithEnterController_ShouldCallOnEnter()
    {
        var factory = new TestStateFactory();
        var testState = new ControllableTestState();
        factory.RegisterState(creator: () => testState);
        var stateMachine = new TestableStateMachine(factory);
        
        stateMachine.ChangeState<ControllableTestState>();
        
        Assert.True(testState.EnterCalled == 1, "Enter() should be called");
        Assert.True(testState.OnEnterCalled == 1, "OnEnter() should be called");
    }

    [Fact]
    public void ChangeState_WithExitController_ShouldCallOnExit()
    {
        var factory = new TestStateFactory();
        var controllableState = new ControllableTestState();
        var anotherState = new AnotherTestState();
        factory.RegisterState(creator: () => controllableState);
        factory.RegisterState(creator: () => anotherState);
        var stateMachine = new TestableStateMachine(factory);
        
        stateMachine.ChangeState<ControllableTestState>();
        stateMachine.ChangeState<AnotherTestState>();
        
        Assert.True(controllableState.ExitCalled == 1, "Exit() should be called");
        Assert.True(controllableState.OnExitCalled == 1, "OnExit() should be called");
    }
    #endregion

    #region Update Tests
    [Fact]
    public void Update_WithUpdateController_ShouldCallUpdateOnCurrentState()
    {
        var factory = new TestStateFactory();
        var testState = new UpdateableTestState();
        factory.RegisterState(creator: () => testState);
        var stateMachine = new TestableStateMachine(factory);
        stateMachine.ChangeState<UpdateableTestState>();
        
        stateMachine.CallUpdate(deltaTime: 0.016f);
        
        Assert.Equal(1, testState.UpdateCallCount);
        Assert.Equal(0.016f, testState.LastDeltaTime);
    }

    [Fact]
    public void Update_MultipleCalls_ShouldCallUpdateEachTime()
    {
        var factory = new TestStateFactory();
        var testState = new UpdateableTestState();
        factory.RegisterState(creator: () => testState);
        var stateMachine = new TestableStateMachine(factory);
        
        stateMachine.ChangeState<UpdateableTestState>();
        stateMachine.CallUpdate(deltaTime: 0.016f);
        stateMachine.CallUpdate(deltaTime: 0.016f);
        stateMachine.CallUpdate(deltaTime: 0.016f);
        
        Assert.Equal(3, testState.UpdateCallCount);
    }

    [Fact]
    public void LateUpdate_WithLateUpdateController_ShouldCallLateUpdateOnCurrentState()
    {
        var factory = new TestStateFactory();
        var testState = new FullUpdateTestState();
        factory.RegisterState(creator: () => testState);
        var stateMachine = new TestableStateMachine(factory);
        stateMachine.ChangeState<FullUpdateTestState>();
        
        stateMachine.CallLateUpdate(deltaTime: 0.016f);
        
        Assert.Equal(1, testState.LateUpdateCallCount);
    }

    [Fact]
    public void FixedUpdate_WithFixedUpdateController_ShouldCallFixedUpdateOnCurrentState()
    {
        var factory = new TestStateFactory();
        var testState = new FullUpdateTestState();
        factory.RegisterState(creator:() => testState);
        var stateMachine = new TestableStateMachine(factory);
        
        stateMachine.ChangeState<FullUpdateTestState>();
        stateMachine.CallFixedUpdate(deltaTime: 0.02f);
        
        Assert.Equal(1, testState.FixedUpdateCallCount);
    }

    [Fact]
    public void Update_WithStateWithoutUpdateController_ShouldNotThrow()
    {
        var factory = new TestStateFactory();
        var testState = new SimpleTestState();
        factory.RegisterState(creator: () => testState);
        var stateMachine = new TestableStateMachine(factory);
        
        stateMachine.ChangeState<SimpleTestState>();
        var exception = Record.Exception(() => stateMachine.CallUpdate(deltaTime: 0.016f));
        
        Assert.Null(exception);
    }
    #endregion

    #region Dispose Tests
    [Fact]
    public void Dispose_WithDisposableController_ShouldCallDisposeOnCurrentState()
    {
        var factory = new TestStateFactory();
        var testState = new DisposableTestState();
        factory.RegisterState(creator: () => testState);
        var stateMachine = new TestableStateMachine(factory);
        
        stateMachine.ChangeState<DisposableTestState>();
        stateMachine.Dispose();
        
        Assert.True(testState.DisposeCalled);
    }

    [Fact]
    public void Dispose_ShouldTriggerDisposedAndDisposingCallbacks()
    {
        var factory = new TestStateFactory();
        factory.RegisterState<SimpleTestState>();
        var stateMachine = new TestableStateMachine(factory);
        
        stateMachine.ChangeState<SimpleTestState>();
        stateMachine.Dispose();
        
        Assert.Equal(1, stateMachine.DisposedCallCount);
        Assert.Equal(1, stateMachine.DisposingCallCount);
    }
    #endregion

    #region Hierarchical State Tests
    [Fact]
    public void ChangeState_ToChildState_ShouldEnterParentAndChildStates()
    {
        var factory = new TestStateFactory();
        var parentState = new ParentTestState();
        var childState = new ChildTestState();
        factory.RegisterState(creator: () => parentState);
        factory.RegisterState(creator: () => childState);
        var stateMachine = new TestableStateMachine(factory);
        
        stateMachine.ChangeState<ChildTestState>();
        
        Assert.True(parentState.EnterCalled is 1);
        Assert.True(childState.EnterCalled is 1);
        // Parent + Child (EmptyState is replaced)
        Assert.Equal(2, stateMachine.CurrentStates.Count);
        Assert.Contains(parentState, stateMachine.CurrentStates);
        Assert.Contains(childState, stateMachine.CurrentStates);
    }

    [Fact]
    public void ChangeState_FromChildToSiblingChild_ShouldOnlyExitChild()
    {
        var factory = new TestStateFactory();
        var parentState = new ParentTestState();
        var childState = new ChildTestState();
        var siblingState = new SiblingChildTestState();
        factory.RegisterState(creator: () => parentState);
        factory.RegisterState(creator: () => childState);
        factory.RegisterState(creator: () => siblingState);
        var stateMachine = new TestableStateMachine(factory);
        
        stateMachine.ChangeState<ChildTestState>();
        factory.ClearReleasedStates();
        stateMachine.ChangeState<SiblingChildTestState>();
        
        Assert.True(childState.ExitCalled is 1);
        // Parent should not exit
        Assert.False(parentState.ExitCalled is 1);
        Assert.True(siblingState.EnterCalled is 1);
        Assert.Single(factory.ReleasedStates);
        Assert.Contains(childState, factory.ReleasedStates);
    }

    [Fact]
    public void ChangeState_ToGrandchildState_ShouldEnterAllAncestors()
    {
        var factory = new TestStateFactory();
        var parentState = new ParentTestState();
        var childState = new ChildTestState();
        var grandchildState = new GrandchildTestState();
        factory.RegisterState(creator: () => parentState);
        factory.RegisterState(creator: () => childState);
        factory.RegisterState(creator: () => grandchildState);
        var stateMachine = new TestableStateMachine(factory);
        
        stateMachine.ChangeState<GrandchildTestState>();

        Assert.True(parentState.EnterCalled is 1);
        Assert.True(childState.EnterCalled is 1);
        Assert.True(grandchildState.EnterCalled is 1);
        // Parent + Child + Grandchild (EmptyState is replaced)
        Assert.Equal(3, stateMachine.CurrentStates.Count);
    }

    [Fact]
    public void ChangeState_FromGrandchildToAnotherRoot_ShouldExitAllStates()
    {
        var factory = new TestStateFactory();
        var parentState = new ParentTestState();
        var childState = new ChildTestState();
        var grandchildState = new GrandchildTestState();
        var simpleState = new SimpleTestState();
        factory.RegisterState(creator: () => parentState);
        factory.RegisterState(creator: () => childState);
        factory.RegisterState(creator: () => grandchildState);
        factory.RegisterState(creator: () => simpleState);
        var stateMachine = new TestableStateMachine(factory);
        
        stateMachine.ChangeState<GrandchildTestState>();
        stateMachine.ChangeState<SimpleTestState>();
        
        Assert.True(grandchildState.ExitCalled is 1);
        Assert.True(childState.ExitCalled is 1);
        Assert.True(parentState.ExitCalled is 1);
        Assert.True(simpleState.EnterCalled is 1);
        // Only the Simple state remains
        Assert.Single(stateMachine.CurrentStates); 
    }

    [Fact]
    public void ChangeState_ToSameState_ShouldNotReenter()
    {
        var factory = new TestStateFactory();
        var sameState = new SimpleTestState();
        factory.RegisterState(creator: () => sameState);
        var stateMachine = new TestableStateMachine(factory);
        
        stateMachine.ChangeState<SimpleTestState>();
        // Reset enter/exit flags
        sameState.Reset();
        factory.ClearReleasedStates();
        stateMachine.ChangeState<SimpleTestState>();
        
        Assert.False(sameState.EnterCalled is 1);
        Assert.False(sameState.ExitCalled is 1);
    }
    #endregion
}
