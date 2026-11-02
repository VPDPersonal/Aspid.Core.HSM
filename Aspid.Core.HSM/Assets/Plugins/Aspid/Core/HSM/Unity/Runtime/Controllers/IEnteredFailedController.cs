#nullable enable
using System;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Defines a controller that handles a failed <see cref="IAsyncEnteredController"/> stage of its state or of any state below it.
    /// </summary>
    /// <remarks>
    /// The machine looks for it when the stage starts: on the stage's state, then on the states above it, nearest first.
    /// When the stage fails, it calls the one it found, even if that state has exited since; with none, the machine's own
    /// fallback runs. One handler on the root of a branch covers every stage under it, also when the failed transition was
    /// leaving the branch. In a <c>[ControllerGroup]</c> it is called after the group's exit too.
    /// </remarks>
    public interface IEnteredFailedController : IController
    {
        /// <summary>
        /// Called when an entered stage fails.
        /// </summary>
        /// <param name="state">The state whose stage failed; it may have exited already.</param>
        /// <param name="exception">The failure; a plain <see cref="OperationCanceledException"/> when a transition the stage
        /// started cancelled itself.</param>
        void OnEnteredFailed(IState state, Exception exception);
    }
}
