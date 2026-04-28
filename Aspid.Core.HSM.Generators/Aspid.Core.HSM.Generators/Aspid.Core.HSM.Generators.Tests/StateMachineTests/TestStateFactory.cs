using System;
using System.Collections.Generic;

namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

public class TestStateFactory : StateFactory
{
    private readonly List<IState> _releasedStates = [];
    private readonly Dictionary<Type, Func<IState>> _stateCreators = new();

    public TestStateFactory() { }

    public TestStateFactory(IStateHierarchy hierarchy) : base(hierarchy) { }

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

    protected override IState CreateStateInternal(Type type)
    {
        return _stateCreators.TryGetValue(type, out var creator) 
            ? creator()
            : throw new InvalidOperationException($"State of type {type} is not registered.");
    }

    protected override void ReleaseInternal(IState state) =>
        _releasedStates.Add(state);

    public void ClearReleasedStates() =>
        _releasedStates.Clear();
}

