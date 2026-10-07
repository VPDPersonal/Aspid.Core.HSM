using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public partial class StateMachineBase
    {
        private readonly Dictionary<(Type source, Type target), ITransition> _transitionRegistry = new();

        /// <inheritdoc />
        public bool IsTransitioning => _activeTransitionCts is not null;

        #region Registration
        /// <summary>
        /// Registers a transition for use by <see cref="TransitionTo{TTarget}"/> and
        /// <see cref="TransitionVia{TTransition}"/>. Overwrites any existing transition with the same source/target pair.
        /// </summary>
        /// <param name="transition">The transition to register.</param>
        public void RegisterTransition(ITransition transition)
        {
            var key = (transition.SourceState, transition.TargetState);
            _transitionRegistry[key] = transition;
        }

        /// <inheritdoc cref="RegisterTransition(ITransition)"/>
        /// <typeparam name="TTransition">The transition type.</typeparam>
        public void RegisterTransition<TTransition>(TTransition transition)
            where TTransition : ITransition
        {
            RegisterTransition((ITransition)transition);
        }
        #endregion

        #region TransitionTo (sync)
        /// <inheritdoc />
        public void TransitionTo<TTarget>() where TTarget : IState =>
            TransitionTo(typeof(TTarget));

        /// <inheritdoc cref="TransitionTo{TTarget}"/>
        /// <param name="targetType">The target leaf state type.</param>
        public void TransitionTo(Type targetType)
        {
            ThrowIfAsyncTransitionInProgress();
            Request(new PendingRequest(targetType, PendingKind.TransitionTo));
        }

        private void ApplyTransitionTo(Type targetType)
        {
            var currentLeafType = _currentStates[^1].GetType();

            if (!IsStateEnabled(targetType) || !IsTransitionEnabled(currentLeafType, targetType))
                return;

            var transition = ResolveTransition(currentLeafType, targetType);

            if (transition is not null)
            {
                if (!transition.CanTransition())
                    return;

                transition.OnBeforeTransition();
                ChangeStateCore(targetType);
                transition.OnAfterTransition();
                return;
            }

            var chain = ResolveTransitionChain(targetType, out var isComplete);

            if (!isComplete && StrictTransitions)
                throw UnregisteredTransition(currentLeafType, targetType);

            if (chain is null)
            {
                ChangeStateCore(targetType);
                return;
            }

            foreach (var t in chain)
            {
                if (!t.CanTransition())
                    return;
            }

            foreach (var t in chain)
                t.OnBeforeTransition();

            ChangeStateCore(targetType);

            for (var i = chain.Count - 1; i >= 0; i--)
                chain[i].OnAfterTransition();
        }
        #endregion

        #region TransitionVia (sync)
        /// <inheritdoc />
        public void TransitionVia<TTransition>() where TTransition : ITransition =>
            TransitionVia(typeof(TTransition));

        /// <inheritdoc cref="TransitionVia{TTransition}"/>
        /// <param name="transitionType">The registered transition type to execute.</param>
        public void TransitionVia(Type transitionType)
        {
            ThrowIfAsyncTransitionInProgress();
            Request(new PendingRequest(transitionType, PendingKind.TransitionVia));
        }

        private void ApplyTransitionVia(Type transitionType)
        {
            var transition = FindTransitionByType(transitionType);
            var currentLeafType = _currentStates[^1].GetType();

            if (!IsStateEnabled(transition.TargetState) ||
                !IsTransitionEnabled(currentLeafType, transition.TargetState) ||
                !transition.CanTransition())
                return;

            transition.OnBeforeTransition();
            ChangeStateCore(transition.TargetState);
            transition.OnAfterTransition();
        }
        #endregion

        #region TransitionTo (async)
        /// <summary>
        /// Asynchronously transitions to <typeparamref name="TTarget"/>, executing any registered
        /// <see cref="ITransition"/> guards and hooks along the path.
        /// </summary>
        /// <typeparam name="TTarget">The target leaf state type.</typeparam>
        /// <param name="ct">Cancellation token for the transition.</param>
        /// <exception cref="InvalidOperationException">
        /// A synchronous state change is in progress, for example when called from a synchronous enter or exit callback.
        /// Or <see cref="StrictTransitions"/> is enabled and no registered transition covers the whole path.
        /// </exception>
        public UniTask TransitionToAsync<TTarget>(CancellationToken ct = default)
            where TTarget : IState =>
            TransitionToAsync(typeof(TTarget), ct);

        /// <inheritdoc cref="TransitionToAsync{TTarget}"/>
        /// <param name="targetType">The target leaf state type.</param>
        /// <param name="ct">Cancellation token for the transition.</param>
        public async UniTask TransitionToAsync(Type targetType, CancellationToken ct = default)
        {
            ThrowIfSyncChangeInProgress();

            if (!IsStateEnabled(targetType))
                return;

            // Resolve the edge only after the superseded transition has unwound, as ChangeStateAsync does.
            await SupersedeActiveTransitionAsync();
            var currentLeafType = _currentStates[^1].GetType();

            if (!IsTransitionEnabled(currentLeafType, targetType))
                return;

            var transition = ResolveTransition(currentLeafType, targetType);

            if (transition is not null)
            {
                if (!transition.CanTransition())
                    return;

                transition.OnBeforeTransition();
                await RunTransitionCoreAsync(targetType, ct);
                transition.OnAfterTransition();
                return;
            }

            var chain = ResolveTransitionChain(targetType, out var isComplete);

            if (!isComplete && StrictTransitions)
                throw UnregisteredTransition(currentLeafType, targetType);

            if (chain is null)
            {
                await RunTransitionCoreAsync(targetType, ct);
                return;
            }

            foreach (var t in chain)
            {
                if (!t.CanTransition())
                    return;
            }

            foreach (var t in chain)
                t.OnBeforeTransition();

            await RunTransitionCoreAsync(targetType, ct);

            for (var i = chain.Count - 1; i >= 0; i--)
                chain[i].OnAfterTransition();
        }
        #endregion

        #region TransitionVia (async)
        /// <summary>
        /// Asynchronously executes a specific registered transition by type, transitioning to its
        /// <see cref="ITransition.TargetState"/> if <see cref="ITransition.CanTransition"/> returns <see langword="true"/>.
        /// </summary>
        /// <typeparam name="TTransition">The registered transition type to execute.</typeparam>
        /// <param name="ct">Cancellation token for the transition.</param>
        /// <exception cref="InvalidOperationException">
        /// A synchronous state change is in progress, for example when called from a synchronous enter or exit callback.
        /// Or the transition type is not registered.
        /// </exception>
        public UniTask TransitionViaAsync<TTransition>(CancellationToken ct = default)
            where TTransition : ITransition =>
            TransitionViaAsync(typeof(TTransition), ct);

        /// <inheritdoc cref="TransitionViaAsync{TTransition}"/>
        /// <param name="transitionType">The registered transition type to execute.</param>
        /// <param name="ct">Cancellation token for the transition.</param>
        public async UniTask TransitionViaAsync(Type transitionType, CancellationToken ct = default)
        {
            ThrowIfSyncChangeInProgress();

            var transition = FindTransitionByType(transitionType);

            if (!IsStateEnabled(transition.TargetState))
                return;

            await SupersedeActiveTransitionAsync();
            var currentLeafType = _currentStates[^1].GetType();

            if (!IsTransitionEnabled(currentLeafType, transition.TargetState) ||
                !transition.CanTransition())
                return;

            transition.OnBeforeTransition();
            await RunTransitionCoreAsync(transition.TargetState, ct);
            transition.OnAfterTransition();
        }
        #endregion

        #region Resolution
        /// <summary>
        /// Resolves a registered transition between the given source and target state types.
        /// Override to implement custom transition resolution (e.g. convention-based lookup).
        /// </summary>
        /// <param name="sourceType">The source state type.</param>
        /// <param name="targetType">The target state type.</param>
        /// <returns>The matching transition, or <c>null</c> if none is registered.</returns>
        protected virtual ITransition? ResolveTransition(Type sourceType, Type targetType)
        {
            var key = (sourceType, targetType);
            return _transitionRegistry.TryGetValue(key, out var transition) ? transition : null;
        }

        private ITransition FindTransitionByType(Type transitionType)
        {
            foreach (var transition in _transitionRegistry.Values)
            {
                if (transition.GetType() == transitionType)
                    return transition;
            }

            throw new InvalidOperationException(
                $"Transition of type '{transitionType.Name}' is not registered.");
        }

        private static InvalidOperationException UnregisteredTransition(Type sourceType, Type targetType) =>
            new($"No registered transition covers the full path from '{sourceType.Name}' to '{targetType.Name}'. " +
                $"{nameof(StrictTransitions)} is enabled, so the transition registry declares which edges are legal. " +
                $"Register a transition for the missing edge, or call ChangeState({targetType.Name}) to bypass the registry.");
        #endregion

        #region Chain Resolution

        private readonly List<Type> _targetTypeChainBuffer = new(capacity: 4);
        private readonly List<Type> _pathTypeBuffer = new(capacity: 8);

        /// <summary>
        /// Collects the registered transitions covering the exit/enter path to <paramref name="targetType"/>.
        /// </summary>
        /// <param name="targetType">The target leaf state type.</param>
        /// <param name="isComplete">
        /// <c>true</c> when a transition was found for <em>every</em> step of the path — which is what
        /// <see cref="StrictTransitions"/> requires. A partially covered path reports <c>false</c> while still
        /// returning the transitions that were found, preserving the permissive default behaviour.
        /// </param>
        /// <returns>The transitions found along the path, or <c>null</c> if none were.</returns>
        private List<ITransition>? ResolveTransitionChain(Type targetType, out bool isComplete)
        {
            // Build the target type chain by walking IChildState.ParentState
            _targetTypeChainBuffer.Clear();
            BuildTypeChain(targetType, _targetTypeChainBuffer);

            // Find diverge index by comparing current chain types with target chain types
            var currentCount = _currentStates.Count;
            var commonLength = Math.Min(currentCount, _targetTypeChainBuffer.Count);
            var divergeIndex = commonLength;

            for (var i = 0; i < commonLength; i++)
            {
                if (_currentStates[i].GetType() != _targetTypeChainBuffer[i])
                {
                    divergeIndex = i;
                    break;
                }
            }

            // Build the traversal path: exiting states (leaf→diverge), then entering states (diverge→leaf)
            var pathTypes = _pathTypeBuffer;
            pathTypes.Clear();

            for (var i = currentCount - 1; i >= divergeIndex; i--)
                pathTypes.Add(_currentStates[i].GetType());

            for (var i = divergeIndex; i < _targetTypeChainBuffer.Count; i++)
                pathTypes.Add(_targetTypeChainBuffer[i]);

            // Look up transitions for each consecutive pair
            var requiredSegments = Math.Max(0, pathTypes.Count - 1);
            var foundSegments = 0;
            List<ITransition>? chain = null;

            for (var i = 0; i < requiredSegments; i++)
            {
                var key = (pathTypes[i], pathTypes[i + 1]);
                if (_transitionRegistry.TryGetValue(key, out var segmentTransition))
                {
                    chain ??= new List<ITransition>();
                    chain.Add(segmentTransition);
                    foundSegments++;
                }
            }

            isComplete = foundSegments == requiredSegments;
            return chain;
        }

        private static void BuildTypeChain(Type leafType, List<Type> result)
        {
            if (TryGetParentStateType(leafType, out var parentType))
                BuildTypeChain(parentType, result);

            result.Add(leafType);
        }

        /// <summary>
        /// Reads a state's parent type from its <see cref="IChildState{T}"/> interface without
        /// instantiating it, so DI states (no public parameterless constructor) resolve correctly
        /// and no throwaway instances / constructor side effects are produced.
        /// </summary>
        private static bool TryGetParentStateType(Type stateType, out Type parentType)
        {
            foreach (var contract in stateType.GetInterfaces())
            {
                if (contract.IsGenericType &&
                    contract.GetGenericTypeDefinition() == typeof(IChildState<>))
                {
                    parentType = contract.GetGenericArguments()[0];
                    return true;
                }
            }

            parentType = null!;
            return false;
        }

        #endregion

        #region Helpers
        private void ThrowIfAsyncTransitionInProgress()
        {
            if (_activeTransitionCts is not null)
                throw new InvalidOperationException(
                    "An asynchronous transition is in progress. Use the async transition methods or wait for it to complete.");
        }

        // The mirror of ThrowIfAsyncTransitionInProgress. An async change started from a synchronous Enter/OnEnter
        // would run inside the synchronous change and rewrite the chain that change is still iterating.
        private void ThrowIfSyncChangeInProgress()
        {
            if (_isChangingState)
                throw new InvalidOperationException(
                    "A synchronous state change is in progress. Call ChangeState, TransitionTo or TransitionVia instead: " +
                    "they queue the request and apply it once the running change completes.");
        }
        #endregion
    }
}
