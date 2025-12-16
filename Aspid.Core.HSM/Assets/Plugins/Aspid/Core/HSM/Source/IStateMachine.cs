using System;
// ReSharper disable once CheckNamespace

namespace Aspid.Core.HSM
{
    public interface IStateMachine
    {
        public Type CurrentState { get; }
        
        public void ChangeState<T>()
            where T : IState;
    }
}
