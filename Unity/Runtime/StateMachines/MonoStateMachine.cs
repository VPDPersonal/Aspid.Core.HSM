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
        
        // TODO Aspid.Core.HSM - Add ZLinq support
        public IReadOnlyCollection<IState> CurrentStates => _currentStates;

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
                for (var i = _currentStates.Count - 1; i >= 0; i--)
                {
                    _currentStates[i].GetController<IExitController>()?.OnExit();
                    _currentStates[i].Exit();
                    
                    _stateFactory.Release(_currentStates[i]);
                }
                
                _currentStates.Clear();
                _currentStates.AddRange(collection: _stateFactory.CreateState<TState>());
                
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
