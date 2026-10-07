#nullable enable

using System;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Runtime view of a state's parent in the hierarchy. Do not implement it directly: implement
    /// <see cref="IChildState{T}"/>, which supplies <see cref="ParentState"/>. <see cref="StateFactory"/> reads the
    /// parent from the type, before any instance exists, and rejects a state implementing only this interface.
    /// </summary>
    public interface IChildState
    {
        /// <summary>
        /// The parent state type. Must implement <see cref="IState"/>.
        /// </summary>
        public Type ParentState { get; }
    }

    /// <summary>
    /// Declares <typeparamref name="T"/> as the state's parent. The state is created inside a child of the
    /// parent's scope, so it can depend on anything the parent registers there. An extension state implementing
    /// it is scoped under <typeparamref name="T"/> the same way, attaches only while <typeparamref name="T"/> is
    /// active and is detached before <typeparamref name="T"/> exits.
    /// </summary>
    /// <typeparam name="T">The parent state type.</typeparam>
    public interface IChildState<T> : IChildState
        where T : IState
    {
        Type IChildState.ParentState => typeof(T);
    }
}
