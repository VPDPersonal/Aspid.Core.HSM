using System;
using System.Collections.Generic;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

/// <summary>
/// Minimal hierarchical container: services and state creators registered in a scope are visible to its
/// child scopes, and a state is always constructed against the scope it is resolved from.
/// </summary>
public class TestScope : IStateScope
{
    private readonly Dictionary<Type, object> _services = new();
    private readonly Dictionary<Type, Func<TestScope, IState>> _stateCreators = new();

    public IStateScope? Parent { get; }
    public bool IsDisposed { get; private set; }
    public int ChildScopeCount { get; private set; }

    public TestScope(IStateScope? parent = null) => Parent = parent;

    public void Register<TService>(TService service) where TService : class =>
        _services[typeof(TService)] = service;

    public TService? Get<TService>() where TService : class
    {
        Assert.False(IsDisposed, "Resolved from a disposed scope.");

        return _services.TryGetValue(typeof(TService), out var service)
            ? (TService)service
            : (Parent as TestScope)?.Get<TService>();
    }

    public void RegisterState<TState>(Func<TestScope, TState> creator) where TState : IState =>
        _stateCreators[typeof(TState)] = scope => creator(scope);

    public IState? Resolve(Type stateType) => ResolveFrom(stateType, this);

    private IState? ResolveFrom(Type stateType, TestScope origin) =>
        _stateCreators.TryGetValue(stateType, out var creator)
            ? creator(origin)
            : (Parent as TestScope)?.ResolveFrom(stateType, origin);

    public IStateScope CreateChildScope()
    {
        ChildScopeCount++;
        return new TestScope(this);
    }

    public void Dispose() => IsDisposed = true;
}

public class ScopeLifecycleTests
{
    [Fact]
    public void Scope_created_for_state_on_enter()
    {
        var factory = new TestStateFactory();
        factory.RegisterState<SimpleTestState>();
        var rootScope = new TestScope();
        factory.SetRootScope(rootScope);
        var stateMachine = new TestableStateMachine(factory);

        stateMachine.ChangeState<SimpleTestState>();

        var scope = factory.GetScope(typeof(SimpleTestState));
        Assert.NotNull(scope);
    }

    [Fact]
    public void Scope_disposed_on_state_exit()
    {
        var factory = new TestStateFactory();
        factory.RegisterState<SimpleTestState>();
        factory.RegisterState<AnotherTestState>();
        var rootScope = new TestScope();
        factory.SetRootScope(rootScope);
        var stateMachine = new TestableStateMachine(factory);

        stateMachine.ChangeState<SimpleTestState>();
        var scope = factory.GetScope(typeof(SimpleTestState)) as TestScope;
        Assert.NotNull(scope);

        stateMachine.ChangeState<AnotherTestState>();

        Assert.True(scope.IsDisposed);
        Assert.Null(factory.GetScope(typeof(SimpleTestState)));
    }

    [Fact]
    public void Child_state_scope_has_parent_scope()
    {
        var factory = new TestStateFactory();
        factory.RegisterState<ParentTestState>();
        factory.RegisterState<ChildTestState>();
        var rootScope = new TestScope();
        factory.SetRootScope(rootScope);
        var stateMachine = new TestableStateMachine(factory);

        stateMachine.ChangeState<ChildTestState>();

        var parentScope = factory.GetScope(typeof(ParentTestState));
        var childScope = factory.GetScope(typeof(ChildTestState));
        Assert.NotNull(parentScope);
        Assert.NotNull(childScope);
        Assert.Same(parentScope, childScope.Parent);
    }

    [Fact]
    public void Cached_scope_survives_exit()
    {
        var factory = new TestStateFactory();
        factory.RegisterState<CachedTestState>();
        factory.RegisterState<AnotherTestState>();
        var rootScope = new TestScope();
        factory.SetRootScope(rootScope);
        var stateMachine = new TestableStateMachine(factory);

        stateMachine.ChangeState<CachedTestState>();
        var scope = factory.GetScope(typeof(CachedTestState)) as TestScope;
        Assert.NotNull(scope);

        stateMachine.ChangeState<AnotherTestState>();

        Assert.False(scope.IsDisposed);
    }

    [Fact]
    public void Cached_scope_reused_on_reenter()
    {
        var factory = new TestStateFactory();
        factory.RegisterState<CachedTestState>();
        factory.RegisterState<AnotherTestState>();
        var rootScope = new TestScope();
        factory.SetRootScope(rootScope);
        var stateMachine = new TestableStateMachine(factory);

        stateMachine.ChangeState<CachedTestState>();
        var firstScope = factory.GetScope(typeof(CachedTestState));
        Assert.NotNull(firstScope);

        stateMachine.ChangeState<AnotherTestState>();
        stateMachine.ChangeState<CachedTestState>();

        var secondScope = factory.GetScope(typeof(CachedTestState));
        Assert.Same(firstScope, secondScope);
    }

    [Fact]
    public void Cached_scope_disposed_on_DisposeAllScopes()
    {
        var factory = new TestStateFactory();
        factory.RegisterState<CachedTestState>();
        factory.RegisterState<AnotherTestState>();
        var rootScope = new TestScope();
        factory.SetRootScope(rootScope);
        var stateMachine = new TestableStateMachine(factory);

        stateMachine.ChangeState<CachedTestState>();
        var scope = factory.GetScope(typeof(CachedTestState)) as TestScope;
        Assert.NotNull(scope);

        // Move away so scope goes to cached dictionary
        stateMachine.ChangeState<AnotherTestState>();
        Assert.False(scope.IsDisposed);

        factory.DisposeAllScopes();

        Assert.True(scope.IsDisposed);
    }

    [Fact]
    public void No_scope_created_when_no_root_scope_set()
    {
        var factory = new TestStateFactory();
        factory.RegisterState<SimpleTestState>();
        // Do NOT call factory.SetRootScope
        var stateMachine = new TestableStateMachine(factory);

        stateMachine.ChangeState<SimpleTestState>();

        var scope = factory.GetScope(typeof(SimpleTestState));
        Assert.Null(scope);
    }
}
