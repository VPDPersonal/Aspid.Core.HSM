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
    /// A transition started through this entry from an <see cref="IAsyncEnteredController"/> stage is known to belong to
    /// the stage. When it fails, the failure is reported to <see cref="IEnteredFailedController"/> even after the state has
    /// exited, while the stage's own cancellation, caused by that exit, stays silent. A transition started on the machine
    /// directly, or from <see cref="IEnteredController"/>, has no such link.
    /// </para>
    /// <para>
    /// Once <see cref="Lifetime"/> is cancelled, the entry starts no transition: it returns a task cancelled by
    /// <see cref="Lifetime"/>, so a stage that outlives its state cannot move the machine away from a later state.
    /// </para>
    /// <para>
    /// Do not pass <see cref="Lifetime"/>, or a token linked to it, to the next transition: that transition exits the
    /// state and so cancels itself.
    /// </para>
    /// </remarks>
    public sealed class StateEntry
    {
        private readonly StateMachineBase _stateMachine;

        // The cancellation that ended the last transition started through this entry; null when it completed, failed
        // otherwise or ended silently.
        private OperationCanceledException? _transitionCancellation;

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
            Lifetime.IsCancellationRequested
                ? UniTask.FromCanceled(Lifetime)
                : TrackAsync(_stateMachine.ChangeStateAsync<TState>(cancellationToken));

        /// <summary>
        /// Transitions to <typeparamref name="TTarget"/> through <see cref="IStateMachine.TransitionToAsync{TTarget}"/> on
        /// behalf of the stage.
        /// </summary>
        /// <typeparam name="TTarget">The target leaf state type.</typeparam>
        /// <param name="cancellationToken">Cancels the transition; not <see cref="Lifetime"/>.</param>
        public UniTask TransitionToAsync<TTarget>(CancellationToken cancellationToken = default)
            where TTarget : IState =>
            Lifetime.IsCancellationRequested
                ? UniTask.FromCanceled(Lifetime)
                : TrackAsync(_stateMachine.TransitionToAsync<TTarget>(cancellationToken));

        /// <summary>
        /// Executes <typeparamref name="TTransition"/> through <see cref="IStateMachine.TransitionViaAsync{TTransition}"/> on
        /// behalf of the stage.
        /// </summary>
        /// <typeparam name="TTransition">The registered transition type to execute.</typeparam>
        /// <param name="cancellationToken">Cancels the transition; not <see cref="Lifetime"/>.</param>
        public UniTask TransitionViaAsync<TTransition>(CancellationToken cancellationToken = default)
            where TTransition : ITransition =>
            Lifetime.IsCancellationRequested
                ? UniTask.FromCanceled(Lifetime)
                : TrackAsync(_stateMachine.TransitionViaAsync<TTransition>(cancellationToken));

        // The cancellation of the last transition started through this entry, when exception is that cancellation.
        // A plain OperationCanceledException is created anew at every await but keeps its token, so it is matched by
        // the token: a cancellation of the stage's own wait, by the lifetime or a token linked to it, never matches.
        internal OperationCanceledException? FindTransitionCancellation(OperationCanceledException exception) =>
            _transitionCancellation is { } cancellation && cancellation.CancellationToken == exception.CancellationToken
                ? cancellation
                : null;

        private async UniTask TrackAsync(UniTask transition)
        {
            // Per attempt: only the last transition can stand for the cancellation that ends the stage.
            _transitionCancellation = null;
            try
            {
                await transition;
            }
            catch (OperationCanceledException exception) when (exception is not TransitionSupersededException and not StateMachineDisposedException)
            {
                _transitionCancellation = exception;
                throw;
            }
        }
    }
}
