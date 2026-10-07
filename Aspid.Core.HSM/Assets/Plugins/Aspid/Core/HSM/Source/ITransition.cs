using System;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// A guarded transition between two states. Transitions are registered on the state machine
    /// and resolved automatically by <see cref="IStateMachine.TransitionTo{TTarget}"/>
    /// and <see cref="IStateMachine.TransitionVia{TTransition}"/>.
    /// </summary>
    public interface ITransition
    {
        /// <summary>
        /// The state type this transition originates from.
        /// </summary>
        Type SourceState { get; }

        /// <summary>
        /// The state type this transition leads to.
        /// </summary>
        Type TargetState { get; }

        /// <summary>
        /// Guard condition evaluated before the transition executes. Return <c>false</c> to block the transition.
        /// </summary>
        bool CanTransition() => true;

        /// <summary>
        /// Called after the guard passes but before the state change occurs.
        /// </summary>
        void OnBeforeTransition() { }

        /// <summary>
        /// Called after the state change has completed.
        /// </summary>
        void OnAfterTransition() { }
    }

    /// <summary>
    /// Strongly-typed variant of <see cref="ITransition"/> that derives
    /// <see cref="ITransition.SourceState"/> and <see cref="ITransition.TargetState"/> from type parameters.
    /// </summary>
    /// <typeparam name="TSource">The source state type.</typeparam>
    /// <typeparam name="TTarget">The target state type.</typeparam>
    public interface ITransition<TSource, TTarget> : ITransition
        where TSource : IState
        where TTarget : IState
    {
        Type ITransition.SourceState => typeof(TSource);
        Type ITransition.TargetState => typeof(TTarget);
    }
}
