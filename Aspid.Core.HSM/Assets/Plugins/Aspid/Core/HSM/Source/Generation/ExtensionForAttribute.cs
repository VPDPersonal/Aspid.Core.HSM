#nullable enable
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

        /// <summary>
        /// Gets or sets a value indicating whether the extension also attaches below the listed states. When
        /// <see langword="true"/>, a listed state anywhere in the active chain is enough, so the extension stays attached
        /// while the leaf changes inside it and is detached once it exits; otherwise, a listed state must be the leaf.
        /// Either way a listed type also matches its derived types and implementations, as <see langword="is"/> does.
        /// </summary>
        public bool IncludeDescendants { get; set; }

        /// <param name="compatibleStates">One or more state types this extension supports.</param>
        public ExtensionForAttribute(params Type[] compatibleStates)
        {
            CompatibleStates = compatibleStates;
        }
    }
}
