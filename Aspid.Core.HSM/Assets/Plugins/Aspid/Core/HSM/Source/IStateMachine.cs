using System.Threading;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Public contract for a hierarchical state machine. Exposes the active state chain,
    /// state changes, guarded transitions, and extension state management.
    /// </summary>
    public interface IStateMachine
    {
        /// <summary>
        /// The currently active state chain ordered from root to leaf.
        /// </summary>
        IReadOnlyList<IState> CurrentStates { get; }

        /// <summary>
        /// Immediately transitions to the state <typeparamref name="T"/> by diffing the
        /// new chain against the current one, exiting removed states and entering added states.
        /// </summary>
        /// <typeparam name="T">The target leaf state type.</typeparam>
        void ChangeState<T>() where T : IState;

        /// <summary>
        /// Transitions to <typeparamref name="TTarget"/>, executing any registered
        /// <see cref="ITransition"/> guards and hooks along the path.
        /// </summary>
        /// <typeparam name="TTarget">The target leaf state type.</typeparam>
        void TransitionTo<TTarget>() where TTarget : IState;

        /// <summary>
        /// Executes a specific registered transition by type, transitioning to its
        /// <see cref="ITransition.TargetState"/> if <see cref="ITransition.CanTransition"/> returns <c>true</c>.
        /// </summary>
        /// <typeparam name="TTransition">The transition type to execute.</typeparam>
        void TransitionVia<TTransition>() where TTransition : ITransition;

        /// <inheritdoc cref="TransitionTo{TTarget}"/>
        UniTask TransitionToAsync<TTarget>(CancellationToken ct = default) where TTarget : IState;

        /// <inheritdoc cref="TransitionVia{TTransition}"/>
        UniTask TransitionViaAsync<TTransition>(CancellationToken ct = default) where TTransition : ITransition;

        /// <summary>
        /// <c>true</c> while an async state change or transition is in progress.
        /// </summary>
        bool IsTransitioning { get; }

        /// <summary>
        /// Extension states currently attached to the state machine.
        /// </summary>
        IReadOnlyList<IExtensionState> ActiveExtensions { get; }

        /// <summary>
        /// Creates and attaches an extension state if it is compatible with the current leaf state.
        /// </summary>
        /// <typeparam name="T">The extension state type.</typeparam>
        void AttachExtension<T>() where T : IExtensionState;

        /// <summary>
        /// Detaches and releases the first active extension of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The extension state type to detach.</typeparam>
        void DetachExtension<T>() where T : IExtensionState;
    }
}
