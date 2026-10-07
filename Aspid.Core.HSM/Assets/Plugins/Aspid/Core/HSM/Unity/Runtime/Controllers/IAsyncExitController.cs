using System.Threading;
using Cysharp.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Async variant of <see cref="IExitController"/>.
    /// When a state implements this interface, <see cref="StateMachineBase.ChangeStateAsync{TState}"/>
    /// awaits <see cref="OnExitAsync"/> instead of calling <see cref="IExitController.OnExit"/>.
    /// </summary>
    [AsyncOf(typeof(IExitController))]
    public interface IAsyncExitController : IController
    {
        /// <summary>
        /// Called once each time the state is exited during an async state change.
        /// </summary>
        /// <param name="cancellationToken">Cancelled if a newer state change supersedes this one.</param>
        [ReverseExecute]
        public UniTask OnExitAsync(CancellationToken cancellationToken);
    }
}
