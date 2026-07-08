// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Sentinel state used as the initial state of <see cref="StateMachineBase"/> before
    /// the first <see cref="StateMachineBase.ChangeState{TState}"/> call.
    /// </summary>
    public sealed class EmptyState : IState { }
}
