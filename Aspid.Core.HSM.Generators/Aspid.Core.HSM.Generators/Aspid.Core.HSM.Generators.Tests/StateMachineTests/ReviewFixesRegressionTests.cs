using System;
using System.Collections.Generic;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

#region Helpers

/// <summary>Child state without a public parameterless constructor (a DI-created state).</summary>
public class DiChildState : BaseTestState, IChildState<ParentTestState>
{
    public DiChildState(object dependency)
    {
        _ = dependency ?? throw new ArgumentNullException(nameof(dependency));
    }
}

/// <summary>Extension that produces a fresh instance per creation, to probe same-type double attach.</summary>
public class CountingExtensionState : BaseTestState, IExtensionState
{
    public bool CanAttachTo(IState hostState) => true;
    public void OnAttached(IState hostState) { }
    public void OnDetached(IState hostState) { }
}

public class InitCountingStateFactory : TestStateFactory
{
    public Dictionary<Type, int> InitCounts { get; } = new();

    protected override void OnInitializeState(IState state)
    {
        var type = state.GetType();
        InitCounts[type] = InitCounts.TryGetValue(type, out var count) ? count + 1 : 1;
    }
}

#endregion

public class ReviewFixesRegressionTests
{
    // #1 — chain resolution must read the parent type without instantiating the state,
    // so DI states without a parameterless constructor no longer throw MissingMethodException.
    [Fact]
    public void TransitionTo_di_child_without_parameterless_ctor_does_not_throw()
    {
        var factory = new TestStateFactory();
        factory.RegisterState<ParentTestState>();
        factory.RegisterState<DiChildState>(() => new DiChildState(new object()));
        var sm = new TestableStateMachine(factory);

        sm.ChangeState<ParentTestState>();

        // No direct transition registered → falls into chain resolution → BuildTypeChain(DiChildState).
        var exception = Record.Exception(() => sm.TransitionTo<DiChildState>());

        Assert.Null(exception);
        Assert.IsType<DiChildState>(sm.CurrentStates[^1]);
    }

    // #5 — attaching the same extension type twice must be a no-op; the two would otherwise
    // share one Type-keyed scope and the first detach would dispose it out from under the second.
    [Fact]
    public void AttachExtension_same_type_twice_only_attaches_once()
    {
        var factory = new TestStateFactory();
        factory.RegisterState<SimpleTestState>();
        factory.RegisterState<CountingExtensionState>(() => new CountingExtensionState());
        var sm = new TestableStateMachine(factory);

        sm.ChangeState<SimpleTestState>();
        sm.AttachExtension<CountingExtensionState>();
        sm.AttachExtension<CountingExtensionState>();

        Assert.Single(sm.ActiveExtensions);
    }

    // #6 — Dispose must dispose the factory's active/cached scopes, not leak them.
    [Fact]
    public void Dispose_disposes_active_state_scopes()
    {
        var factory = new TestStateFactory();
        factory.RegisterState<SimpleTestState>();
        var rootScope = new TestScope();
        factory.SetRootScope(rootScope);
        var sm = new TestableStateMachine(factory);

        sm.ChangeState<SimpleTestState>();
        var scope = factory.GetScope(typeof(SimpleTestState)) as TestScope;
        Assert.NotNull(scope);

        sm.Dispose();

        Assert.True(scope!.IsDisposed);
    }

    // #7 — a Cached state keeps its initialized flag across re-entry, so the one-time
    // OnInitializeState hook fires exactly once even though the scope is reused.
    [Fact]
    public void Cached_state_runs_OnInitializeState_only_once_across_reentry()
    {
        var factory = new InitCountingStateFactory();
        factory.RegisterState<CachedTestState>();
        factory.RegisterState<AnotherTestState>();
        var rootScope = new TestScope();
        factory.SetRootScope(rootScope);
        var sm = new TestableStateMachine(factory);

        sm.ChangeState<CachedTestState>();
        sm.ChangeState<AnotherTestState>();
        sm.ChangeState<CachedTestState>();

        Assert.Equal(1, factory.InitCounts[typeof(CachedTestState)]);
    }

    // #7 (control) — a Transient state is fully reset, so its one-time hook re-runs on re-entry.
    [Fact]
    public void Transient_state_runs_OnInitializeState_again_on_reentry()
    {
        var factory = new InitCountingStateFactory();
        factory.RegisterState<SimpleTestState>();
        factory.RegisterState<AnotherTestState>();
        var rootScope = new TestScope();
        factory.SetRootScope(rootScope);
        var sm = new TestableStateMachine(factory);

        sm.ChangeState<SimpleTestState>();
        sm.ChangeState<AnotherTestState>();
        sm.ChangeState<SimpleTestState>();

        Assert.Equal(2, factory.InitCounts[typeof(SimpleTestState)]);
    }
}
