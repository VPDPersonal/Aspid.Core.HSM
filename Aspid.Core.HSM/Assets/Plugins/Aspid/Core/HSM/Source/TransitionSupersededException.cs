#nullable enable
using System;
using System.Threading;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// <see cref="OperationCanceledException"/> for an async transition that a newer one superseded before it completed.
    /// </summary>
    /// <remarks>
    /// The cause is recorded when the newer transition cancels this one, so the exception does not depend on whether
    /// the newer transition is still running when it reaches the caller. Nothing needs handling: the newer transition
    /// owns the machine.
    /// </remarks>
    public sealed class TransitionSupersededException : OperationCanceledException
    {
        /// <summary>
        /// Gets the target state type of the transition that superseded this one.
        /// </summary>
        public Type SupersedingState { get; }

        /// <summary>
        /// Gets the exceptions thrown while the superseded transition rolled back; empty when the rollback succeeded.
        /// </summary>
        public IReadOnlyList<Exception> RollbackExceptions { get; }

        /// <param name="supersedingState">The target state type of the transition that superseded this one.</param>
        /// <param name="rollbackExceptions">The exceptions thrown while the superseded transition rolled back.</param>
        /// <param name="innerException">The cancellation the superseded transition observed.</param>
        /// <param name="cancellationToken">The token of the superseded transition.</param>
        public TransitionSupersededException(
            Type supersedingState,
            IReadOnlyList<Exception> rollbackExceptions,
            Exception? innerException,
            CancellationToken cancellationToken)
            : base(
                $"The transition was superseded by a transition to '{supersedingState.Name}'" +
                (rollbackExceptions.Count > 0 ? $", and its rollback threw {rollbackExceptions.Count} exception(s)." : "."),
                innerException,
                cancellationToken)
        {
            SupersedingState = supersedingState;
            RollbackExceptions = rollbackExceptions;
        }
    }
}
