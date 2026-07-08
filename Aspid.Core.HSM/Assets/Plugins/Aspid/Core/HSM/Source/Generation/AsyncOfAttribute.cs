using System;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Links an async controller interface to its synchronous counterpart.
    /// Used by the source generator to pair sync/async controller pairs
    /// (e.g. <see cref="IAsyncEnterController"/> → <see cref="IEnterController"/>).
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface)]
    public sealed class AsyncOfAttribute : Attribute
    {
        /// <summary>
        /// The synchronous controller interface that this async interface replaces.
        /// </summary>
        public Type SyncInterface { get; }

        /// <param name="syncInterface">The synchronous controller interface type.</param>
        public AsyncOfAttribute(Type syncInterface)
        {
            SyncInterface = syncInterface;
        }
    }
}
