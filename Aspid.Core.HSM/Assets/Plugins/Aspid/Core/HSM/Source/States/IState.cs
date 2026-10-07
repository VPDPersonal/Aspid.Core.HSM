// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Base interface for all states in the hierarchical state machine.
    /// </summary>
    public interface IState
    {
        /// <summary>
        /// Called when the state is entered during a state change.
        /// Invoked after <see cref="StateFactory.MarkInitialized"/> and before <see cref="IEnterController.OnEnter"/>.
        /// </summary>
        public void Enter() { }

        /// <summary>
        /// Called when the state is exited during a state change.
        /// Invoked after <see cref="IExitController.OnExit"/> and before <see cref="StateFactory.Release"/>.
        /// </summary>
        public void Exit() { }
    }
}
