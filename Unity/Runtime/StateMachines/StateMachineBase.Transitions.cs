using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public partial class StateMachineBase
    {
        private readonly Dictionary<(Type source, Type target), ITransition> _transitionRegistry = new();

        public bool IsTransitioning => _activeTransitionCts is not null;

        #region Registration
        public void RegisterTransition(ITransition transition)
        {
            var key = (transition.SourceState, transition.TargetState);
            _transitionRegistry[key] = transition;
        }

        public void RegisterTransition<TTransition>(TTransition transition)
            where TTransition : ITransition
        {
            RegisterTransition((ITransition)transition);
        }
        #endregion

        #region TransitionTo (sync)
        public void TransitionTo<TTarget>() where TTarget : IState
        {
            if (!IsStateEnabled(typeof(TTarget)))
                return;

            var targetType = typeof(TTarget);
            var currentLeafType = _currentStates[^1].GetType();

            var transition = ResolveTransition(currentLeafType, targetType);

            if (transition is not null)
            {
                if (!transition.CanTransition())
                    return;

                transition.OnBeforeTransition();
                ChangeState<TTarget>();
                transition.OnAfterTransition();
            }
            else
            {
                var chain = ResolveTransitionChain(targetType);

                if (chain is not null)
                {
                    foreach (var t in chain)
                    {
                        if (!t.CanTransition())
                            return;
                    }

                    foreach (var t in chain)
                        t.OnBeforeTransition();

                    ChangeState<TTarget>();

                    for (var i = chain.Count - 1; i >= 0; i--)
                        chain[i].OnAfterTransition();
                }
                else
                {
                    ChangeState<TTarget>();
                }
            }
        }
        #endregion

        #region TransitionVia (sync)
        public void TransitionVia<TTransition>() where TTransition : ITransition
        {
            var transition = FindTransitionByType<TTransition>();

            if (!transition.CanTransition())
                return;

            transition.OnBeforeTransition();
            ChangeStateByType(transition.TargetState);
            transition.OnAfterTransition();
        }
        #endregion

        #region TransitionTo (async)
        public async UniTask TransitionToAsync<TTarget>(CancellationToken ct = default)
            where TTarget : IState
        {
            var targetType = typeof(TTarget);
            var currentLeafType = _currentStates[^1].GetType();

            var transition = ResolveTransition(currentLeafType, targetType);

            if (transition is not null)
            {
                if (!transition.CanTransition())
                    return;

                transition.OnBeforeTransition();
                await ChangeStateAsync<TTarget>(ct);
                transition.OnAfterTransition();
            }
            else
            {
                var chain = ResolveTransitionChain(targetType);

                if (chain is not null)
                {
                    foreach (var t in chain)
                    {
                        if (!t.CanTransition())
                            return;
                    }

                    foreach (var t in chain)
                        t.OnBeforeTransition();

                    await ChangeStateAsync<TTarget>(ct);

                    for (var i = chain.Count - 1; i >= 0; i--)
                        chain[i].OnAfterTransition();
                }
                else
                {
                    await ChangeStateAsync<TTarget>(ct);
                }
            }
        }
        #endregion

        #region TransitionVia (async)
        public async UniTask TransitionViaAsync<TTransition>(CancellationToken ct = default)
            where TTransition : ITransition
        {
            var transition = FindTransitionByType<TTransition>();

            if (!transition.CanTransition())
                return;

            transition.OnBeforeTransition();
            await ChangeStateAsyncByType(transition.TargetState, ct);
            transition.OnAfterTransition();
        }
        #endregion

        #region Resolution
        protected virtual ITransition? ResolveTransition(Type sourceType, Type targetType)
        {
            var key = (sourceType, targetType);
            return _transitionRegistry.TryGetValue(key, out var transition) ? transition : null;
        }

        private ITransition FindTransitionByType<TTransition>() where TTransition : ITransition
        {
            var transitionType = typeof(TTransition);

            foreach (var transition in _transitionRegistry.Values)
            {
                if (transition.GetType() == transitionType)
                    return transition;
            }

            throw new InvalidOperationException(
                $"Transition of type '{transitionType.Name}' is not registered.");
        }
        #endregion

        #region Chain Resolution

        private readonly List<Type> _targetTypeChainBuffer = new(capacity: 4);

        private List<ITransition>? ResolveTransitionChain(Type targetType)
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
            var pathTypes = new List<Type>();

            for (var i = currentCount - 1; i >= divergeIndex; i--)
                pathTypes.Add(_currentStates[i].GetType());

            for (var i = divergeIndex; i < _targetTypeChainBuffer.Count; i++)
                pathTypes.Add(_targetTypeChainBuffer[i]);

            // Look up transitions for each consecutive pair
            List<ITransition>? chain = null;

            for (var i = 0; i < pathTypes.Count - 1; i++)
            {
                var key = (pathTypes[i], pathTypes[i + 1]);
                if (_transitionRegistry.TryGetValue(key, out var segmentTransition))
                {
                    chain ??= new List<ITransition>();
                    chain.Add(segmentTransition);
                }
            }

            return chain;
        }

        private static void BuildTypeChain(Type leafType, List<Type> result)
        {
            if (typeof(IChildState).IsAssignableFrom(leafType))
            {
                var instance = (IChildState)Activator.CreateInstance(leafType)!;
                BuildTypeChain(instance.ParentState, result);
            }

            result.Add(leafType);
        }

        #endregion

        #region Helpers
        private void ChangeStateByType(Type targetStateType)
        {
            var method = typeof(StateMachineBase).GetMethod(nameof(ChangeState))!
                .MakeGenericMethod(targetStateType);
            method.Invoke(this, null);
        }

        private UniTask ChangeStateAsyncByType(Type targetStateType, CancellationToken ct)
        {
            var method = typeof(StateMachineBase).GetMethod(nameof(ChangeStateAsync))!
                .MakeGenericMethod(targetStateType);
            return (UniTask)method.Invoke(this, new object[] { ct })!;
        }
        #endregion
    }
}
