using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public abstract class StateFactory
    {
        public IEnumerable<IState> CreateState<TState>()
            where TState : IState
        {
            return CreateState(typeof(TState));
        }

        private IEnumerable<IState> CreateState(Type type)
        {
            var state = CreateStateInternal(type);
            
            if (state is IChildState childState)
            {
                foreach (var parentState in CreateState(childState.ParentState))
                    yield return parentState;
            }
            
            yield return state;
        }

        protected abstract IState CreateStateInternal(Type type);

        public abstract void Release(IState state);
    }
}
