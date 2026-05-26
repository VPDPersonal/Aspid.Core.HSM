using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public interface IStateMachine
    {
        IReadOnlyList<IState> CurrentStates { get; }

        void ChangeState<T>() where T : IState;

        void TransitionTo<TTarget>() where TTarget : IState;
        void TransitionVia<TTransition>() where TTransition : ITransition;
        UniTask TransitionToAsync<TTarget>(CancellationToken ct = default) where TTarget : IState;
        UniTask TransitionViaAsync<TTransition>(CancellationToken ct = default) where TTransition : ITransition;

        bool IsTransitioning { get; }
    }
}
