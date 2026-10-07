#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Responsible for creating state instances, each inside its own <see cref="IStateScope"/>, and for
    /// resolving the root-to-leaf chain of state types declared by <see cref="IChildState{T}"/>.
    /// Also tracks first-time initialization.
    /// </summary>
    /// <remarks>
    /// A state is created only right before it is entered: by then every ancestor has been entered and
    /// owns an active scope, so the state's scope is created as a child of its parent's scope and the state
    /// itself is resolved from it (<see cref="CreateStateInternal"/>). Dependencies a parent registers in its
    /// scope are therefore visible to the constructors of its descendants.
    /// </remarks>
    public abstract class StateFactory
    {
        private readonly HashSet<Type> _initializedStates = new();
        private readonly Dictionary<Type, Type?> _parentTypeCache = new();

        private IStateScope? _rootScope;
        private readonly Dictionary<Type, IStateScope> _activeScopes = new();
        private readonly Dictionary<Type, IStateScope> _cachedScopes = new();

        /// <summary>
        /// Clears <paramref name="destination"/> and fills it with the root-to-leaf chain of state types
        /// ending in <paramref name="leafType"/>, following <see cref="IChildState{T}"/> without creating any state.
        /// </summary>
        /// <param name="leafType">The target leaf state type.</param>
        /// <param name="destination">The list to fill. Cleared before use.</param>
        /// <exception cref="InvalidOperationException">
        /// A state in the chain implements <see cref="IChildState"/> without <see cref="IChildState{T}"/>.
        /// </exception>
        public void BuildTypeChain(Type leafType, List<Type> destination)
        {
            destination.Clear();

            for (Type? type = leafType; type is not null; type = GetParentType(type))
                destination.Add(type);

            destination.Reverse();
        }

        /// <summary>
        /// Returns the parent state type <paramref name="stateType"/> declares through
        /// <see cref="IChildState{T}"/>, or <c>null</c> for a root state.
        /// </summary>
        /// <param name="stateType">The state type to inspect. Not instantiated.</param>
        /// <exception cref="InvalidOperationException">
        /// <paramref name="stateType"/> implements <see cref="IChildState"/> without <see cref="IChildState{T}"/>.
        /// </exception>
        public Type? GetParentType(Type stateType)
        {
            if (_parentTypeCache.TryGetValue(stateType, out var parentType))
                return parentType;

            if (typeof(IChildState).IsAssignableFrom(stateType))
            {
                foreach (var contract in stateType.GetInterfaces())
                {
                    if (!contract.IsGenericType || contract.GetGenericTypeDefinition() != typeof(IChildState<>))
                        continue;

                    parentType = contract.GetGenericArguments()[0];
                    break;
                }

                // The parent must be known from the type alone: a state is created inside its parent's scope,
                // so reading the parent off an instance would mean creating the state before its scope exists.
                if (parentType is null)
                    throw new InvalidOperationException(
                        $"{stateType} implements {nameof(IChildState)} without {nameof(IChildState)}<TParent>. " +
                        $"Declare the parent by implementing {nameof(IChildState)}<TParent>.");
            }

            _parentTypeCache[stateType] = parentType;
            return parentType;
        }

        /// <summary>
        /// Creates a state of <paramref name="stateType"/> inside its own scope. The scope is activated first —
        /// a child of the nearest active ancestor's scope, or reused when the state is
        /// <see cref="ScopeLifetime.Cached"/> — and then passed to <see cref="CreateStateInternal"/>.
        /// </summary>
        /// <remarks>
        /// Call it only once the ancestors of <paramref name="stateType"/> are active. The state is not marked
        /// initialized; <see cref="Release"/> undoes the scope activation. If creation throws, the scope is
        /// released here and the exception propagates.
        /// </remarks>
        /// <param name="stateType">The state or extension type to create.</param>
        public IState CreateState(Type stateType)
        {
            var scope = _rootScope is not null ? ActivateScope(stateType) : null;

            try
            {
                return CreateStateInternal(stateType, scope);
            }
            catch
            {
                ReleaseScope(stateType);
                throw;
            }
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
        /// Marks a state as initialized. On first initialization, calls <see cref="OnInitializeState(IState)"/>.
        /// </summary>
        /// <param name="state">The state instance to mark as initialized.</param>
        public void MarkInitialized(IState state)
        {
            if (_initializedStates.Add(state.GetType()))
                OnInitializeState(state);
        }

        /// <summary>
        /// Instantiates a state of the given type. Implement with your DI container — resolving from
        /// <paramref name="scope"/> gives the state access to everything its ancestors registered in their
        /// scopes — or with <c>Activator.CreateInstance</c>.
        /// </summary>
        /// <param name="type">The state type to create.</param>
        /// <param name="scope">
        /// The state's own scope, already active. <c>null</c> when no root scope has been set, or when
        /// <see cref="CreateScopeForState"/> returned <c>null</c> for this state.
        /// </param>
        protected abstract IState CreateStateInternal(Type type, IStateScope? scope);

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

            // Cached states keep their scope AND their initialized flag on re-entry, so the one-time
            // OnInitializeState hook stays truly one-time. Transient states are fully reset.
            if (GetScopeLifetime(stateType) != ScopeLifetime.Cached)
                _initializedStates.Remove(stateType);

            ReleaseInternal(state);
            ReleaseScope(stateType);
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

        private IStateScope? ActivateScope(Type stateType)
        {
            if (_activeScopes.TryGetValue(stateType, out var activeScope))
                return activeScope;

            if (_cachedScopes.TryGetValue(stateType, out var cachedScope))
            {
                _cachedScopes.Remove(stateType);
                _activeScopes[stateType] = cachedScope;
                return cachedScope;
            }

            var newScope = CreateScopeForState(stateType, ResolveParentScope(stateType));
            if (newScope != null)
                _activeScopes[stateType] = newScope;

            return newScope;
        }

        private void ReleaseScope(Type stateType)
        {
            if (!_activeScopes.TryGetValue(stateType, out var scope))
                return;

            _activeScopes.Remove(stateType);

            if (GetScopeLifetime(stateType) == ScopeLifetime.Cached)
                _cachedScopes[stateType] = scope;
            else
                scope.Dispose();
        }

        // The nearest ancestor with an active scope: an ancestor for which CreateScopeForState returned null
        // is skipped rather than cutting its descendants off the hierarchy.
        private IStateScope? ResolveParentScope(Type stateType)
        {
            for (var parentType = GetParentType(stateType); parentType is not null; parentType = GetParentType(parentType))
            {
                if (_activeScopes.TryGetValue(parentType, out var parentScope))
                    return parentScope;
            }

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
