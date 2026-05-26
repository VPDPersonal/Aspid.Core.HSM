using System.Threading;
using Cysharp.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public partial class MonoStateMachine
    {
        public UniTask ChangeStateAsync<TState>(CancellationToken cancellationToken = default)
            where TState : IState =>
            _stateMachine!.ChangeStateAsync<TState>(cancellationToken);

        public UniTask TransitionToAsync<TTarget>(CancellationToken ct = default)
            where TTarget : IState =>
            _stateMachine!.TransitionToAsync<TTarget>(ct);

        public UniTask TransitionViaAsync<TTransition>(CancellationToken ct = default)
            where TTransition : ITransition =>
            _stateMachine!.TransitionViaAsync<TTransition>(ct);
    }
}
