using System;
using UnityEngine;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public class MonoStateMachine : MonoBehaviour, IStateMachine, IDisposable
    {
        private StateFactory _stateFactory;
        private readonly List<IState> _currentStates = new(capacity: 1);

        public bool IsInitialized => _stateFactory is not null;
        
        public IReadOnlyList<IState> CurrentStates => _currentStates;

        #region Initialize
        public void Initialize(StateFactory stateFactory)
        {
            if (IsInitialized) return;

            OnInitializing();
            {
                _stateFactory = stateFactory;
                _currentStates.Add(new EmptyState());
            }
            OnInitialized();
        }

        protected virtual void OnInitialized() { }
        
        protected virtual void OnInitializing() { }
        #endregion

        #region Update
        private void Update()
        {
            OnUpdating();
            {
                foreach (var state in _currentStates)
                    state.GetController<IUpdateController>()?.Update(Time.deltaTime);
            }
            OnUpdated();
        }
        
        protected virtual void OnUpdating() { }
        
        protected virtual void OnUpdated() { }
        #endregion

        #region LateUpdate
        private void LateUpdate()
        {
            OnLateUpdating();
            {
                foreach (var state in _currentStates)
                    state.GetController<ILateUpdateController>()?.LateUpdate(Time.deltaTime);
            }
            OnLateUpdated();
        }
        
        protected virtual void OnLateUpdating() { }
        
        protected virtual void OnLateUpdated() { }
        #endregion

        #region FixedUpdate
        private void FixedUpdate()
        {
            OnFixedUpdating();
            {
                foreach (var state in _currentStates)
                    state.GetController<IFixedUpdateController>()?.FixedUpdate(Time.fixedDeltaTime);
            }
            OnFixedUpdated();
        }
        
        protected virtual void OnFixedUpdating() { }
        
        protected virtual void OnFixedUpdated() { }
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
