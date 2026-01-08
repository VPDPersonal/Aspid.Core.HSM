using System;

namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

public class BaseTestState : IState
{
    public int EnterCalled { get; private set; }
    public int ExitCalled { get; private set; }

    public void Enter() => EnterCalled++;
    public void Exit() => ExitCalled++;
    
    public virtual void Reset()
    {
        EnterCalled = 0;
        ExitCalled = 0;
    }
}

#region Simple States
public class SimpleTestState : BaseTestState { }

public class AnotherTestState : BaseTestState { }
#endregion

#region States with Controllers
public class UpdateableTestState : BaseTestState, IUpdateController
{
    public float LastDeltaTime { get; private set; }
    
    public int UpdateCallCount { get; private set; }

    public void Update(float deltaTime)
    {
        LastDeltaTime = deltaTime;
        UpdateCallCount++;
    }

    public override void Reset()
    {
        LastDeltaTime = 0;
        UpdateCallCount = 0;
        
        base.Reset();
    }
}

public class ControllableTestState : BaseTestState, IEnterController, IExitController
{
    public int OnEnterCalled { get; private set; }
    
    public int OnExitCalled { get; private set; }

    public void OnEnter() => OnEnterCalled++;
    
    public void OnExit() => OnExitCalled++;
    
    public override void Reset()
    {
        OnEnterCalled = 0;
        OnExitCalled = 0;
        
        base.Reset();
    }
}

public class FullUpdateTestState : BaseTestState, IUpdateController, ILateUpdateController, IFixedUpdateController
{
    public int UpdateCallCount { get; private set; }
    
    public int LateUpdateCallCount { get; private set; }
    
    public int FixedUpdateCallCount { get; private set; }

    public void Update(float deltaTime) => UpdateCallCount++;
    
    public void LateUpdate(float deltaTime) => LateUpdateCallCount++;
    
    public void FixedUpdate(float deltaTime) => FixedUpdateCallCount++;
    
    public override void Reset()
    {
        UpdateCallCount = 0;
        LateUpdateCallCount = 0;
        FixedUpdateCallCount = 0;
        
        base.Reset();
    }
}

public class DisposableTestState : BaseTestState, IDisposableController
{
    public bool DisposeCalled { get; private set; }

    public void Dispose() => DisposeCalled = true;

    public override void Reset()
    {
        DisposeCalled = false;
        base.Reset();
    }
}
#endregion

#region Hierarchical States
public class ParentTestState : BaseTestState { }

public class ChildTestState : BaseTestState, IChildState
{
    public Type ParentState => typeof(ParentTestState);
}

public class SiblingChildTestState : BaseTestState, IChildState
{
    public Type ParentState => typeof(ParentTestState);
}

public class GrandchildTestState : BaseTestState, IChildState
{
    public Type ParentState => typeof(ChildTestState);
}
#endregion
