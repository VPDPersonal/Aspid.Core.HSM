using System;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Declares the source and target state types for a transition class.
    /// The source generator uses this to implement <see cref="ITransition.SourceState"/>
    /// and <see cref="ITransition.TargetState"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class TransitionAttribute : Attribute
    {
        /// <summary>
        /// The state type this transition originates from.
        /// </summary>
        public Type SourceState { get; }

        /// <summary>
        /// The state type this transition leads to.
        /// </summary>
        public Type TargetState { get; }

        /// <param name="sourceState">The source state type.</param>
        /// <param name="targetState">The target state type.</param>
        public TransitionAttribute(Type sourceState, Type targetState)
        {
            SourceState = sourceState;
            TargetState = targetState;
        }
    }
}
