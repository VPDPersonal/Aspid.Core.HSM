// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Controller invoked when the state machine is disposed.
    /// Execution order is reversed (leaf-to-root) in controller groups.
    /// </summary>
    public interface IDisposableController : IController
    {
        /// <summary>
        /// Called once when the owning state machine is disposed.
        /// </summary>
        [ReverseExecute]
        public void Dispose();
    }
}
