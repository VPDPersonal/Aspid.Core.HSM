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
    }
}
