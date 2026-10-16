#nullable enable
using System;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Defines a composite state that picks the child it enters when a transition targets the composite itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A state change that ends at this state enters it fully, enter controllers and async enter included, then enters
    /// the type returned by <see cref="SelectEntry"/> in the same transition, and repeats while the new leaf is a
    /// selector too. The transition ends, and <c>OnEntered</c> runs, only once a leaf that is not a selector is entered.
    /// </para>
    /// <para>
    /// A change that targets a descendant directly passes through this state without calling it, and a change between
    /// two of its children does not enter it again. A cancelled or superseded transition never reaches it.
    /// </para>
    /// </remarks>
    public interface IEntrySelector
    {
        /// <summary>
        /// Returns the child to enter after this state has been entered.
        /// </summary>
        /// <returns>A type that implements <see cref="IChildState{T}"/> of this state.</returns>
        Type SelectEntry();
    }
}
