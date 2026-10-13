#nullable enable
using System.Threading;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Controller invoked when a state is entered. Called after <see cref="IState.Enter"/>.
    /// </summary>
    public interface IEnterController : IController
    {
        /// <summary>
        /// Called once each time the state is entered.
        /// </summary>
        public void OnEnter();
    }

    /// <summary>
    /// Defines a controller invoked once the transition that entered its state has completed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Runs after the whole chain is entered and <see cref="IStateMachine.IsTransitioning"/> is <see langword="false"/>,
    /// from root to leaf, after <see cref="IStateMachine.ChangeState{T}"/> and <see cref="IStateMachine.ChangeStateAsync{T}"/>
    /// alike. It does not run for a transition that throws, is cancelled or rolls back, nor for a state the transition's
    /// queued follow-ups exited again.
    /// </para>
    /// <para>
    /// A state change requested here is a new transition, not a redirect of the finished one. To wait for an event,
    /// start the wait here and pass it <c>stateLifetime</c>.
    /// </para>
    /// </remarks>
    public interface IEnteredController : IController
    {
        /// <summary>
        /// Called once for each entry into the state, after the transition that entered it has completed.
        /// </summary>
        /// <param name="stateLifetime">
        /// Cancelled when the state starts to exit, before its exit controllers run, and when the machine is disposed.
        /// </param>
        public void OnEntered(CancellationToken stateLifetime);
    }
}
