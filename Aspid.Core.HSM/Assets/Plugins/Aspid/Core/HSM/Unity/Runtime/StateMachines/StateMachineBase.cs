using System;
using System.Collections.Generic;
using System.Threading;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Core state machine implementation. Holds the active state chain, diffs it on
    /// <see cref="ChangeState{TState}"/>, and dispatches update/enter/exit controllers.
    /// State lifetime and parent-chain construction are delegated to <see cref="StateFactory"/>.
    /// </summary>
    public partial class StateMachineBase : IStateMachine, IDisposable
    {
        private readonly StateFactory _stateFactory;
        private readonly List<IState> _currentStates = new(capacity: 1);

        private readonly Stack<List<IState>> _chainBufferPool = new();
        private readonly Queue<PendingRequest> _pendingRequests = new();
        private bool _isChangingState;

        private CancellationTokenSource? _activeTransitionCts;

        /// <inheritdoc />
        public IReadOnlyList<IState> CurrentStates => _currentStates;

        /// <param name="stateFactory">The factory used to create and release state instances.</param>
        public StateMachineBase(StateFactory stateFactory)
        {
            _stateFactory = stateFactory;
            _currentStates.Add(new EmptyState());
        }

        #region Update
        /// <summary>
        /// Dispatches <see cref="IUpdateController.Update"/> to all active states and extensions.
        /// </summary>
        /// <param name="deltaTime">Time in seconds since the previous frame.</param>
        protected void Update(float deltaTime)
        {
            foreach (var state in _currentStates)
            {
                var controller = state.GetController<IUpdateController>();
                if (controller is not null && IsControllerEnabled(controller, state))
                    controller.Update(deltaTime);
            }
            foreach (var ext in _activeExtensions)
            {
                var controller = ext.GetController<IUpdateController>();
                if (controller is not null && IsControllerEnabled(controller, ext))
                    controller.Update(deltaTime);
            }
        }

        /// <summary>
        /// Dispatches <see cref="ILateUpdateController.LateUpdate"/> to all active states and extensions.
        /// </summary>
        /// <param name="deltaTime">Time in seconds since the previous frame.</param>
        protected void LateUpdate(float deltaTime)
        {
            foreach (var state in _currentStates)
            {
                var controller = state.GetController<ILateUpdateController>();
                if (controller is not null && IsControllerEnabled(controller, state))
                    controller.LateUpdate(deltaTime);
            }
            foreach (var ext in _activeExtensions)
            {
                var controller = ext.GetController<ILateUpdateController>();
                if (controller is not null && IsControllerEnabled(controller, ext))
                    controller.LateUpdate(deltaTime);
            }
        }

        /// <summary>
        /// Dispatches <see cref="IFixedUpdateController.FixedUpdate"/> to all active states and extensions.
        /// </summary>
        /// <param name="deltaTime">The fixed timestep interval in seconds.</param>
        protected void FixedUpdate(float deltaTime)
        {
            foreach (var state in _currentStates)
            {
                var controller = state.GetController<IFixedUpdateController>();
                if (controller is not null && IsControllerEnabled(controller, state))
                    controller.FixedUpdate(deltaTime);
            }
            foreach (var ext in _activeExtensions)
            {
                var controller = ext.GetController<IFixedUpdateController>();
                if (controller is not null && IsControllerEnabled(controller, ext))
                    controller.FixedUpdate(deltaTime);
            }
        }
        #endregion

        #region ChangeState
        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">An async transition is already in progress.</exception>
        public void ChangeState<TState>()
            where TState : IState =>
            ChangeState(typeof(TState));

        /// <inheritdoc cref="ChangeState{TState}"/>
        /// <param name="stateType">The target leaf state type.</param>
        public void ChangeState(Type stateType)
        {
            ThrowIfAsyncTransitionInProgress();
            Request(new PendingRequest(stateType, PendingKind.ChangeState));
        }

        private void ApplyChangeState(Type stateType)
        {
            if (!IsStateEnabled(stateType))
                return;

            if (!IsTransitionEnabled(_currentStates[^1].GetType(), stateType))
                return;

            OnChangingState();
            {
                // Rented per in-flight transition: the chain must stay valid across Enter/Exit callbacks,
                // which are free to re-enter the factory. A shared buffer would be rewritten underneath us.
                var newChain = RentChainBuffer();
                try
                {
                    _stateFactory.CreateState(stateType, _currentStates, newChain);
                    var divergeIndex = FindDivergeIndex(newChain);

                    for (var i = _currentStates.Count - 1; i >= divergeIndex; i--)
                    {
                        ExitState(_currentStates[i]);
                        _currentStates.RemoveAt(i);
                    }

                    for (var i = divergeIndex; i < newChain.Count; i++)
                    {
                        var state = newChain[i];
                        _currentStates.Add(state);
                        EnterState(state);
                    }
                }
                finally
                {
                    ReturnChainBuffer(newChain);
                }
            }
            OnChangedState();
            AutoDetachIncompatibleExtensions();
        }

        private int FindDivergeIndex(IReadOnlyList<IState> newChain)
        {
            var commonLength = Math.Min(_currentStates.Count, newChain.Count);
            for (var i = 0; i < commonLength; i++)
            {
                if (!ReferenceEquals(_currentStates[i], newChain[i]))
                    return i;
            }

            return commonLength;
        }

        /// <summary>
        /// Called before the state chain diff and exit/enter sequence begins.
        /// </summary>
        protected virtual void OnChangingState() { }

        /// <summary>
        /// Called after all state exits and enters have completed.
        /// </summary>
        protected virtual void OnChangedState() { }
        #endregion

        #region Run-to-completion
        private enum PendingKind
        {
            ChangeState,
            TransitionTo,
            TransitionVia,
        }

        private readonly struct PendingRequest
        {
            public readonly Type Type;
            public readonly PendingKind Kind;

            public PendingRequest(Type type, PendingKind kind)
            {
                Type = type;
                Kind = kind;
            }
        }

        /// <summary>
        /// Entry gate for every synchronous state change. A request made while another one is still
        /// running — the usual case being a state that redirects from its own <see cref="IState.Enter"/>
        /// or <see cref="IEnterController.OnEnter"/> — is queued and applied once the running change
        /// completes, rather than mutating the chain underneath it (run-to-completion semantics).
        /// </summary>
        private void Request(PendingRequest request)
        {
            if (_isChangingState)
            {
                _pendingRequests.Enqueue(request);
                return;
            }

            _isChangingState = true;
            try
            {
                Apply(request);

                // Requests queued by Enter/Exit callbacks are drained here, in the order they were made.
                // Guards are resolved at apply time, so each one sees the chain the previous one left behind.
                while (_pendingRequests.Count > 0)
                    Apply(_pendingRequests.Dequeue());
            }
            finally
            {
                _isChangingState = false;
                // A throwing state must not leak its queued follow-ups into an unrelated later transition.
                _pendingRequests.Clear();
            }
        }

        private void Apply(PendingRequest request)
        {
            switch (request.Kind)
            {
                case PendingKind.ChangeState:
                    ApplyChangeState(request.Type);
                    break;

                case PendingKind.TransitionTo:
                    ApplyTransitionTo(request.Type);
                    break;

                case PendingKind.TransitionVia:
                    ApplyTransitionVia(request.Type);
                    break;
            }
        }

        private List<IState> RentChainBuffer() =>
            _chainBufferPool.Count > 0 ? _chainBufferPool.Pop() : new List<IState>(capacity: 4);

        private void ReturnChainBuffer(List<IState> buffer)
        {
            buffer.Clear();
            _chainBufferPool.Push(buffer);
        }
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

        /// <summary>
        /// Called before a state's exit controllers and <see cref="IState.Exit"/> are invoked.
        /// </summary>
        /// <param name="state">The state about to be exited.</param>
        protected virtual void OnExitingState(IState state) { }

        /// <summary>
        /// Called after a state has been fully exited and released.
        /// </summary>
        /// <param name="state">The state that was exited.</param>
        protected virtual void OnExitedState(IState state) { }
        #endregion

        #region Enter
        private void EnterState(IState state)
        {
            OnEnteringState(state);
            {
                _stateFactory.MarkInitialized(state);
                state.Enter();
                state.GetController<IEnterController>()?.OnEnter();
            }
            OnEnteredState(state);
        }

        /// <summary>
        /// Called before a state is initialized, entered, and its enter controllers are invoked.
        /// </summary>
        /// <param name="state">The state about to be entered.</param>
        protected virtual void OnEnteringState(IState state) { }

        /// <summary>
        /// Called after a state has been fully entered and its enter controllers have run.
        /// </summary>
        /// <param name="state">The state that was entered.</param>
        protected virtual void OnEnteredState(IState state) { }
        #endregion

        #region Dispose
        /// <summary>
        /// Detaches all extensions, disposes all active states via <see cref="IDisposableController"/>,
        /// and disposes all active and cached state scopes.
        /// </summary>
        public void Dispose()
        {
            Disposing();
            {
                for (int i = _activeExtensions.Count - 1; i >= 0; i--)
                    DetachExtensionAt(i);

                foreach (var state in _currentStates)
                    state.GetController<IDisposableController>()?.Dispose();

                _stateFactory.DisposeAllScopes();
            }
            Disposed();
        }

        /// <summary>
        /// Called before dispose logic runs.
        /// </summary>
        protected virtual void Disposing() { }

        /// <summary>
        /// Called after dispose logic completes.
        /// </summary>
        protected virtual void Disposed() { }
        #endregion

        #region Extension Points
        /// <summary>
        /// Determines whether a controller should execute during the current update tick.
        /// Override to conditionally suppress controllers (e.g. during pause).
        /// </summary>
        /// <param name="controller">The controller about to be invoked.</param>
        /// <param name="state">The state that owns the controller.</param>
        /// <returns><c>true</c> to invoke the controller; <c>false</c> to skip it.</returns>
        protected virtual bool IsControllerEnabled(IController controller, IState state) => true;

        /// <summary>
        /// Determines whether a state change to <paramref name="stateType"/> is allowed, regardless of
        /// where it is coming from. Override to block entering a state conditionally.
        /// </summary>
        /// <remarks>
        /// This is a node-level guard. To allow or deny a specific <em>edge</em>, override
        /// <see cref="IsTransitionEnabled"/>, which also receives the source state type.
        /// </remarks>
        /// <param name="stateType">The target state type.</param>
        /// <returns><c>true</c> to allow the state change; <c>false</c> to silently suppress it.</returns>
        protected virtual bool IsStateEnabled(Type stateType) => true;

        /// <summary>
        /// Determines whether moving from <paramref name="sourceType"/> to <paramref name="targetType"/>
        /// is allowed. Every state change funnels through this guard, including
        /// <see cref="ChangeState{TState}"/>, so it is the single point at which edge legality can be enforced.
        /// </summary>
        /// <param name="sourceType">The current leaf state type the machine is moving away from.</param>
        /// <param name="targetType">The target leaf state type.</param>
        /// <returns><c>true</c> to allow the state change; <c>false</c> to silently suppress it.</returns>
        protected virtual bool IsTransitionEnabled(Type sourceType, Type targetType) => true;

        /// <summary>
        /// Controls whether a registered <see cref="ITransition"/> is <em>required</em> for
        /// <see cref="TransitionTo{TTarget}"/> and <see cref="TransitionToAsync{TTarget}"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Default is <c>false</c>: an unregistered target is still entered, so <c>[Transition]</c> declares
        /// guards and hooks for edges that have them and says nothing about edges that do not.
        /// </para>
        /// <para>
        /// Override to <c>true</c> to treat the transition registry as the declaration of the <em>allowed</em>
        /// edges. A target with no registered transition covering every step of the path then throws
        /// <see cref="InvalidOperationException"/> instead of silently succeeding. <see cref="ChangeState{TState}"/>
        /// remains the deliberate escape hatch and is never subject to this check.
        /// </para>
        /// </remarks>
        protected virtual bool StrictTransitions => false;
        #endregion
    }
}
