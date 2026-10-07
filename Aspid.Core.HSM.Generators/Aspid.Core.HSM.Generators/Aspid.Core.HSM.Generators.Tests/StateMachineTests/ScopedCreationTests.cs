using System;
using System.Collections.Generic;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

#region Helpers

/// <summary>Something a family state registers in its own scope for its descendants.</summary>
public sealed class FamilyRules;

public sealed class ScopedFamilyState : BaseTestState;

public sealed class ScopedLeafState(FamilyRules rules) : BaseTestState, IChildState<ScopedFamilyState>
{
    public FamilyRules Rules { get; } = rules;
}

public sealed class ScopedSiblingLeafState(FamilyRules rules) : BaseTestState, IChildState<ScopedFamilyState>
{
    public FamilyRules Rules { get; } = rules;
}

public sealed class ScopedOtherRootState : BaseTestState;

public sealed class ThrowingLeafState : BaseTestState, IChildState<ScopedFamilyState>
{
    public ThrowingLeafState() => throw new InvalidOperationException("constructor failed");
}

[ScopeLifetime(ScopeLifetime.Cached)]
public sealed class CachedScopedLeafState : BaseTestState, IChildState<ScopedFamilyState>;

/// <summary>Pause-like overlay bound to the family: lives in a child of the family's scope.</summary>
public sealed class FamilyPauseExtension(FamilyRules rules, Func<bool> isFamilyScopeAlive)
    : BaseTestState, IExtensionState, IChildState<ScopedFamilyState>, IExitController
{
    public FamilyRules Rules { get; } = rules;

    public bool FamilyScopeAliveOnExit { get; private set; }

    public bool CanAttachTo(IState hostState) => true;

    public void OnExit() => FamilyScopeAliveOnExit = isFamilyScopeAlive();
}

public sealed class UnboundExtension : BaseTestState, IExtensionState
{
    public bool CanAttachTo(IState hostState) => true;
}

#endregion

// A state used to be created before its own scope existed — the whole new chain was constructed before the
// first Enter — so it could not depend on anything its ancestors registered in their scopes.
public class ScopedCreationTests
{
    private readonly FamilyRules _rules = new();
    private readonly TestScope _rootScope = new();
    private readonly TestStateFactory _factory = new();
    private readonly TestableStateMachine _sm;

    public ScopedCreationTests()
    {
        _rootScope.RegisterState(_ => new ScopedFamilyState());
        _rootScope.RegisterState(_ => new ScopedOtherRootState());
        _rootScope.RegisterState(scope => new ScopedLeafState(scope.Get<FamilyRules>()!));
        _rootScope.RegisterState(scope => new ScopedSiblingLeafState(scope.Get<FamilyRules>()!));
        _rootScope.RegisterState(_ => new ThrowingLeafState());
        _rootScope.RegisterState(_ => new CachedScopedLeafState());
        _rootScope.RegisterState(_ => new UnboundExtension());
        _rootScope.RegisterState(scope => new FamilyPauseExtension(
            scope.Get<FamilyRules>()!,
            () => _factory.GetScope<ScopedFamilyState>() is TestScope { IsDisposed: false }));

        _factory.SetRootScope(_rootScope);
        _factory.ScopeInstaller = (stateType, scope) =>
        {
            if (stateType == typeof(ScopedFamilyState))
                scope.Register(_rules);
        };

        _sm = new TestableStateMachine(_factory);
    }

    [Fact]
    public void Child_is_resolved_with_what_its_parent_registered_in_its_scope()
    {
        _sm.ChangeState<ScopedLeafState>();

        var leaf = Assert.IsType<ScopedLeafState>(_sm.CurrentStates[1]);
        Assert.Same(_rules, leaf.Rules);
    }

    [Fact]
    public void Each_state_is_created_inside_its_own_scope_under_its_parents_scope()
    {
        _sm.ChangeState<ScopedLeafState>();

        var familyScope = _factory.GetScope<ScopedFamilyState>();
        var leafScope = _factory.GetScope<ScopedLeafState>();

        Assert.Equal(
            new (Type, IStateScope?)[] { (typeof(ScopedFamilyState), familyScope), (typeof(ScopedLeafState), leafScope) },
            _factory.CreatedWithScope);
        Assert.Same(_rootScope, familyScope!.Parent);
        Assert.Same(familyScope, leafScope!.Parent);
    }

    [Fact]
    public void A_state_is_created_only_after_its_parent_has_been_entered()
    {
        var log = new List<string>();
        _factory.ScopeInstaller = (stateType, scope) =>
        {
            log.Add("scope " + stateType.Name);
            if (stateType == typeof(ScopedFamilyState))
                scope.Register(_rules);
        };
        var sm = new LoggingStateMachine(_factory, log);

        sm.ChangeState<ScopedLeafState>();

        Assert.Equal(
            new[] { "scope ScopedFamilyState", "enter ScopedFamilyState", "scope ScopedLeafState", "enter ScopedLeafState" },
            log);
    }

    [Fact]
    public void Sibling_change_keeps_the_family_and_its_scope()
    {
        _sm.ChangeState<ScopedLeafState>();
        var family = _sm.CurrentStates[0];
        var familyScope = _factory.GetScope<ScopedFamilyState>();

        _sm.ChangeState<ScopedSiblingLeafState>();

        Assert.Same(family, _sm.CurrentStates[0]);
        Assert.Same(familyScope, _factory.GetScope<ScopedFamilyState>());
        Assert.Same(_rules, Assert.IsType<ScopedSiblingLeafState>(_sm.CurrentStates[1]).Rules);
        Assert.Equal(1, ((BaseTestState)family).EnterCalled);
    }

    [Fact]
    public void A_throwing_constructor_releases_the_scope_it_was_given()
    {
        _sm.ChangeState<ScopedLeafState>();
        var family = _sm.CurrentStates[0];

        Assert.Throws<InvalidOperationException>(() => _sm.ChangeState<ThrowingLeafState>());

        var (_, scope) = _factory.CreatedWithScope[^1];
        Assert.True(Assert.IsType<TestScope>(scope).IsDisposed);
        Assert.Null(_factory.GetScope<ThrowingLeafState>());
        Assert.Equal(new[] { family }, _sm.CurrentStates);
    }

    [Fact]
    public void A_cached_state_is_created_again_from_its_reused_scope()
    {
        _sm.ChangeState<CachedScopedLeafState>();
        var firstScope = _factory.GetScope<CachedScopedLeafState>();

        _sm.ChangeState<ScopedLeafState>();
        _sm.ChangeState<CachedScopedLeafState>();

        var created = _factory.CreatedWithScope.FindAll(entry => entry.Type == typeof(CachedScopedLeafState));
        Assert.Equal(2, created.Count);
        Assert.Same(firstScope, created[0].Scope);
        Assert.Same(firstScope, created[1].Scope);
    }

    [Fact]
    public void Async_change_creates_each_state_inside_its_own_scope_too()
    {
        _sm.ChangeStateAsync<ScopedLeafState>().GetAwaiter().GetResult();

        Assert.Same(_rules, Assert.IsType<ScopedLeafState>(_sm.CurrentStates[1]).Rules);
        Assert.Same(_factory.GetScope<ScopedFamilyState>(), _factory.GetScope<ScopedLeafState>()!.Parent);
    }

    [Fact]
    public void Bound_extension_lives_in_a_child_of_its_parents_scope()
    {
        _sm.ChangeState<ScopedLeafState>();

        _sm.AttachExtension<FamilyPauseExtension>();

        var extension = Assert.IsType<FamilyPauseExtension>(Assert.Single(_sm.ActiveExtensions));
        Assert.Same(_rules, extension.Rules);
        Assert.Same(_factory.GetScope<ScopedFamilyState>(), _factory.GetScope<FamilyPauseExtension>()!.Parent);
    }

    [Fact]
    public void Bound_extension_does_not_attach_while_its_parent_is_inactive()
    {
        _sm.ChangeState<ScopedOtherRootState>();

        _sm.AttachExtension<FamilyPauseExtension>();

        Assert.Empty(_sm.ActiveExtensions);
        Assert.DoesNotContain(_factory.CreatedWithScope, entry => entry.Type == typeof(FamilyPauseExtension));
    }

    [Fact]
    public void Bound_extension_survives_leaf_changes_inside_its_parent()
    {
        _sm.ChangeState<ScopedLeafState>();
        _sm.AttachExtension<FamilyPauseExtension>();
        var extension = _sm.ActiveExtensions[0];

        _sm.ChangeState<ScopedSiblingLeafState>();

        Assert.Same(extension, Assert.Single(_sm.ActiveExtensions));
        Assert.False(Assert.IsType<TestScope>(_factory.GetScope<FamilyPauseExtension>()).IsDisposed);
    }

    [Fact]
    public void Bound_extension_is_detached_before_its_parents_scope_is_disposed()
    {
        _sm.ChangeState<ScopedLeafState>();
        _sm.AttachExtension<FamilyPauseExtension>();
        var extension = (FamilyPauseExtension)_sm.ActiveExtensions[0];
        var extensionScope = Assert.IsType<TestScope>(_factory.GetScope<FamilyPauseExtension>());

        _sm.ChangeState<ScopedOtherRootState>();

        Assert.Empty(_sm.ActiveExtensions);
        Assert.Equal(1, extension.ExitCalled);
        Assert.True(extension.FamilyScopeAliveOnExit);
        Assert.True(extensionScope.IsDisposed);
    }

    [Fact]
    public void Unbound_extension_is_scoped_under_the_root()
    {
        _sm.ChangeState<ScopedLeafState>();

        _sm.AttachExtension<UnboundExtension>();

        Assert.Same(_rootScope, _factory.GetScope<UnboundExtension>()!.Parent);
    }

    private sealed class LoggingStateMachine(StateFactory factory, List<string> log) : StateMachineBase(factory)
    {
        protected override void OnEnteredState(IState state) => log.Add("enter " + state.GetType().Name);
    }
}
