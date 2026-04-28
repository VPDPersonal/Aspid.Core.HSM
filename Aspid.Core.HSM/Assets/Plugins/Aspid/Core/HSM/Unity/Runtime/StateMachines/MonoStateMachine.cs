using System;
using UnityEngine;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public class MonoStateMachine : MonoBehaviour, IStateMachine, IDisposable
    {
        private static readonly IReadOnlyList<IState> EmptyStates = Array.Empty<IState>();

        private MonoStateMachineCore? _stateMachine;

        public bool IsInitialized => _stateMachine is not null;

        public IReadOnlyList<IState> CurrentStates => _stateMachine?.CurrentStates ?? EmptyStates;

        #region Initialize
        public void Initialize(StateFactory stateFactory)
        {
            if (IsInitialized) return;

            OnInitializing();
            {
                _stateMachine = new MonoStateMachineCore(this, stateFactory);
            }
            OnInitialized();
        }

        protected virtual void OnInitializing() { }

        protected virtual void OnInitialized() { }
        #endregion

        public void ChangeState<TState>() where TState : IState =>
            _stateMachine!.ChangeState<TState>();

        #region Update
        private void Update()
        {
            if (_stateMachine is null) return;

            OnUpdating();
            _stateMachine.InvokeUpdate(Time.deltaTime);
            OnUpdated();
        }

        private void LateUpdate()
        {
            if (_stateMachine is null) return;

            OnLateUpdating();
            _stateMachine.InvokeLateUpdate(Time.deltaTime);
            OnLateUpdated();
        }

        private void FixedUpdate()
        {
            if (_stateMachine is null) return;

            OnFixedUpdating();
            _stateMachine.InvokeFixedUpdate(Time.fixedDeltaTime);
            OnFixedUpdated();
        }

        protected virtual void OnUpdating() { }

        protected virtual void OnUpdated() { }

        protected virtual void OnLateUpdating() { }

        protected virtual void OnLateUpdated() { }

        protected virtual void OnFixedUpdating() { }

        protected virtual void OnFixedUpdated() { }
        #endregion

        #region ChangeState hooks
        protected virtual void OnChangingState() { }

        protected virtual void OnChangedState() { }

        protected virtual void OnEnteringState(IState state) { }

        protected virtual void OnEnteredState(IState state) { }

        protected virtual void OnExitingState(IState state) { }

        protected virtual void OnExitedState(IState state) { }
        #endregion

        #region Dispose
        public void Dispose() =>
            _stateMachine?.Dispose();

        protected virtual void Disposing() { }

        protected virtual void Disposed() { }
        #endregion

        internal void RaiseChangingState() => OnChangingState();
        internal void RaiseChangedState() => OnChangedState();
        internal void RaiseEnteringState(IState state) => OnEnteringState(state);
        internal void RaiseEnteredState(IState state) => OnEnteredState(state);
        internal void RaiseExitingState(IState state) => OnExitingState(state);
        internal void RaiseExitedState(IState state) => OnExitedState(state);
        internal void RaiseDisposing() => Disposing();
        internal void RaiseDisposed() => Disposed();
    }
}
