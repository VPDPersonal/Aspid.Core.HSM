using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public abstract class StateFactory
    {
        private readonly HashSet<Type> _initializedStates = new();
        private readonly List<IState> _chainBuffer = new(capacity: 4);

        public IReadOnlyList<IState> CreateState<TState>(IReadOnlyList<IState> activeStates)
            where TState : IState
        {
            _chainBuffer.Clear();
            BuildChain(typeof(TState), activeStates, activeStates.Count - 1);
            return _chainBuffer;
        }

        private void BuildChain(Type type, IReadOnlyList<IState> activeStates, int index)
        {
            if (index >= 0 && type == activeStates[index].GetType())
            {
                for (var i = 0; i <= index; i++)
                    _chainBuffer.Add(activeStates[i]);
                return;
            }

            var state = CreateStateInternal(type);

            if (state is IChildState childState)
                BuildChain(childState.ParentState, activeStates, index - 1);

            _chainBuffer.Add(state);
        }

        public void MarkInitialized(IState state)
        {
            if (_initializedStates.Add(state.GetType()))
                OnInitializeState(state);
        }

        protected abstract IState CreateStateInternal(Type type);

        protected virtual void OnInitializeState(IState state) { }

        public void Release(IState state)
        {
            if (state is EmptyState) return;

            _initializedStates.Remove(state.GetType());
            ReleaseInternal(state);
        }

        protected virtual void ReleaseInternal(IState state) { }
    }

    public abstract class StateFactory<TState> : StateFactory
        where TState : IState
    {
        protected sealed override void OnInitializeState(IState state) =>
            OnInitializeState((TState)state);

        protected virtual void OnInitializeState(TState state) { }

        protected sealed override void ReleaseInternal(IState state) =>
            ReleaseInternal((TState)state);

        protected virtual void ReleaseInternal(TState state) { }
    }
}
