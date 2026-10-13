#nullable enable
using System;
using System.Threading;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// <see cref="OperationCanceledException"/> for an async transition that the machine's <c>Dispose</c> cut short.
    /// </summary>
    /// <remarks>
    /// The cause is recorded when <c>Dispose</c> cancels the transition, before the machine releases its states. A
    /// transition requested before <c>Dispose</c> that was still waiting for an older one to unwind ends the same way.
    /// </remarks>
    public sealed class StateMachineDisposedException : OperationCanceledException
    {
        /// <param name="innerException">The cancellation the transition observed; <see langword="null"/> if it never started.</param>
        /// <param name="cancellationToken">The token of the transition.</param>
        public StateMachineDisposedException(Exception? innerException, CancellationToken cancellationToken)
            : base("The transition was cut short because the state machine was disposed.", innerException, cancellationToken)
        {
        }
    }
}
