#nullable enable

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// A state that can be dynamically attached to and detached from the state machine
    /// independently of the main state hierarchy. Extensions participate in update loops
    /// and are auto-detached when incompatible with the current leaf state.
    /// </summary>
    public interface IExtensionState : IState
    {
        /// <summary>
        /// Determines whether this extension can be attached when the given host state is the current leaf.
        /// Also evaluated on every state change to auto-detach incompatible extensions.
        /// </summary>
        /// <param name="hostState">The current leaf state of the state machine.</param>
        /// <returns><c>true</c> if the extension is compatible with the host state.</returns>
        public bool CanAttachTo(IState hostState);

        /// <summary>
        /// Called after the extension has been entered and attached to the state machine.
        /// </summary>
        /// <param name="hostState">The current leaf state at the time of attachment.</param>
        public void OnAttached(IState hostState) { }

        /// <summary>
        /// Called before the extension is exited and released from the state machine.
        /// </summary>
        /// <param name="hostState">The current leaf state at the time of detachment.</param>
        public void OnDetached(IState hostState) { }
    }
}
