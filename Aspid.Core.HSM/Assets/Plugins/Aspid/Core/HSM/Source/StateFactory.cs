using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public abstract class StateFactory
    {
        public IEnumerable<IState> CreateState<TState>(IReadOnlyList<IState> activeStates)
            where TState : IState
        {
            return CreateState(typeof(TState), activeStates, index: activeStates.Count - 1);
        }

        private IEnumerable<IState> CreateState(Type type, IReadOnlyList<IState> activeStates, int index)
        {
            if (index >= 0 && type == activeStates[index].GetType())
            {
                for (var i = 0; i <= index; i++)
                    yield return activeStates[i];
            }
            else
            {
                var state = CreateStateInternal(type);
                
                if (state is IChildState childState)
                {
                    foreach (var parentState in CreateState(childState.ParentState, activeStates, --index))
                        yield return parentState;
                }
                
                yield return state;
            }
        }

        protected abstract IState CreateStateInternal(Type type);

        public abstract void Release(IState state);
    }
}
