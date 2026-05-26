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
                ChangeState<TTarget>();
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
                await ChangeStateAsync<TTarget>(ct);
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
