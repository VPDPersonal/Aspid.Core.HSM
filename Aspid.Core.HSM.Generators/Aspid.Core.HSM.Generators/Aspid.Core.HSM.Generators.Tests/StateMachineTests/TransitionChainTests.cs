using System;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

#region Test Transitions for Chain

public class ChildToSiblingSegmentTransition : ITransition<ChildTestState, SiblingChildTestState>
{
    public bool GuardResult { get; set; } = true;
    public int BeforeCount { get; private set; }
    public int AfterCount { get; private set; }

    public bool CanTransition() => GuardResult;
    public void OnBeforeTransition() => BeforeCount++;
    public void OnAfterTransition() => AfterCount++;
}

public class GrandchildToChildSegmentTransition : ITransition<GrandchildTestState, ChildTestState>
{
    public bool GuardResult { get; set; } = true;
    public int BeforeCount { get; private set; }
    public int AfterCount { get; private set; }

    public bool CanTransition() => GuardResult;
    public void OnBeforeTransition() => BeforeCount++;
    public void OnAfterTransition() => AfterCount++;
}

public class GrandchildToSiblingDirectTransition : ITransition<GrandchildTestState, SiblingChildTestState>
{
    public bool GuardResult { get; set; } = true;
    public int BeforeCount { get; private set; }
    public int AfterCount { get; private set; }

    public bool CanTransition() => GuardResult;
    public void OnBeforeTransition() => BeforeCount++;
    public void OnAfterTransition() => AfterCount++;
}

#endregion

public class TransitionChainTests
{
    private TestStateFactory CreateFactory()
    {
        var factory = new TestStateFactory();
        factory.RegisterState<ParentTestState>();
        factory.RegisterState<ChildTestState>();
        factory.RegisterState<SiblingChildTestState>();
        factory.RegisterState<GrandchildTestState>();
        return factory;
    }

    [Fact]
    public void Chain_fallback_fires_segment_transitions_along_path()
    {
        var factory = CreateFactory();
        var sm = new TestableStateMachine(factory);

        var segmentTransition = new ChildToSiblingSegmentTransition();
        sm.RegisterTransition(segmentTransition);

        // Current state: ChildTestState (chain: [ParentTestState, ChildTestState])
        sm.ChangeState<ChildTestState>();

        // No direct transition registered for (ChildTestState → SiblingChildTestState) as a leaf-to-leaf,
        // but a segment transition exists for this pair along the path
        sm.TransitionTo<SiblingChildTestState>();

        // Should have transitioned
        Assert.IsType<SiblingChildTestState>(sm.CurrentStates[^1]);

        // Segment hooks should have fired
        Assert.Equal(1, segmentTransition.BeforeCount);
        Assert.Equal(1, segmentTransition.AfterCount);
    }

    [Fact]
    public void Chain_fallback_aborts_if_any_segment_guard_fails()
    {
        var factory = CreateFactory();
        var sm = new TestableStateMachine(factory);

        var segmentTransition = new ChildToSiblingSegmentTransition { GuardResult = false };
        sm.RegisterTransition(segmentTransition);

        sm.ChangeState<ChildTestState>();
        sm.TransitionTo<SiblingChildTestState>();

        // State should NOT have changed
        Assert.IsType<ChildTestState>(sm.CurrentStates[^1]);

        // Hooks should NOT have fired
        Assert.Equal(0, segmentTransition.BeforeCount);
        Assert.Equal(0, segmentTransition.AfterCount);
    }

    [Fact]
    public void Direct_transition_takes_priority_over_chain()
    {
        var factory = CreateFactory();
        var sm = new TestableStateMachine(factory);

        // Register segment transitions along the path
        var grandchildToChild = new GrandchildToChildSegmentTransition();
        var childToSibling = new ChildToSiblingSegmentTransition();
        sm.RegisterTransition(grandchildToChild);
        sm.RegisterTransition(childToSibling);

        // Register a direct transition from leaf to target
        var directTransition = new GrandchildToSiblingDirectTransition();
        sm.RegisterTransition(directTransition);

        // Current state: GrandchildTestState
        // Chain: [ParentTestState, ChildTestState, GrandchildTestState]
        sm.ChangeState<GrandchildTestState>();

        // TransitionTo SiblingChildTestState — direct transition should take priority
        sm.TransitionTo<SiblingChildTestState>();

        // Should have transitioned
        Assert.IsType<SiblingChildTestState>(sm.CurrentStates[^1]);

        // Direct transition hooks should have fired
        Assert.Equal(1, directTransition.BeforeCount);
        Assert.Equal(1, directTransition.AfterCount);

        // Segment transitions should NOT have fired
        Assert.Equal(0, grandchildToChild.BeforeCount);
        Assert.Equal(0, grandchildToChild.AfterCount);
        Assert.Equal(0, childToSibling.BeforeCount);
        Assert.Equal(0, childToSibling.AfterCount);
    }
}
