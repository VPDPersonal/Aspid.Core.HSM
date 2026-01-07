using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public interface IStateMachine
    {
        public IReadOnlyList<IState> CurrentStates { get; }
        
        public void ChangeState<T>()
            where T : IState;
    }
}
