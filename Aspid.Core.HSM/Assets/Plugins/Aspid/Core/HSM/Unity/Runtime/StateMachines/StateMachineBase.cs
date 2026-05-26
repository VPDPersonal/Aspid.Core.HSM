using System;
using System.Collections.Generic;
using System.Threading;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public partial class StateMachineBase : IStateMachine, IDisposable
    {
        private readonly StateFactory _stateFactory;
        private readonly List<IState> _currentStates = new(capacity: 1);

        private CancellationTokenSource? _activeTransitionCts;

        public IReadOnlyList<IState> CurrentStates => _currentStates;

        public StateMachineBase(StateFactory stateFactory)
        {
            _stateFactory = stateFactory;
            _currentStates.Add(new EmptyState());
        }

        #region Update
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
        public void ChangeState<TState>()
            where TState : IState
        {
            if (!IsStateEnabled(typeof(TState)))
                return;

            if (_activeTransitionCts is not null)
                throw new InvalidOperationException(
                    "An asynchronous transition is in progress. Use ChangeStateAsync or wait for it to complete.");

            OnChangingState();
            {
                var newChain = _stateFactory.CreateState<TState>(_currentStates);
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
                _stateFactory.MarkInitialized(state);
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
            Disposing();
            {
                for (int i = _activeExtensions.Count - 1; i >= 0; i--)
                    DetachExtensionAt(i);

                foreach (var state in _currentStates)
                    state.GetController<IDisposableController>()?.Dispose();
            }
            Disposed();
        }

        protected virtual void Disposing() { }

        protected virtual void Disposed() { }
        #endregion

        #region Extension Points
        protected virtual bool IsControllerEnabled(IController controller, IState state) => true;

        protected virtual bool IsStateEnabled(Type stateType) => true;
        #endregion
    }
}
