#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// <see cref="IEnteredController"/> that runs an async stage once its state is entered and leaves it with
    /// <see cref="ChangeStateAsync{TState}"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ends that need no handling stay silent: the state exited (<c>stateLifetime</c> cancelled), the next
    /// transition was superseded by a newer one (<see cref="TransitionSupersededException"/>), or the machine was disposed
    /// (<see cref="StateMachineDisposedException"/>). Everything else, including a next transition that failed or whose
    /// enter cancelled itself, goes to <see cref="OnFailed"/>, where the policy belongs to the consumer.
    /// </para>
    /// <para>
    /// Do not pass <c>stateLifetime</c> to the next transition: that transition exits this state and cancels it.
    /// </para>
    /// </remarks>
    public abstract class AsyncEnteredController : IEnteredController
    {
        private readonly IStateMachine _stateMachine;

        // The failure of a transition this stage started, told apart from a cancellation of the stage itself. Kept
        // as found: a plain OperationCanceledException is created anew at every await, so it cannot be matched by reference.
        private Exception? _transitionFailure;

        /// <param name="stateMachine">The machine the stage changes state on.</param>
        protected AsyncEnteredController(IStateMachine stateMachine)
        {
            _stateMachine = stateMachine;
        }

        /// <inheritdoc />
        void IEnteredController.OnEntered(CancellationToken stateLifetime) =>
            RunGuardedAsync(stateLifetime).Forget();

        /// <summary>
        /// Called once the transition that entered the state has completed. Override to wait for the stage's event and
        /// then change state.
        /// </summary>
        /// <param name="stateLifetime">Cancelled when the state starts to exit and when the machine is disposed.</param>
        protected abstract UniTask RunAsync(CancellationToken stateLifetime);

        /// <summary>
        /// Called when <see cref="RunAsync"/> or a transition it started fails. Override to apply the consumer's policy,
        /// for example an emergency exit.
        /// </summary>
        /// <param name="exception">The failure, a plain <see cref="OperationCanceledException"/> included when a
        /// transition's enter cancelled itself.</param>
        protected abstract void OnFailed(Exception exception);

        /// <summary>
        /// Changes to <typeparamref name="TState"/> through <see cref="IStateMachine.ChangeStateAsync{T}"/>, so that
        /// <see cref="OnFailed"/> sees its failure even after this state has exited.
        /// </summary>
        /// <typeparam name="TState">The target leaf state type.</typeparam>
        /// <param name="cancellationToken">Cancels the transition; not <c>stateLifetime</c>.</param>
        protected async UniTask ChangeStateAsync<TState>(CancellationToken cancellationToken = default)
            where TState : IState
        {
            try
            {
                await _stateMachine.ChangeStateAsync<TState>(cancellationToken);
            }
            catch (Exception exception) when (exception is not TransitionSupersededException and not StateMachineDisposedException)
            {
                _transitionFailure = exception;
                throw;
            }
        }

        private async UniTaskVoid RunGuardedAsync(CancellationToken stateLifetime)
        {
            _transitionFailure = null;
            try
            {
                await RunAsync(stateLifetime);
            }
            catch (TransitionSupersededException) { }
            catch (StateMachineDisposedException) { }
            catch (OperationCanceledException) when (stateLifetime.IsCancellationRequested && _transitionFailure is null)
            {
                // The state exited or the machine was disposed while the stage waited.
            }
            catch (Exception exception)
            {
                OnFailed(_transitionFailure ?? exception);
            }
        }
    }
}
