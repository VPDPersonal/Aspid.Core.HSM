#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Represents one entry into a state: its lifetime, and the state changes the stage that runs after the enter starts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A transition started through this entry is known to belong to the stage. When it fails, the failure is reported to
    /// <see cref="IEnteredFailedController"/> even after the state has exited, while the stage's own cancellation, caused
    /// by that exit, stays silent. A transition started on the machine directly has no such link: its failure after the
    /// exit looks like the stage's cancellation.
    /// </para>
    /// <para>
    /// Do not pass <see cref="Lifetime"/> to the next transition: that transition exits the state and cancels it.
    /// </para>
    /// </remarks>
    public sealed class StateEntry
    {
        private readonly StateMachineBase _stateMachine;

        // The failure of the last transition started through this entry; null when it completed or ended silently.
        // Kept as found: a plain OperationCanceledException is created anew at every await, so it cannot be matched by reference.
        internal Exception? TransitionFailure { get; private set; }

        internal StateEntry(StateMachineBase stateMachine, CancellationToken lifetime)
        {
            _stateMachine = stateMachine;
            Lifetime = lifetime;
        }

        /// <summary>
        /// Gets the token cancelled when the state starts to exit, before its exit controllers run, and when the machine is
        /// disposed.
        /// </summary>
        public CancellationToken Lifetime { get; }

        /// <summary>
        /// Changes to <typeparamref name="TState"/> through <see cref="IStateMachine.ChangeStateAsync{T}"/> on behalf of the stage.
        /// </summary>
        /// <typeparam name="TState">The target leaf state type.</typeparam>
        /// <param name="cancellationToken">Cancels the transition; not <see cref="Lifetime"/>.</param>
        public UniTask ChangeStateAsync<TState>(CancellationToken cancellationToken = default)
            where TState : IState =>
            TrackAsync(_stateMachine.ChangeStateAsync<TState>(cancellationToken));

        /// <summary>
        /// Transitions to <typeparamref name="TTarget"/> through <see cref="IStateMachine.TransitionToAsync{TTarget}"/> on
        /// behalf of the stage.
        /// </summary>
        /// <typeparam name="TTarget">The target leaf state type.</typeparam>
        /// <param name="cancellationToken">Cancels the transition; not <see cref="Lifetime"/>.</param>
        public UniTask TransitionToAsync<TTarget>(CancellationToken cancellationToken = default)
            where TTarget : IState =>
            TrackAsync(_stateMachine.TransitionToAsync<TTarget>(cancellationToken));

        /// <summary>
        /// Executes <typeparamref name="TTransition"/> through <see cref="IStateMachine.TransitionViaAsync{TTransition}"/> on
        /// behalf of the stage.
        /// </summary>
        /// <typeparam name="TTransition">The registered transition type to execute.</typeparam>
        /// <param name="cancellationToken">Cancels the transition; not <see cref="Lifetime"/>.</param>
        public UniTask TransitionViaAsync<TTransition>(CancellationToken cancellationToken = default)
            where TTransition : ITransition =>
            TrackAsync(_stateMachine.TransitionViaAsync<TTransition>(cancellationToken));

        // The stage's own wait was cancelled by its lifetime: the token says so. Without that token, it is the stage's
        // cancellation only when no transition it started has failed since the last attempt.
        internal bool IsStageCancellation(OperationCanceledException exception) =>
            Lifetime.IsCancellationRequested &&
            (exception.CancellationToken == Lifetime || TransitionFailure is null);

        private async UniTask TrackAsync(UniTask transition)
        {
            // Per attempt: a failure the stage caught and handled must not be reported later.
            TransitionFailure = null;
            try
            {
                await transition;
            }
            catch (Exception exception) when (exception is not TransitionSupersededException and not StateMachineDisposedException)
            {
                TransitionFailure = exception;
                throw;
            }
        }
    }
}
