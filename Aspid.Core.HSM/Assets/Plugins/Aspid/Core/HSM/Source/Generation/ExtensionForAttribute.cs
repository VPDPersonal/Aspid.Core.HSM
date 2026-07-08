using System;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Declares which states an <see cref="IExtensionState"/> is compatible with.
    /// The source generator uses this to implement <see cref="IExtensionState.CanAttachTo"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ExtensionForAttribute : Attribute
    {
        /// <summary>
        /// The set of state types this extension can be attached to.
        /// </summary>
        public Type[] CompatibleStates { get; }

        /// <param name="compatibleStates">One or more state types this extension supports.</param>
        public ExtensionForAttribute(params Type[] compatibleStates)
        {
            CompatibleStates = compatibleStates;
        }
    }
}
