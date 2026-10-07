using System;
using System.Collections.Generic;

namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

public class TestStateFactory : StateFactory
{
    private readonly List<IState> _releasedStates = [];
    private readonly Dictionary<Type, Func<IState>> _stateCreators = new();

    public IReadOnlyList<IState> ReleasedStates => _releasedStates;

    public void RegisterState<TState>()
        where TState : IState, new()
    {
        _stateCreators[typeof(TState)] = () => new TState();
    }
    
    public void RegisterState<TState>(Func<TState> creator)
        where TState : IState
    {
        _stateCreators[typeof(TState)] = () => creator();
    }

    /// <summary>The scope each state was created with, in creation order.</summary>
    public List<(Type Type, IStateScope? Scope)> CreatedWithScope { get; } = [];

    /// <summary>Runs on every new state scope, like a DI installer registering into it.</summary>
    public Action<Type, TestScope>? ScopeInstaller { get; set; }

    protected override IState CreateStateInternal(Type type, IStateScope? scope)
    {
        CreatedWithScope.Add((type, scope));

        if (scope is TestScope testScope && testScope.Resolve(type) is { } resolved)
            return resolved;

        return _stateCreators.TryGetValue(type, out var creator)
            ? creator()
            : throw new InvalidOperationException($"State of type {type} is not registered.");
    }

    protected override IStateScope? CreateScopeForState(Type stateType, IStateScope? parentScope)
    {
        var scope = base.CreateScopeForState(stateType, parentScope);
        if (scope is TestScope testScope)
            ScopeInstaller?.Invoke(stateType, testScope);

        return scope;
    }

    protected override void ReleaseInternal(IState state) =>
        _releasedStates.Add(state);

    public void ClearReleasedStates() =>
        _releasedStates.Clear();
}

