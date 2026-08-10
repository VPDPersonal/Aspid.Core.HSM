using System;
using System.Linq;
using System.Collections.Generic;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

#region Helpers

/// <summary>Root state that redirects the machine exactly once, from inside its own OnEnter.</summary>
public sealed class RedirectRootState : BaseTestState, IEnterController
{
    public Action? RedirectOnce { get; set; }

    public void OnEnter()
    {
        var redirect = RedirectOnce;
        RedirectOnce = null;
        redirect?.Invoke();
    }
}

public sealed class RedirectChildState : BaseTestState, IChildState<RedirectRootState> { }

public sealed class RedirectLeafState : BaseTestState, IChildState<RedirectChildState> { }

public sealed class RedirectAltState : BaseTestState, IChildState<RedirectRootState> { }

/// <summary>State machine exposing the edge-level guard and strict mode for assertions.</summary>
public sealed class GuardedStateMachine(StateFactory stateFactory) : StateMachineBase(stateFactory)
{
    public Func<Type, Type, bool>? EdgeGuard { get; set; }

    public bool Strict { get; set; }

    public List<(Type Source, Type Target)> ObservedEdges { get; } = [];

    protected override bool StrictTransitions => Strict;

    protected override bool IsTransitionEnabled(Type sourceType, Type targetType)
    {
        ObservedEdges.Add((sourceType, targetType));
        return EdgeGuard?.Invoke(sourceType, targetType) ?? true;
    }
}

#endregion

public class ReentrancyAndGuardsTests
{
    private static TestStateFactory CreateHierarchyFactory()
    {
        var factory = new TestStateFactory();
        factory.RegisterState<ParentTestState>();
        factory.RegisterState<ChildTestState>();
        factory.RegisterState<SiblingChildTestState>();
        factory.RegisterState<GrandchildTestState>();
        return factory;
    }

    // The factory used to hand out its own chain buffer, so the previous result silently
    // became the next result. Callers may now hold a chain across further factory calls.
    [Fact]
    public void CreateState_result_is_not_invalidated_by_a_later_call()
    {
        var factory = CreateHierarchyFactory();
        factory.RegisterState<SimpleTestStateForFactory>();
        var empty = Array.Empty<IState>();

        // A two-state chain first, then a one-state chain: a shared buffer would shrink `first` underneath us.
        var first = factory.CreateState<ChildTestState>(empty);
        var snapshot = first.ToArray();

        factory.CreateState<SimpleTestStateForFactory>(empty);

        Assert.Equal(snapshot.Length, first.Count);
        Assert.Equal(snapshot, first.ToArray());
    }

    // A ChangeState issued from OnEnter used to rewrite the chain the outer loop was still
    // iterating, entering states twice and leaving duplicates in CurrentStates. It is now
    // queued and applied once the running change completes (run-to-completion).
    [Fact]
    public void Reentrant_ChangeState_from_OnEnter_does_not_corrupt_the_chain()
    {
        var root = new RedirectRootState();
        var child = new RedirectChildState();
        var leaf = new RedirectLeafState();
        var alt = new RedirectAltState();

        var factory = new TestStateFactory();
        factory.RegisterState<RedirectRootState>(() => root);
        factory.RegisterState<RedirectChildState>(() => child);
        factory.RegisterState<RedirectLeafState>(() => leaf);
        factory.RegisterState<RedirectAltState>(() => alt);

        var sm = new TestableStateMachine(factory);
        root.RedirectOnce = () => sm.ChangeState<RedirectAltState>();

        sm.ChangeState<RedirectLeafState>();

        // No state appears twice, and the queued request produced the final chain.
        Assert.Equal(sm.CurrentStates.Count, sm.CurrentStates.Distinct().Count());
        Assert.Equal(new IState[] { root, alt }, sm.CurrentStates.ToArray());

        // The interrupted change still ran to completion before the queued one was applied.
        Assert.Equal(1, child.EnterCalled);
        Assert.Equal(1, child.ExitCalled);
        Assert.Equal(1, leaf.EnterCalled);
        Assert.Equal(1, leaf.ExitCalled);

        // Each state was entered exactly once — no double enter from a rewritten chain.
        Assert.Equal(1, root.EnterCalled);
        Assert.Equal(1, alt.EnterCalled);
        Assert.Equal(0, alt.ExitCalled);
    }

    [Fact]
    public void IsTransitionEnabled_blocks_a_denied_edge()
    {
        var factory = new TestStateFactory();
        factory.RegisterState<SimpleTestState>();
        factory.RegisterState<AnotherTestState>();

        var sm = new GuardedStateMachine(factory);
        sm.ChangeState<SimpleTestState>();

        sm.EdgeGuard = (source, target) =>
            !(source == typeof(SimpleTestState) && target == typeof(AnotherTestState));

        sm.ChangeState<AnotherTestState>();

        Assert.IsType<SimpleTestState>(sm.CurrentStates[^1]);
    }

    [Fact]
    public void IsTransitionEnabled_receives_the_source_leaf_and_the_target()
    {
        var factory = new TestStateFactory();
        factory.RegisterState<SimpleTestState>();
        factory.RegisterState<AnotherTestState>();

        var sm = new GuardedStateMachine(factory);
        sm.ChangeState<SimpleTestState>();
        sm.ObservedEdges.Clear();

        sm.ChangeState<AnotherTestState>();

        Assert.Contains((typeof(SimpleTestState), typeof(AnotherTestState)), sm.ObservedEdges);
    }

    [Fact]
    public void StrictTransitions_throws_for_an_unregistered_edge()
    {
        var sm = new GuardedStateMachine(CreateHierarchyFactory());
        sm.ChangeState<ChildTestState>();
        sm.Strict = true;

        var exception = Assert.Throws<InvalidOperationException>(
            () => sm.TransitionTo<SiblingChildTestState>());

        Assert.Contains(nameof(ChildTestState), exception.Message);
        Assert.Contains(nameof(SiblingChildTestState), exception.Message);
        Assert.IsType<ChildTestState>(sm.CurrentStates[^1]);
    }

    [Fact]
    public void StrictTransitions_allows_a_fully_registered_path()
    {
        var sm = new GuardedStateMachine(CreateHierarchyFactory());
        sm.RegisterTransition(new ChildToSiblingSegmentTransition());
        sm.ChangeState<ChildTestState>();
        sm.Strict = true;

        sm.TransitionTo<SiblingChildTestState>();

        Assert.IsType<SiblingChildTestState>(sm.CurrentStates[^1]);
    }

    // A partially covered path used to be treated exactly like a fully covered one.
    [Fact]
    public void StrictTransitions_throws_when_only_part_of_the_path_is_registered()
    {
        var sm = new GuardedStateMachine(CreateHierarchyFactory());
        sm.RegisterTransition(new GrandchildToChildSegmentTransition());
        sm.ChangeState<GrandchildTestState>();
        sm.Strict = true;

        Assert.Throws<InvalidOperationException>(() => sm.TransitionTo<SiblingChildTestState>());
        Assert.IsType<GrandchildTestState>(sm.CurrentStates[^1]);
    }

    // Same setup as above, with the default permissive mode: behaviour is unchanged.
    [Fact]
    public void Partially_registered_path_still_transitions_when_not_strict()
    {
        var sm = new GuardedStateMachine(CreateHierarchyFactory());
        sm.RegisterTransition(new GrandchildToChildSegmentTransition());
        sm.ChangeState<GrandchildTestState>();

        sm.TransitionTo<SiblingChildTestState>();

        Assert.IsType<SiblingChildTestState>(sm.CurrentStates[^1]);
    }

    // ChangeState is the documented escape hatch and stays outside the registry check.
    [Fact]
    public void ChangeState_is_not_subject_to_StrictTransitions()
    {
        var sm = new GuardedStateMachine(CreateHierarchyFactory());
        sm.ChangeState<ChildTestState>();
        sm.Strict = true;

        sm.ChangeState<SiblingChildTestState>();

        Assert.IsType<SiblingChildTestState>(sm.CurrentStates[^1]);
    }
}

/// <summary>Standalone root state used to force a second, differently shaped chain from the factory.</summary>
public sealed class SimpleTestStateForFactory : BaseTestState { }
