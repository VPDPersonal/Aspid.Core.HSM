using System;
using UnityEngine;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Unity <see cref="MonoBehaviour"/> wrapper around <see cref="StateMachineBase"/>.
    /// Wires Unity's Update/LateUpdate/FixedUpdate lifecycle to the state machine and
    /// exposes the full <see cref="IStateMachine"/> contract.
    /// </summary>
    public partial class MonoStateMachine : MonoBehaviour, IStateMachine, IDisposable
    {
        private static readonly IReadOnlyList<IState> EmptyStates = Array.Empty<IState>();
        private static readonly IReadOnlyList<IExtensionState> EmptyExtensions = Array.Empty<IExtensionState>();

        private MonoStateMachineCore? _stateMachine;

        /// <summary>
        /// <c>true</c> after <see cref="Initialize"/> has been called.
        /// </summary>
        public bool IsInitialized => _stateMachine is not null;

        /// <inheritdoc />
        public IReadOnlyList<IState> CurrentStates => _stateMachine?.CurrentStates ?? EmptyStates;

        #region Initialize
        /// <summary>
        /// Initializes the state machine with the given factory. Idempotent — subsequent calls are ignored.
        /// </summary>
        /// <param name="stateFactory">The factory used to create and release state instances.</param>
        public void Initialize(StateFactory stateFactory)
        {
            if (IsInitialized) return;

            OnInitializing();
            {
                _stateMachine = new MonoStateMachineCore(this, stateFactory);
            }
            OnInitialized();
        }

        /// <summary>
        /// Called before the internal state machine core is created.
        /// </summary>
        protected virtual void OnInitializing() { }

        /// <summary>
        /// Called after the internal state machine core has been created.
        /// </summary>
        protected virtual void OnInitialized() { }
        #endregion

        /// <inheritdoc />
        public IReadOnlyList<IExtensionState> ActiveExtensions => _stateMachine?.ActiveExtensions ?? EmptyExtensions;

        /// <inheritdoc />
        public void AttachExtension<T>() where T : IExtensionState => _stateMachine!.AttachExtension<T>();

        /// <inheritdoc />
        public void DetachExtension<T>() where T : IExtensionState => _stateMachine!.DetachExtension<T>();

        /// <inheritdoc />
        public void ChangeState<TState>() where TState : IState =>
            _stateMachine!.ChangeState<TState>();

        /// <inheritdoc />
        public void TransitionTo<TTarget>() where TTarget : IState =>
            _stateMachine!.TransitionTo<TTarget>();

        /// <inheritdoc />
        public void TransitionVia<TTransition>() where TTransition : ITransition =>
            _stateMachine!.TransitionVia<TTransition>();

        /// <inheritdoc />
        public bool IsTransitioning => _stateMachine?.IsTransitioning ?? false;

        /// <inheritdoc cref="StateMachineBase.RegisterTransition(ITransition)"/>
        public void RegisterTransition(ITransition transition) =>
            _stateMachine!.RegisterTransition(transition);

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

        /// <summary>Called before the Update dispatch each frame.</summary>
        protected virtual void OnUpdating() { }

        /// <summary>Called after the Update dispatch each frame.</summary>
        protected virtual void OnUpdated() { }

        /// <summary>Called before the LateUpdate dispatch each frame.</summary>
        protected virtual void OnLateUpdating() { }

        /// <summary>Called after the LateUpdate dispatch each frame.</summary>
        protected virtual void OnLateUpdated() { }

        /// <summary>Called before the FixedUpdate dispatch each tick.</summary>
        protected virtual void OnFixedUpdating() { }

        /// <summary>Called after the FixedUpdate dispatch each tick.</summary>
        protected virtual void OnFixedUpdated() { }
        #endregion

        #region ChangeState hooks
        /// <inheritdoc cref="StateMachineBase.OnChangingState"/>
        protected virtual void OnChangingState() { }

        /// <inheritdoc cref="StateMachineBase.OnChangedState"/>
        protected virtual void OnChangedState() { }

        /// <inheritdoc cref="StateMachineBase.OnEnteringState"/>
        protected virtual void OnEnteringState(IState state) { }

        /// <inheritdoc cref="StateMachineBase.OnEnteredState"/>
        protected virtual void OnEnteredState(IState state) { }

        /// <inheritdoc cref="StateMachineBase.OnExitingState"/>
        protected virtual void OnExitingState(IState state) { }

        /// <inheritdoc cref="StateMachineBase.OnExitedState"/>
        protected virtual void OnExitedState(IState state) { }
        #endregion

        #region Dispose
        /// <summary>
        /// Disposes the internal state machine, detaching all extensions and disposing all active states.
        /// </summary>
        public void Dispose() =>
            _stateMachine?.Dispose();

        /// <inheritdoc cref="StateMachineBase.Disposing"/>
        protected virtual void Disposing() { }

        /// <inheritdoc cref="StateMachineBase.Disposed"/>
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
