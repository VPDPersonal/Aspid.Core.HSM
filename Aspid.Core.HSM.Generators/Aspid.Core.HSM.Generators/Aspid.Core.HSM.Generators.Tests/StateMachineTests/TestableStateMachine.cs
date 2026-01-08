namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

/// <summary>
/// Testable version of StateMachineBase that exposes protected update methods.
/// </summary>
public class TestableStateMachine(StateFactory stateFactory)
    : StateMachineBase(stateFactory)
{
    public int ChangingStateCallCount { get; private set; }
    
    public int ChangedStateCallCount { get; private set; }
    
    public int EnteringStateCallCount { get; private set; }
    
    public int EnteredStateCallCount { get; private set; }
    
    public int ExitingStateCallCount { get; private set; }
    
    public int ExitedStateCallCount { get; private set; }
    
    public int DisposedCallCount { get; private set; }
    
    public int DisposingCallCount { get; private set; }

    public IState? LastEnteringState { get; private set; }
    
    public IState? LastEnteredState { get; private set; }
    
    public IState? LastExitingState { get; private set; }
    
    public IState? LastExitedState { get; private set; }

    public void CallUpdate(float deltaTime) => Update(deltaTime);
    
    public void CallLateUpdate(float deltaTime) => LateUpdate(deltaTime);
    
    public void CallFixedUpdate(float deltaTime) => FixedUpdate(deltaTime);

    protected override void OnChangingState() => ChangingStateCallCount++;

    protected override void OnChangedState() => ChangedStateCallCount++;

    protected override void OnEnteringState(IState state)
    {
        EnteringStateCallCount++;
        LastEnteringState = state;
    }

    protected override void OnEnteredState(IState state)
    {
        EnteredStateCallCount++;
        LastEnteredState = state;
    }

    protected override void OnExitingState(IState state)
    {
        ExitingStateCallCount++;
        LastExitingState = state;
    }

    protected override void OnExitedState(IState state)
    {
        ExitedStateCallCount++;
        LastExitedState = state;
    }

    protected override void Disposed() =>
        DisposedCallCount++;

    protected override void Disposing() =>
        DisposingCallCount++;
}

