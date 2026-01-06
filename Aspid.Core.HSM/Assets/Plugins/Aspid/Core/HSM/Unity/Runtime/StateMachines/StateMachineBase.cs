using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public abstract class StateMachineBase : IStateMachine
    {
        private readonly StateFactory _stateFactory;
        private readonly List<IState> _currentStates = new(capacity: 1);

        // TODO Aspid.Core.HSM - Add ZLinq support
        public IReadOnlyCollection<IState> CurrentStates => _currentStates;

        protected StateMachineBase(StateFactory stateFactory)
        {
            _stateFactory = stateFactory;
            _currentStates.Add(new EmptyState());
        }

        #region Update
        protected void Update(float deltaTime)
        {
            foreach (var state in _currentStates)
                state.GetController<IUpdateController>()?.Update(deltaTime);
        }
        
        protected void LateUpdate(float deltaTime)
        {
            foreach (var state in _currentStates)
                state.GetController<ILateUpdateController>()?.LateUpdate(deltaTime);
        }
        
        protected void FixedUpdate(float deltaTime)
        {
            foreach (var state in _currentStates)
                state.GetController<IFixedUpdateController>()?.FixedUpdate(deltaTime);
        }
        #endregion

        #region ChangeState
        public void ChangeState<TState>()
            where TState : IState
        {
            OnChangingState();
            {
                for (var i = _currentStates.Count - 1; i >= 0; i--)
                {
                    _currentStates[i].GetController<IExitController>()?.OnExit();
                    _currentStates[i].Exit();
                }

                _currentStates.Clear();
                _currentStates.AddRange(collection: GetStates<TState>());

                foreach (var state in _currentStates)
                {
                    state.Enter();
                    state.GetController<IEnterController>()?.OnEnter();
                }
            }
            OnChangedState();
        }

        protected virtual void OnChangingState() { }

        protected virtual void OnChangedState() { }

        protected abstract IEnumerable<IState> GetStates<TState>()
            where TState : IState;
        #endregion

        #region Dispose
        public void Dispose()
        {
            Disposed();
            {
                foreach (var state in _currentStates)
                    state.GetController<IDisposableController>()?.Dispose();
            }
            Disposing();
        }

        protected virtual void Disposed() { }

        protected virtual void Disposing() { }
        #endregion
    }
}
