// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Controller invoked when a state is exited. Called before <see cref="IState.Exit"/>.
    /// Execution order is reversed (leaf-to-root) in controller groups.
    /// </summary>
    public interface IExitController : IController
    {
        /// <summary>
        /// Called once each time the state is exited.
        /// </summary>
        [ReverseExecute]
        public void OnExit();
    }
}
