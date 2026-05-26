using System;
using System.Collections.Generic;
using System.Reflection;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public abstract class StateFactory
    {
        private readonly HashSet<Type> _initializedStates = new();
        private readonly List<IState> _chainBuffer = new(capacity: 4);

        private IStateScope? _rootScope;
        private readonly Dictionary<Type, IStateScope> _activeScopes = new();
        private readonly Dictionary<Type, IStateScope> _cachedScopes = new();

        public IReadOnlyList<IState> CreateState<TState>(IReadOnlyList<IState> activeStates)
            where TState : IState
        {
            _chainBuffer.Clear();
            BuildChain(typeof(TState), activeStates, activeStates.Count - 1);
            return _chainBuffer;
        }

        private void BuildChain(Type type, IReadOnlyList<IState> activeStates, int index)
        {
            if (index >= 0 && type == activeStates[index].GetType())
            {
                for (var i = 0; i <= index; i++)
                    _chainBuffer.Add(activeStates[i]);
                return;
            }

            var state = CreateStateInternal(type);

            if (state is IChildState childState)
                BuildChain(childState.ParentState, activeStates, index - 1);

            _chainBuffer.Add(state);
        }

        public void SetRootScope(IStateScope rootScope) => _rootScope = rootScope;

        public IStateScope? GetScope(Type stateType) =>
            _activeScopes.TryGetValue(stateType, out var scope) ? scope : null;

        public IStateScope? GetScope<TState>() where TState : IState =>
            GetScope(typeof(TState));

        public void MarkInitialized(IState state)
        {
            var stateType = state.GetType();
            var firstTime = _initializedStates.Add(stateType);

            if (firstTime)
                OnInitializeState(state);

            if (_rootScope != null)
                ActivateScope(stateType, state);
        }

        public IState CreateInstance(Type type) => CreateStateInternal(type);

        protected abstract IState CreateStateInternal(Type type);

        protected virtual void OnInitializeState(IState state) { }

        public void Release(IState state)
        {
            if (state is EmptyState) return;

            var stateType = state.GetType();

            _initializedStates.Remove(stateType);
            ReleaseInternal(state);

            if (_activeScopes.TryGetValue(stateType, out var scope))
            {
                _activeScopes.Remove(stateType);

                if (GetScopeLifetime(stateType) == ScopeLifetime.Cached)
                    _cachedScopes[stateType] = scope;
                else
                    scope.Dispose();
            }
        }

        protected virtual void ReleaseInternal(IState state) { }

        protected virtual IStateScope? CreateScopeForState(Type stateType, IStateScope? parentScope) =>
            parentScope?.CreateChildScope();

        public void DisposeAllScopes()
        {
            foreach (var scope in _activeScopes.Values)
                scope.Dispose();
            _activeScopes.Clear();

            foreach (var scope in _cachedScopes.Values)
                scope.Dispose();
            _cachedScopes.Clear();
        }

        private void ActivateScope(Type stateType, IState state)
        {
            if (_activeScopes.ContainsKey(stateType))
                return;

            if (_cachedScopes.TryGetValue(stateType, out var cachedScope))
            {
                _cachedScopes.Remove(stateType);
                _activeScopes[stateType] = cachedScope;
                return;
            }

            var parentScope = ResolveParentScope(state);
            var newScope = CreateScopeForState(stateType, parentScope);
            if (newScope != null)
                _activeScopes[stateType] = newScope;
        }

        private IStateScope? ResolveParentScope(IState state)
        {
            if (state is IChildState childState &&
                _activeScopes.TryGetValue(childState.ParentState, out var parentStateScope))
                return parentStateScope;

            return _rootScope;
        }

        private static ScopeLifetime GetScopeLifetime(Type stateType)
        {
            var attribute = stateType.GetCustomAttribute<ScopeLifetimeAttribute>();
            return attribute?.Lifetime ?? ScopeLifetime.Transient;
        }
    }

    public abstract class StateFactory<TState> : StateFactory
        where TState : IState
    {
        protected sealed override void OnInitializeState(IState state) =>
            OnInitializeState((TState)state);

        protected virtual void OnInitializeState(TState state) { }

        protected sealed override void ReleaseInternal(IState state) =>
            ReleaseInternal((TState)state);

        protected virtual void ReleaseInternal(TState state) { }
    }
}
