using System;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public abstract class MonoStateMachine : MonoBehaviour, IStateMachine
    {
        private IState _currentState;
        
        public Type CurrentState => _currentState.GetType();

        #region Awake
        private void Awake()
        {
            OnAwaking();
            {
                _currentState = new RootState();
            }
            OnAwoke();
        }

        protected virtual void OnAwaking() { }
        
        protected virtual void OnAwoke() {}
        #endregion

        #region Update
        private void Update()
        {
            OnUpdating();
            {
                _currentState.GetController<IUpdateController>()?.Update(Time.deltaTime);
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
                _currentState.GetController<ILateUpdateController>()?.LateUpdate(Time.deltaTime);
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
                _currentState.GetController<IFixedUpdateController>()?.FixedUpdate(Time.deltaTime);
            }
            OnFixedUpdated();
        }
        
        protected virtual void OnFixedUpdating() { }
        
        protected virtual void OnFixedUpdated() { }
        #endregion

        #region ChangeState
        public void ChangeState<T>()
            where T : IState
        {
            OnChangingState();
            {
                _currentState.GetController<IExitController>()?.OnExit();
                _currentState.Exit();
                {
                    _currentState = GetState<T>();
                }
                _currentState.Enter();
                _currentState.GetController<IEnterController>()?.OnEnter();
            }
            OnChangedState();
        }

        protected virtual void OnChangingState() { }
        
        protected virtual void OnChangedState() { }
        #endregion
        
        protected abstract IState GetState<T>() 
            where T : IState;
    }
}
