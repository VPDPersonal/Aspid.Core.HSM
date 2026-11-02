#nullable enable
using Cysharp.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Defines the async counterpart of <see cref="IEnteredController"/>: a stage that runs on after the transition that
    /// entered its state has completed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The machine starts it where it would call <see cref="IEnteredController.OnEntered"/>, and does not await it: the
    /// transition is over by then. A state that implements both is started through this interface only, the way a
    /// <c>[ControllerGroup]</c> with one async step implements it.
    /// </para>
    /// <para>
    /// In a <c>[ControllerGroup]</c> the steps run one after another in <c>AddControllers</c> order, with synchronous
    /// <see cref="IEnteredController"/> steps between them, unless <c>[AsyncMode]</c> says otherwise.
    /// </para>
    /// <para>
    /// The machine also owns the outcome. Silent ends: the entry's lifetime cancelled, a
    /// <see cref="TransitionSupersededException"/>, a <see cref="StateMachineDisposedException"/>. Any other failure,
    /// including a transition started through <see cref="StateEntry"/> that failed after the state had exited, goes to
    /// <see cref="IEnteredFailedController"/>.
    /// </para>
    /// </remarks>
    [AsyncOf(typeof(IEnteredController), DefaultMode = AsyncExecutionMode.Sequential)]
    public interface IAsyncEnteredController : IController
    {
        /// <summary>
        /// Called once for each entry into the state, after the transition that entered it has completed.
        /// </summary>
        /// <param name="entry">The entry: its lifetime and the state changes the stage starts.</param>
        UniTask OnEnteredAsync(StateEntry entry);
    }
}
