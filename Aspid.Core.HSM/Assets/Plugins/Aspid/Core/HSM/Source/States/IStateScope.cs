#nullable enable
using System;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Represents a dependency injection scope tied to a state's lifetime.
    /// Scopes form a hierarchy mirroring the state tree: a child state's scope
    /// is created from its parent state's scope.
    /// </summary>
    public interface IStateScope : IDisposable
    {
        /// <summary>
        /// The parent scope, or <c>null</c> if this is the root scope.
        /// </summary>
        IStateScope? Parent { get; }

        /// <summary>
        /// Creates a new child scope whose lifetime is bounded by this scope.
        /// </summary>
        IStateScope CreateChildScope();
    }
}
