using System.Collections.Generic;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

#region Helpers

public abstract class TickOrderState : IState, IUpdateController, ILateUpdateController, IFixedUpdateController
{
    public List<string>? Log { get; set; }

    public void Update(float deltaTime) => Log?.Add("update " + GetType().Name);

    public void LateUpdate(float deltaTime) => Log?.Add("late " + GetType().Name);

    public void FixedUpdate(float deltaTime) => Log?.Add("fixed " + GetType().Name);
}

public sealed class TickRoot : TickOrderState;

public sealed class TickMiddle : TickOrderState, IChildState<TickRoot>;

public sealed class TickLeaf : TickOrderState, IChildState<TickMiddle>;

#endregion

/// <summary>
/// Contract: every tick visits the active chain from the root to the leaf, so a parent's result of the frame is ready
/// before its children read it.
/// </summary>
public class TickOrderTests
{
    private readonly List<string> _log = new();
    private readonly TestableStateMachine _sm;

    public TickOrderTests()
    {
        var factory = new TestStateFactory();
        factory.RegisterState(() => new TickRoot { Log = _log });
        factory.RegisterState(() => new TickMiddle { Log = _log });
        factory.RegisterState(() => new TickLeaf { Log = _log });
        _sm = new TestableStateMachine(factory);
        _sm.ChangeState<TickLeaf>();
    }

    [Fact]
    public void Update_TicksActiveChainFromRootToLeaf()
    {
        _sm.CallUpdate(0.016f);

        Assert.Equal(new[] { "update TickRoot", "update TickMiddle", "update TickLeaf" }, _log);
    }

    [Fact]
    public void LateUpdate_TicksActiveChainFromRootToLeaf()
    {
        _sm.CallLateUpdate(0.016f);

        Assert.Equal(new[] { "late TickRoot", "late TickMiddle", "late TickLeaf" }, _log);
    }

    [Fact]
    public void FixedUpdate_TicksActiveChainFromRootToLeaf()
    {
        _sm.CallFixedUpdate(0.02f);

        Assert.Equal(new[] { "fixed TickRoot", "fixed TickMiddle", "fixed TickLeaf" }, _log);
    }
}
