// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Extension methods for <see cref="IStateMachine"/>.
    /// </summary>
    public static class StateMachineExtensions
    {
        /// <summary>
        /// Returns the root (topmost) state in the active chain.
        /// </summary>
        public static IState GetParentState(this IStateMachine stateMachine) =>
            stateMachine.CurrentStates[0];

        /// <summary>
        /// Returns the leaf (deepest) state in the active chain.
        /// </summary>
        public static IState GetChildState(this IStateMachine stateMachine) =>
            stateMachine.CurrentStates[^1];
    }
}
