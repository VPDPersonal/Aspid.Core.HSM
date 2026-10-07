using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

#region Test Transitions

public class TestTransition : ITransition<SimpleTestState, AnotherTestState>
{
    public bool GuardResult { get; set; } = true;
    public int BeforeCount { get; private set; }
    public int AfterCount { get; private set; }

    public bool CanTransition() => GuardResult;
    public void OnBeforeTransition() => BeforeCount++;
    public void OnAfterTransition() => AfterCount++;
}

public class AnotherTransition : ITransition<AnotherTestState, SimpleTestState>
{
    public bool GuardResult { get; set; } = true;
    public int BeforeCount { get; private set; }
    public int AfterCount { get; private set; }

    public bool CanTransition() => GuardResult;
    public void OnBeforeTransition() => BeforeCount++;
    public void OnAfterTransition() => AfterCount++;
}

public class ReplacementTransition : ITransition<SimpleTestState, AnotherTestState>
{
    public bool GuardResult { get; set; } = true;
    public int BeforeCount { get; private set; }
    public int AfterCount { get; private set; }

    public bool CanTransition() => GuardResult;
    public void OnBeforeTransition() => BeforeCount++;
    public void OnAfterTransition() => AfterCount++;
}

public class ParentToAnotherTransition : ITransition<ParentTestState, AnotherTestState>
{
    public int BeforeCount { get; private set; }

    public void OnBeforeTransition() => BeforeCount++;
}

#endregion

public class TransitionPipelineTests
{
    private TestStateFactory CreateFactory(
        SimpleTestState? simple = null,
        AnotherTestState? another = null)
    {
        var factory = new TestStateFactory();
        factory.RegisterState(() => simple ?? new SimpleTestState());
        factory.RegisterState(() => another ?? new AnotherTestState());
        return factory;
    }

    #region TransitionTo

    [Fact]
    public void TransitionTo_executes_state_change()
    {
        var simple = new SimpleTestState();
        var another = new AnotherTestState();
        var factory = CreateFactory(simple, another);
        var sm = new TestableStateMachine(factory);

        sm.ChangeState<SimpleTestState>();
        sm.TransitionTo<AnotherTestState>();

        Assert.Contains(another, sm.CurrentStates);
        Assert.DoesNotContain(simple, sm.CurrentStates);
    }

    [Fact]
    public void TransitionTo_with_registered_transition_calls_guard()
    {
        var simple = new SimpleTestState();
        var another = new AnotherTestState();
        var factory = CreateFactory(simple, another);
        var sm = new TestableStateMachine(factory);

        var transition = new TestTransition { GuardResult = true };
        sm.RegisterTransition(transition);

        sm.ChangeState<SimpleTestState>();
        sm.TransitionTo<AnotherTestState>();

        // Guard was invoked and allowed the transition
        Assert.Contains(another, sm.CurrentStates);
    }

    [Fact]
    public void TransitionTo_aborts_when_guard_returns_false()
    {
        var simple = new SimpleTestState();
        var another = new AnotherTestState();
        var factory = CreateFactory(simple, another);
        var sm = new TestableStateMachine(factory);

        var transition = new TestTransition { GuardResult = false };
        sm.RegisterTransition(transition);

        sm.ChangeState<SimpleTestState>();
        sm.TransitionTo<AnotherTestState>();

        // State should NOT have changed
        Assert.Contains(simple, sm.CurrentStates);
        Assert.DoesNotContain(another, sm.CurrentStates);
        // Hooks should NOT have fired
        Assert.Equal(0, transition.BeforeCount);
        Assert.Equal(0, transition.AfterCount);
    }

    [Fact]
    public void TransitionTo_calls_OnBeforeTransition_and_OnAfterTransition()
    {
        var simple = new SimpleTestState();
        var another = new AnotherTestState();
        var factory = CreateFactory(simple, another);
        var sm = new TestableStateMachine(factory);

        var transition = new TestTransition();
        sm.RegisterTransition(transition);

        sm.ChangeState<SimpleTestState>();
        sm.TransitionTo<AnotherTestState>();

        Assert.Equal(1, transition.BeforeCount);
        Assert.Equal(1, transition.AfterCount);
    }

    [Fact]
    public void TransitionTo_without_registered_transition_still_works()
    {
        var simple = new SimpleTestState();
        var another = new AnotherTestState();
        var factory = CreateFactory(simple, another);
        var sm = new TestableStateMachine(factory);

        // No transition registered
        sm.ChangeState<SimpleTestState>();
        sm.TransitionTo<AnotherTestState>();

        Assert.Contains(another, sm.CurrentStates);
        Assert.DoesNotContain(simple, sm.CurrentStates);
    }

    #endregion

    #region TransitionVia

    [Fact]
    public void TransitionVia_uses_specific_transition()
    {
        var simple = new SimpleTestState();
        var another = new AnotherTestState();
        var factory = CreateFactory(simple, another);
        var sm = new TestableStateMachine(factory);

        var transition = new TestTransition();
        sm.RegisterTransition(transition);

        sm.ChangeState<SimpleTestState>();
        sm.TransitionVia<TestTransition>();

        Assert.Contains(another, sm.CurrentStates);
        Assert.Equal(1, transition.BeforeCount);
        Assert.Equal(1, transition.AfterCount);
    }

    [Fact]
    public void TransitionVia_throws_if_not_registered()
    {
        var factory = CreateFactory();
        var sm = new TestableStateMachine(factory);

        sm.ChangeState<SimpleTestState>();

        Assert.Throws<InvalidOperationException>(() => sm.TransitionVia<TestTransition>());
    }

    // TransitionVia used to run a transition from anywhere, entering its target without checking its source.
    [Fact]
    public void TransitionVia_does_nothing_outside_its_source()
    {
        var another = new AnotherTestState();
        var factory = CreateFactory(another: another);
        var sm = new TestableStateMachine(factory);
        var transition = new AnotherTransition();
        sm.RegisterTransition(transition);
        sm.RegisterTransition(new TestTransition());
        sm.ChangeState<SimpleTestState>();
        var chain = sm.CurrentStates.ToArray();

        // AnotherTransition leaves AnotherTestState, but the machine is in SimpleTestState.
        sm.TransitionVia<AnotherTransition>();

        Assert.Equal(chain, sm.CurrentStates.ToArray());
        Assert.Equal(0, transition.BeforeCount);
        Assert.Equal(0, transition.AfterCount);
    }

    [Fact]
    public async Task TransitionViaAsync_does_nothing_outside_its_source()
    {
        var factory = CreateFactory();
        var sm = new TestableStateMachine(factory);
        var transition = new AnotherTransition();
        sm.RegisterTransition(transition);
        sm.ChangeState<SimpleTestState>();
        var chain = sm.CurrentStates.ToArray();

        await sm.TransitionViaAsync<AnotherTransition>();

        Assert.Equal(chain, sm.CurrentStates.ToArray());
        Assert.Equal(0, transition.BeforeCount);
    }

    [Fact]
    public void TransitionVia_runs_from_a_descendant_of_its_source()
    {
        var factory = new TestStateFactory();
        factory.RegisterState<ParentTestState>();
        factory.RegisterState<ChildTestState>();
        factory.RegisterState<AnotherTestState>();
        var sm = new TestableStateMachine(factory);
        var transition = new ParentToAnotherTransition();
        sm.RegisterTransition(transition);
        sm.ChangeState<ChildTestState>();

        sm.TransitionVia<ParentToAnotherTransition>();

        Assert.IsType<AnotherTestState>(Assert.Single(sm.CurrentStates));
        Assert.Equal(1, transition.BeforeCount);
    }

    [Fact]
    public void TransitionVia_outside_its_source_throws_under_strict_transitions()
    {
        var factory = CreateFactory();
        var sm = new GuardedStateMachine(factory) { Strict = true };
        sm.RegisterTransition(new AnotherTransition());
        sm.ChangeState<SimpleTestState>();

        var exception = Assert.Throws<InvalidOperationException>(() => sm.TransitionVia<AnotherTransition>());

        Assert.Contains(nameof(AnotherTestState), exception.Message);
        Assert.IsType<SimpleTestState>(Assert.Single(sm.CurrentStates));
    }

    #endregion

    #region RegisterTransition

    [Fact]
    public void RegisterTransition_replaces_existing()
    {
        var simple = new SimpleTestState();
        var another = new AnotherTestState();
        var factory = CreateFactory(simple, another);
        var sm = new TestableStateMachine(factory);

        var original = new TestTransition();
        var replacement = new ReplacementTransition();

        sm.RegisterTransition(original);
        sm.RegisterTransition(replacement);

        sm.ChangeState<SimpleTestState>();
        sm.TransitionTo<AnotherTestState>();

        // Replacement should have been used, not the original
        Assert.Equal(0, original.BeforeCount);
        Assert.Equal(0, original.AfterCount);
        Assert.Equal(1, replacement.BeforeCount);
        Assert.Equal(1, replacement.AfterCount);
    }

    #endregion
}
