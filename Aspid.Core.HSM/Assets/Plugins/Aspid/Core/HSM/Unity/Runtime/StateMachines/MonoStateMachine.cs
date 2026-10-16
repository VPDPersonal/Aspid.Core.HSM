#nullable enable
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
        /// Gets a value indicating whether <see cref="Initialize"/> has been called and <see cref="Dispose"/> has not
        /// been called since; after <see cref="Dispose"/>, <see cref="Initialize"/> creates a new machine.
        /// </summary>
        public bool IsInitialized => _stateMachine is { IsDisposed: false };

        /// <inheritdoc />
        public IReadOnlyList<IState> CurrentStates => _stateMachine?.CurrentStates ?? EmptyStates;

        #region Initialize
        /// <summary>
        /// Initializes the state machine with the given factory. Idempotent — subsequent calls are ignored until
        /// <see cref="Dispose"/>.
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
        /// <summary>
        /// Called by Unity every frame to dispatch <see cref="IUpdateController"/> to the active states and extensions.
        /// </summary>
        /// <remarks>
        /// An override must call <c>base.Update()</c>, or the machine stops ticking. A subclass that declares its own
        /// <c>Update</c> without <see langword="override"/> hides this one, and Unity calls only the subclass's.
        /// </remarks>
        protected virtual void Update()
        {
            if (!IsInitialized) return;

            OnUpdating();
            _stateMachine!.InvokeUpdate(Time.deltaTime);
            OnUpdated();
        }

        /// <summary>
        /// Called by Unity every frame to dispatch <see cref="ILateUpdateController"/> to the active states and extensions.
        /// </summary>
        /// <remarks>An override must call <c>base.LateUpdate()</c>, as for <see cref="Update"/>.</remarks>
        protected virtual void LateUpdate()
        {
            if (!IsInitialized) return;

            OnLateUpdating();
            _stateMachine!.InvokeLateUpdate(Time.deltaTime);
            OnLateUpdated();
        }

        /// <summary>
        /// Called by Unity every fixed step to dispatch <see cref="IFixedUpdateController"/> to the active states and extensions.
        /// </summary>
        /// <remarks>An override must call <c>base.FixedUpdate()</c>, as for <see cref="Update"/>.</remarks>
        protected virtual void FixedUpdate()
        {
            if (!IsInitialized) return;

            OnFixedUpdating();
            _stateMachine!.InvokeFixedUpdate(Time.fixedDeltaTime);
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

        #region Extension Points
        /// <inheritdoc cref="StateMachineBase.IsControllerEnabled"/>
        protected virtual bool IsControllerEnabled(IController controller, IState state) => true;

        /// <inheritdoc cref="StateMachineBase.IsStateEnabled"/>
        protected virtual bool IsStateEnabled(Type stateType) => true;

        /// <inheritdoc cref="StateMachineBase.IsTransitionEnabled"/>
        protected virtual bool IsTransitionEnabled(Type sourceType, Type targetType) => true;

        /// <inheritdoc cref="StateMachineBase.StrictTransitions"/>
        protected virtual bool StrictTransitions => false;
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
        /// <remarks>
        /// Calls made after it are ignored, as by <see cref="StateMachineBase.Dispose"/>, and <see cref="IsInitialized"/>
        /// is <see langword="false"/> again.
        /// </remarks>
        public void Dispose() =>
            _stateMachine?.Dispose();

        /// <summary>
        /// Called by Unity when the component or its GameObject is destroyed to <see cref="Dispose"/> the machine.
        /// </summary>
        /// <remarks>
        /// An override must call <c>base.OnDestroy()</c>, or the states and their scopes are never disposed.
        /// </remarks>
        protected virtual void OnDestroy() =>
            Dispose();

        /// <inheritdoc cref="StateMachineBase.Disposing"/>
        protected virtual void Disposing() { }

        /// <inheritdoc cref="StateMachineBase.Disposed"/>
        protected virtual void Disposed() { }
        #endregion

        internal bool RaiseIsControllerEnabled(IController controller, IState state) => IsControllerEnabled(controller, state);
        internal bool RaiseIsStateEnabled(Type stateType) => IsStateEnabled(stateType);
        internal bool RaiseIsTransitionEnabled(Type sourceType, Type targetType) => IsTransitionEnabled(sourceType, targetType);
        internal bool RaiseStrictTransitions() => StrictTransitions;
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
