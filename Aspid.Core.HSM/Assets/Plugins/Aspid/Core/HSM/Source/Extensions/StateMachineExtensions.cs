// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public static class StateMachineExtensions
    {
        public static IState GetParentState(this IStateMachine stateMachine) =>
            stateMachine.CurrentStates[0];
        
        public static IState GetChildState(this IStateMachine stateMachine) =>
            stateMachine.CurrentStates[^1];
    }
}
