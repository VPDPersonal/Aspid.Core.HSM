using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public abstract class StateMachineBase : IStateMachine
    {
        private readonly StateFactory _stateFactory;
        private readonly List<IState> _currentStates = new(capacity: 1);
        
        public IReadOnlyList<IState> CurrentStates => _currentStates;

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
                var index = 0;

                foreach (var state in _stateFactory.CreateState<TState>(_currentStates))
                {
                    if (index > -1)
                    {
                        if (index < _currentStates.Count && state == _currentStates[index])
                        {
                            index++;
                            continue;
                        }
                        
                        var count = _currentStates.Count - index;

                        for (var i = 0; i <= count; i++)
                        {
                            var lastIndex = _currentStates.Count - 1;
                            
                            ExitState(_currentStates[lastIndex]);
                            _currentStates.RemoveAt(lastIndex);
                        }

                        index = -1;
                    }
                    
                    _currentStates.Add(state);
                    EnterState(state);
                }
            }
            OnChangedState();
        }

        protected virtual void OnChangingState() { }
        
        protected virtual void OnChangedState() { }
        #endregion

        #region Exit
        private void ExitState(IState state)
        {
            OnExitingState(state);
            {
                state.GetController<IExitController>()?.OnExit();
                state.Exit();
            }
            OnExitedState(state);
            
            _stateFactory.Release(state);
        }
        
        protected virtual void OnExitingState(IState state) { }
        
        protected virtual void OnExitedState(IState state) { }
        #endregion

        #region Enter
        private void EnterState(IState state)
        {
            OnEnteringState(state);
            {
                state.Enter();
                state.GetController<IEnterController>()?.OnEnter();
            }
            OnEnteredState(state);
        }
        
        protected virtual void OnEnteringState(IState state) { }
        
        protected virtual void OnEnteredState(IState state) { }
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
