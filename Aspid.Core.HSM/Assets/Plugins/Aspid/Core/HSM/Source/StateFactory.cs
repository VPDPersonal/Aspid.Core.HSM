using System;
using System.Collections.Generic;
using System.Reflection;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Responsible for creating state instances and building parent-to-leaf chains
    /// by walking <see cref="IChildState.ParentState"/>. Also manages per-state
    /// <see cref="IStateScope"/> instances and tracks first-time initialization.
    /// </summary>
    public abstract class StateFactory
    {
        private readonly HashSet<Type> _initializedStates = new();
        private readonly List<IState> _chainBuffer = new(capacity: 4);

        private IStateScope? _rootScope;
        private readonly Dictionary<Type, IStateScope> _activeScopes = new();
        private readonly Dictionary<Type, IStateScope> _cachedScopes = new();

        /// <summary>
        /// Builds the full root-to-leaf state chain for <typeparamref name="TState"/>,
        /// reusing states from <paramref name="activeStates"/> where types match at the same depth.
        /// </summary>
        /// <typeparam name="TState">The target leaf state type.</typeparam>
        /// <param name="activeStates">The currently active state chain for reuse comparison.</param>
        /// <returns>The new state chain ordered root-to-leaf.</returns>
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

        /// <summary>
        /// Sets the root DI scope from which all per-state child scopes are derived.
        /// </summary>
        /// <param name="rootScope">The root scope (typically the application container).</param>
        public void SetRootScope(IStateScope rootScope) => _rootScope = rootScope;

        /// <summary>
        /// Returns the active <see cref="IStateScope"/> for <paramref name="stateType"/>,
        /// or <c>null</c> if no scope is currently active for that state.
        /// </summary>
        public IStateScope? GetScope(Type stateType) =>
            _activeScopes.TryGetValue(stateType, out var scope) ? scope : null;

        /// <inheritdoc cref="GetScope(Type)"/>
        /// <typeparam name="TState">The state type to look up.</typeparam>
        public IStateScope? GetScope<TState>() where TState : IState =>
            GetScope(typeof(TState));

        /// <summary>
        /// Marks a state as initialized. On first initialization, calls <see cref="OnInitializeState(IState)"/>
        /// and activates the state's <see cref="IStateScope"/> if a root scope has been set.
        /// </summary>
        /// <param name="state">The state instance to mark as initialized.</param>
        public void MarkInitialized(IState state)
        {
            var stateType = state.GetType();
            var firstTime = _initializedStates.Add(stateType);

            if (firstTime)
                OnInitializeState(state);

            if (_rootScope != null)
                ActivateScope(stateType, state);
        }

        /// <summary>
        /// Creates a new state instance without adding it to the chain or marking it initialized.
        /// Used internally for extension states.
        /// </summary>
        /// <param name="type">The state type to instantiate.</param>
        public IState CreateInstance(Type type) => CreateStateInternal(type);

        /// <summary>
        /// Instantiates a state of the given type. Implement with your DI container or <c>Activator.CreateInstance</c>.
        /// </summary>
        /// <param name="type">The state type to create.</param>
        protected abstract IState CreateStateInternal(Type type);

        /// <summary>
        /// Called the first time a state type is initialized. Override to perform one-time setup.
        /// </summary>
        /// <param name="state">The newly initialized state instance.</param>
        protected virtual void OnInitializeState(IState state) { }

        /// <summary>
        /// Releases a state after it has been exited. Removes it from the initialized set,
        /// calls <see cref="ReleaseInternal"/>, and disposes or caches its scope.
        /// </summary>
        /// <param name="state">The state to release. <see cref="EmptyState"/> is silently ignored.</param>
        public void Release(IState state)
        {
            if (state is EmptyState) return;

            var stateType = state.GetType();
            var isCached = GetScopeLifetime(stateType) == ScopeLifetime.Cached;

            // Cached states keep their scope AND their initialized flag on re-entry, so the one-time
            // OnInitializeState hook stays truly one-time. Transient states are fully reset.
            if (!isCached)
                _initializedStates.Remove(stateType);

            ReleaseInternal(state);

            if (_activeScopes.TryGetValue(stateType, out var scope))
            {
                _activeScopes.Remove(stateType);

                if (isCached)
                    _cachedScopes[stateType] = scope;
                else
                    scope.Dispose();
            }
        }

        /// <summary>
        /// Called when a state is released. Override to perform custom cleanup.
        /// </summary>
        /// <param name="state">The state being released.</param>
        protected virtual void ReleaseInternal(IState state) { }

        /// <summary>
        /// Creates a DI scope for a state. Override to customize scope creation.
        /// Default implementation calls <see cref="IStateScope.CreateChildScope"/> on the parent scope.
        /// </summary>
        /// <param name="stateType">The state type the scope is being created for.</param>
        /// <param name="parentScope">The parent state's scope, or the root scope if this is a root state.</param>
        /// <returns>The new scope, or <c>null</c> to skip scope creation for this state.</returns>
        protected virtual IStateScope? CreateScopeForState(Type stateType, IStateScope? parentScope) =>
            parentScope?.CreateChildScope();

        /// <summary>
        /// Disposes all active and cached scopes. Call during state machine shutdown.
        /// </summary>
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

    /// <summary>
    /// Generic variant of <see cref="StateFactory"/> that provides strongly-typed
    /// <see cref="OnInitializeState(TState)"/> and <see cref="ReleaseInternal(TState)"/> overrides.
    /// </summary>
    /// <typeparam name="TState">The base state type all states in this factory derive from.</typeparam>
    public abstract class StateFactory<TState> : StateFactory
        where TState : IState
    {
        /// <inheritdoc />
        protected sealed override void OnInitializeState(IState state) =>
            OnInitializeState((TState)state);

        /// <inheritdoc cref="StateFactory.OnInitializeState(IState)"/>
        protected virtual void OnInitializeState(TState state) { }

        /// <inheritdoc />
        protected sealed override void ReleaseInternal(IState state) =>
            ReleaseInternal((TState)state);

        /// <inheritdoc cref="StateFactory.ReleaseInternal(IState)"/>
        protected virtual void ReleaseInternal(TState state) { }
    }
}
