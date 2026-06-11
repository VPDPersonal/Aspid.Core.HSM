using System.Threading;
using Cysharp.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Async variant of <see cref="IEnterController"/>.
    /// When a state implements this interface, <see cref="StateMachineBase.ChangeStateAsync{TState}"/>
    /// awaits <see cref="OnEnterAsync"/> instead of calling <see cref="IEnterController.OnEnter"/>.
    /// </summary>
    [AsyncOf(typeof(IEnterController))]
    public interface IAsyncEnterController : IController
    {
        /// <summary>
        /// Called once each time the state is entered during an async state change.
        /// </summary>
        /// <param name="cancellationToken">Cancelled if a newer state change supersedes this one.</param>
        public UniTask OnEnterAsync(CancellationToken cancellationToken);
    }
}
