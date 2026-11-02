#nullable enable
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
    /// A state change requested here is a new transition, not a redirect of the finished one. To wait for an event and
    /// then change state, implement <see cref="IAsyncEnteredController"/> instead: the machine runs it and reports its outcome.
    /// </para>
    /// <para>
    /// Only states of the active chain are notified. An <see cref="IExtensionState"/> is attached outside any transition,
    /// so it gets neither <see cref="OnEntered"/> nor a lifetime; use its enter and exit controllers instead.
    /// </para>
    /// </remarks>
    public interface IEnteredController : IController
    {
        /// <summary>
        /// Called once for each entry into the state, after the transition that entered it has completed.
        /// </summary>
        /// <param name="entry">
        /// The entry: its lifetime and the state changes started on its behalf. Their failures reach
        /// <see cref="IEnteredFailedController"/> only from an <see cref="IAsyncEnteredController"/> stage.
        /// </param>
        public void OnEntered(StateEntry entry);
    }
}
