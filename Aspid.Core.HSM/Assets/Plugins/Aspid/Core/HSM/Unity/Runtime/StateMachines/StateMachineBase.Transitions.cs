#nullable enable
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
        /// <remarks>
        /// A failure inside the state change itself stops it midway, as in <see cref="ChangeState{TState}"/>:
        /// exited states stay exited, and <see cref="OnChangedState"/> still runs before the exception propagates.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// A state on the path has an async-only enter or exit; checked before any transition hook, the chain is left unchanged.
        /// Or an <see cref="IEntrySelector"/> returns <see langword="null"/>, a state that is not its direct child, or an
        /// async-only one; that is known only once the selector has been entered, so the chain then ends at the selector.
        /// </exception>
        /// <exception cref="AggregateException">
        /// A state threw, and then <see cref="OnChangedState"/> or an extension's detach threw too; the state's exception comes first.
        /// Or several extensions threw while the change detached them.
        /// </exception>
        public void TransitionTo<TTarget>() where TTarget : IState =>
            TransitionTo(typeof(TTarget));

        /// <inheritdoc cref="TransitionTo{TTarget}"/>
        /// <param name="targetType">The target leaf state type.</param>
        /// <exception cref="ArgumentNullException"><paramref name="targetType"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="targetType"/> does not implement <see cref="IState"/>.</exception>
        public void TransitionTo(Type targetType)
        {
            ThrowIfNotStateType(targetType, nameof(targetType));

            // A disposed machine ignores state changes, also those a lifetime callback requests while Dispose runs.
            if (_isDisposed)
                return;

            ThrowIfAsyncTransitionInProgress();
            Request(new PendingRequest(targetType, PendingKind.TransitionTo));
        }

        private void ApplyTransitionTo(Type targetType)
        {
            var currentLeafType = _currentStates[^1].GetType();

            if (!IsStateEnabled(targetType) || !IsTransitionEnabled(currentLeafType, targetType))
                return;

            ThrowIfChangeNeedsAsync(targetType);
            var transition = ResolveTransition(currentLeafType, targetType);

            if (transition is not null)
            {
                if (!transition.CanTransition())
                    return;

                transition.OnBeforeTransition();

                // A change cut short by Dispose did not complete, as one that threw: no OnAfterTransition.
                if (ChangeStateCore(targetType))
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

            if (!ChangeStateCore(targetType))
                return;

            for (var i = chain.Count - 1; i >= 0; i--)
                chain[i].OnAfterTransition();
        }
        #endregion

        #region TransitionVia (sync)
        /// <inheritdoc />
        /// <remarks>
        /// A failure inside the state change itself stops it midway, as in <see cref="ChangeState{TState}"/>:
        /// exited states stay exited, and <see cref="OnChangedState"/> still runs before the exception propagates.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// A state on the path has an async-only enter or exit; checked before any transition hook, the chain is left unchanged.
        /// Or an <see cref="IEntrySelector"/> returns <see langword="null"/>, a state that is not its direct child, or an
        /// async-only one; that is known only once the selector has been entered, so the chain then ends at the selector.
        /// </exception>
        /// <exception cref="AggregateException">
        /// A state threw, and then <see cref="OnChangedState"/> or an extension's detach threw too; the state's exception comes first.
        /// Or several extensions threw while the change detached them.
        /// </exception>
        public void TransitionVia<TTransition>() where TTransition : ITransition =>
            TransitionVia(typeof(TTransition));

        /// <inheritdoc cref="TransitionVia{TTransition}"/>
        /// <param name="transitionType">The registered transition type to execute.</param>
        /// <exception cref="ArgumentNullException"><paramref name="transitionType"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="transitionType"/> does not implement <see cref="ITransition"/>.</exception>
        public void TransitionVia(Type transitionType)
        {
            ThrowIfNotTransitionType(transitionType);

            // A disposed machine ignores state changes, also those a lifetime callback requests while Dispose runs.
            if (_isDisposed)
                return;

            ThrowIfAsyncTransitionInProgress();
            Request(new PendingRequest(transitionType, PendingKind.TransitionVia));
        }

        private void ApplyTransitionVia(Type transitionType)
        {
            var transition = FindTransitionByType(transitionType);
            var currentLeafType = _currentStates[^1].GetType();

            if (!IsSourceActive(transition))
                return;

            if (!IsStateEnabled(transition.TargetState) ||
                !IsTransitionEnabled(currentLeafType, transition.TargetState))
                return;

            ThrowIfChangeNeedsAsync(transition.TargetState);

            if (!transition.CanTransition())
                return;

            transition.OnBeforeTransition();

            if (ChangeStateCore(transition.TargetState))
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
        /// Or an <see cref="IEntrySelector"/> returns <see langword="null"/> or a state that is not its direct child.
        /// </exception>
        /// <exception cref="TransitionSupersededException">
        /// A newer async transition superseded this one. Failures of its rollback are in
        /// <see cref="TransitionSupersededException.RollbackExceptions"/>.
        /// </exception>
        /// <exception cref="StateMachineDisposedException">
        /// The machine was disposed before the transition completed, also while it waited for an older one to unwind.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// <paramref name="ct"/> was cancelled, or an enter or exit callback cancelled itself.
        /// </exception>
        /// <exception cref="AggregateException">
        /// The transition failed or was cancelled by its own token or callback, and then a rollback exit,
        /// <see cref="OnChangedState"/> or an extension's detach threw too; the original exception comes first.
        /// Or several extensions threw while the transition detached them.
        /// </exception>
        public UniTask TransitionToAsync<TTarget>(CancellationToken ct = default)
            where TTarget : IState =>
            TransitionToAsync(typeof(TTarget), ct);

        /// <inheritdoc cref="TransitionToAsync{TTarget}"/>
        /// <param name="targetType">The target leaf state type.</param>
        /// <param name="ct">Cancellation token for the transition.</param>
        /// <exception cref="ArgumentNullException"><paramref name="targetType"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="targetType"/> does not implement <see cref="IState"/>.</exception>
        public async UniTask TransitionToAsync(Type targetType, CancellationToken ct = default)
        {
            ThrowIfNotStateType(targetType, nameof(targetType));

            // A disposed machine ignores state changes, also those a lifetime callback requests while Dispose runs.
            if (_isDisposed)
                return;

            ThrowIfSyncChangeInProgress();
            ct.ThrowIfCancellationRequested();

            if (!IsStateEnabled(targetType))
                return;

            // Resolve the edge only after the superseded transition has unwound, as ChangeStateAsync does.
            await SupersedeActiveTransitionAsync(targetType);
            ThrowIfEndedWhileWaiting(ct);
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
        /// Or an <see cref="IEntrySelector"/> returns <see langword="null"/> or a state that is not its direct child.
        /// </exception>
        /// <exception cref="TransitionSupersededException">
        /// A newer async transition superseded this one. Failures of its rollback are in
        /// <see cref="TransitionSupersededException.RollbackExceptions"/>.
        /// </exception>
        /// <exception cref="StateMachineDisposedException">
        /// The machine was disposed before the transition completed, also while it waited for an older one to unwind.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// <paramref name="ct"/> was cancelled, or an enter or exit callback cancelled itself.
        /// </exception>
        /// <exception cref="AggregateException">
        /// The transition failed or was cancelled by its own token or callback, and then a rollback exit,
        /// <see cref="OnChangedState"/> or an extension's detach threw too; the original exception comes first.
        /// Or several extensions threw while the transition detached them.
        /// </exception>
        public UniTask TransitionViaAsync<TTransition>(CancellationToken ct = default)
            where TTransition : ITransition =>
            TransitionViaAsync(typeof(TTransition), ct);

        /// <inheritdoc cref="TransitionViaAsync{TTransition}"/>
        /// <param name="transitionType">The registered transition type to execute.</param>
        /// <param name="ct">Cancellation token for the transition.</param>
        /// <exception cref="ArgumentNullException"><paramref name="transitionType"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="transitionType"/> does not implement <see cref="ITransition"/>.</exception>
        public async UniTask TransitionViaAsync(Type transitionType, CancellationToken ct = default)
        {
            ThrowIfNotTransitionType(transitionType);

            // A disposed machine ignores state changes, also those a lifetime callback requests while Dispose runs.
            if (_isDisposed)
                return;

            ThrowIfSyncChangeInProgress();
            ct.ThrowIfCancellationRequested();

            var transition = FindTransitionByType(transitionType);

            if (!IsStateEnabled(transition.TargetState))
                return;

            await SupersedeActiveTransitionAsync(transition.TargetState);
            ThrowIfEndedWhileWaiting(ct);
            var currentLeafType = _currentStates[^1].GetType();

            // Checked after the supersede, like the edge guard: the source must be active in the chain that the
            // superseded transition left, not in the one that was current when this call was made.
            if (!IsSourceActive(transition))
                return;

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

        // A transition runs only from its own source. The source counts as active anywhere in the chain: being in
        // one of its descendants means being in it. Under StrictTransitions a call from elsewhere is an error.
        private bool IsSourceActive(ITransition transition)
        {
            if (IndexOfActiveState(transition.SourceState) >= 0)
                return true;

            if (StrictTransitions)
                throw new InvalidOperationException(
                    $"Transition '{transition.GetType().Name}' leaves '{transition.SourceState.Name}', which is not " +
                    $"in the active chain (current leaf: '{_currentStates[^1].GetType().Name}'). " +
                    $"{nameof(StrictTransitions)} is enabled, so a transition may only run from its source state.");

            return false;
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
            _stateFactory.BuildTypeChain(targetType, _targetTypeChainBuffer);

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

        #endregion

        #region Helpers
        // Checked before a request is queued: a null type builds an empty chain, which exits every state and enters
        // none, and a type that is not a state exits the old chain before its creation fails.
        private static void ThrowIfNotStateType(Type stateType, string paramName)
        {
            if (stateType is null)
                throw new ArgumentNullException(paramName);

            if (!typeof(IState).IsAssignableFrom(stateType))
                throw new ArgumentException($"'{stateType}' does not implement {nameof(IState)}.", paramName);
        }

        private static void ThrowIfNotTransitionType(Type transitionType)
        {
            if (transitionType is null)
                throw new ArgumentNullException(nameof(transitionType));

            if (!typeof(ITransition).IsAssignableFrom(transitionType))
                throw new ArgumentException($"'{transitionType}' does not implement {nameof(ITransition)}.", nameof(transitionType));
        }

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
