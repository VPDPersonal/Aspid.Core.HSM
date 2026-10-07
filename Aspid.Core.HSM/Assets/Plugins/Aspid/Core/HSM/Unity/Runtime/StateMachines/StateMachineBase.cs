#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
#if ENABLE_PROFILER
using System.Text;
using Unity.Profiling;
#endif

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

        private readonly Stack<List<Type>> _typeChainBufferPool = new();
        private readonly Queue<PendingRequest> _pendingRequests = new();
        private bool _isChangingState;
        private bool _isTicking;

        // Index of the extension the running tick is at, or -1 outside a tick. DetachExtensionAt shifts it
        // when it removes an extension at or before it, so the tick does not skip the next extension.
        private int _tickedExtensionIndex = -1;

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
        protected void Update(float deltaTime) =>
            Tick<IUpdateController>(deltaTime, static (controller, dt) => controller.Update(dt));

        /// <summary>
        /// Dispatches <see cref="ILateUpdateController.LateUpdate"/> to all active states and extensions.
        /// </summary>
        /// <param name="deltaTime">Time in seconds since the previous frame.</param>
        protected void LateUpdate(float deltaTime) =>
            Tick<ILateUpdateController>(deltaTime, static (controller, dt) => controller.LateUpdate(dt));

        /// <summary>
        /// Dispatches <see cref="IFixedUpdateController.FixedUpdate"/> to all active states and extensions.
        /// </summary>
        /// <param name="deltaTime">The fixed timestep interval in seconds.</param>
        protected void FixedUpdate(float deltaTime) =>
            Tick<IFixedUpdateController>(deltaTime, static (controller, dt) => controller.FixedUpdate(dt));

        /// <summary>
        /// Runs one tick over the active chain and then the active extensions. A synchronous state change
        /// requested by a controller during the tick is queued and applied once every controller has been
        /// ticked, so the tick never walks a chain that is being rewritten underneath it.
        /// </summary>
        private void Tick<TController>(float deltaTime, Action<TController, float> invoke)
            where TController : IController
        {
            _isTicking = true;
            try
            {
                // Indexed rather than foreach: a controller may still attach or detach an extension, or start
                // an async transition, and those change the lists mid-tick. An index sees them as they are now:
                // a state or extension removed before its turn is not ticked, and nothing throws.
                for (var i = 0; i < _currentStates.Count; i++)
                {
                    var state = _currentStates[i];
                    var controller = state.GetController<TController>();
                    if (controller is not null && IsControllerEnabled(controller, state))
                    {
#if ENABLE_PROFILER
                        using (GetMarkers(state).ForTick<TController>().Auto())
#endif
                        invoke(controller, deltaTime);
                    }
                }

                for (_tickedExtensionIndex = 0; _tickedExtensionIndex < _activeExtensions.Count; _tickedExtensionIndex++)
                {
                    var extension = _activeExtensions[_tickedExtensionIndex];
                    var controller = extension.GetController<TController>();
                    if (controller is not null && IsControllerEnabled(controller, extension))
                    {
#if ENABLE_PROFILER
                        using (GetMarkers(extension).ForTick<TController>().Auto())
#endif
                        invoke(controller, deltaTime);
                    }
                }
            }
            catch
            {
                // A throwing controller must not leak the requests made before it into a later tick.
                _pendingRequests.Clear();
                throw;
            }
            finally
            {
                _isTicking = false;
                _tickedExtensionIndex = -1;
            }

            if (_pendingRequests.Count > 0)
            {
                // An async transition started later in the same tick owns the chain now. Applying the
                // deferred change on top of it would corrupt the chain, so it is rejected the same way an
                // immediate ChangeState is rejected while an async transition is in progress.
                if (_activeTransitionCts is not null)
                {
                    _pendingRequests.Clear();
                    ThrowIfAsyncTransitionInProgress();
                }

                RunToCompletion(_pendingRequests.Dequeue());
            }
        }
        #endregion

        #region ChangeState
        /// <inheritdoc />
        /// <remarks>
        /// A state constructor, <see cref="IState.Exit"/> or <see cref="IState.Enter"/> that throws stops the change
        /// midway: exited states stay exited, and the chain ends at the last state reached, or at
        /// <see cref="EmptyState"/> when none is left. <see cref="OnChangedState"/> still runs, then the exception propagates.
        /// </remarks>
        /// <exception cref="InvalidOperationException">An async transition is already in progress.</exception>
        /// <exception cref="AggregateException">
        /// A state threw, and then <see cref="OnChangedState"/> or an extension's detach threw too; the state's exception comes first.
        /// Or several extensions threw while the change detached them.
        /// </exception>
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

            ChangeStateCore(stateType);
        }

        // Diffs and swaps the chain with no guard checks: every caller has checked the guards already.
        private void ChangeStateCore(Type stateType)
        {
            OnChangingState();
            // Rented per in-flight transition: the chain must stay valid across Enter/Exit callbacks,
            // which are free to re-enter the factory. A shared buffer would be rewritten underneath us.
            var newChain = RentTypeChainBuffer();
            try
            {
                _stateFactory.BuildTypeChain(stateType, newChain);
                var divergeIndex = FindDivergeIndex(newChain);

                for (var i = _currentStates.Count - 1; i >= divergeIndex; i--)
                {
                    ExitState(_currentStates[i]);
                    _currentStates.RemoveAt(i);
                }

                // Each state is created only once its parent has been entered, so it is resolved from
                // its own scope, a child of the parent's.
                for (var i = divergeIndex; i < newChain.Count; i++)
                {
                    var state = _stateFactory.CreateState(newChain[i]);
                    _currentStates.Add(state);
                    EnterState(state);
                }
            }
            catch (Exception cause)
            {
                CloseFailedChange(cause);
                throw;
            }
            finally
            {
                ReturnTypeChainBuffer(newChain);
            }

            OnChangedState();
            AutoDetachIncompatibleExtensions();
        }

        // A change that throws midway keeps the states it reached: exited states cannot be entered back.
        // Close it anyway, so OnChangingState/OnChangedState stay paired and extensions are checked against
        // the leaf that is really active. An empty chain falls back to EmptyState, as at start, because every
        // later change and tick reads the leaf. A closing callback that throws must not hide the cause:
        // its exception joins the failures collected so far, and all go out after the cause.
        private void CloseFailedChange(Exception cause, List<Exception>? failures = null)
        {
            if (_currentStates.Count == 0)
                _currentStates.Add(new EmptyState());

            try
            {
                OnChangedState();
            }
            catch (Exception exception)
            {
                (failures ??= new List<Exception>()).Add(exception);
            }

            failures = DetachIncompatibleExtensions(failures);

            if (failures is not null)
            {
                failures.Insert(0, cause);
                throw new AggregateException("A state change failed, and closing it failed as well.", failures);
            }
        }

        // The active states are kept for the longest prefix whose types match the new chain from the root.
        private int FindDivergeIndex(List<Type> newChain)
        {
            var commonLength = Math.Min(_currentStates.Count, newChain.Count);
            for (var i = 0; i < commonLength; i++)
            {
                if (_currentStates[i].GetType() != newChain[i])
                    return i;
            }

            return commonLength;
        }

        /// <summary>
        /// Called before the state chain diff and exit/enter sequence begins.
        /// </summary>
        protected virtual void OnChangingState() { }

        /// <summary>
        /// Called after the exit/enter sequence ends, also when a state in it throws.
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
        /// A request made from an update controller is likewise queued until the tick finishes.
        /// </summary>
        private void Request(PendingRequest request)
        {
            if (_isChangingState || _isTicking)
            {
                _pendingRequests.Enqueue(request);
                return;
            }

            RunToCompletion(request);
        }

        private void RunToCompletion(PendingRequest request)
        {
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

        private List<Type> RentTypeChainBuffer() =>
            _typeChainBufferPool.Count > 0 ? _typeChainBufferPool.Pop() : new List<Type>(capacity: 4);

        private void ReturnTypeChainBuffer(List<Type> buffer)
        {
            buffer.Clear();
            _typeChainBufferPool.Push(buffer);
        }
        #endregion

        #region Exit
        private void ExitState(IState state)
        {
            // Extensions bound to this state live in a child of its scope, which Release is about to dispose.
            DetachExtensionsBoundTo(state);

            OnExitingState(state);
#if ENABLE_PROFILER
            using (GetMarkers(state).Exit.Auto())
#endif
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
#if ENABLE_PROFILER
            using (GetMarkers(state).Enter.Auto())
#endif
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

        #region Profiling
#if ENABLE_PROFILER
        // One set of markers per state type, created on first use so the hot path stays allocation-free.
        // Per machine rather than static: the cache is not synchronized, and machines may live on different
        // threads. The profiler identifies a marker by name, so every machine still reports into one entry.
        private readonly Dictionary<Type, StateMarkers> _stateMarkers = new();

        private StateMarkers GetMarkers(IState state)
        {
            var type = state.GetType();
            if (!_stateMarkers.TryGetValue(type, out var markers))
                _stateMarkers[type] = markers = new StateMarkers(GetMarkerTypeName(type));

            return markers;
        }

        // Full name with readable generic arguments, so two closings of one generic state get separate markers.
        // Nested types are joined with '.', and each segment keeps its own arguments: NS.Outer<System.Int32>.Inner.
        // ControllerGroupBody.GetMarkerTypeName names controller markers in the same format, tuples included.
        private static string GetMarkerTypeName(Type type)
        {
            if (type.IsArray)
                return $"{GetMarkerTypeName(type.GetElementType()!)}[{new string(',', type.GetArrayRank() - 1)}]";

            if (type.IsGenericParameter)
                return type.Name;

            var builder = new StringBuilder();
            if (!string.IsNullOrEmpty(type.Namespace))
                builder.Append(type.Namespace).Append('.');

            AppendMarkerTypeSegment(builder, type, type.GetGenericArguments());
            return builder.ToString();
        }

        // A nested type lists the arguments of its declaring types too. Each segment takes only the arguments
        // its own arity adds, so Outer<A>.Inner<B> does not collapse into Outer<A, B>. Returns the arguments used.
        private static int AppendMarkerTypeSegment(StringBuilder builder, Type segment, Type[] arguments)
        {
            var start = 0;
            if (segment.DeclaringType is { } declaringType)
            {
                start = AppendMarkerTypeSegment(builder, declaringType, arguments);
                builder.Append('.');
            }

            var name = segment.Name;
            var tickIndex = name.IndexOf('`');
            builder.Append(name, 0, tickIndex >= 0 ? tickIndex : name.Length);

            var end = segment.GetGenericArguments().Length;
            if (end > start)
            {
                builder.Append('<');
                for (var i = start; i < end; i++)
                {
                    if (i > start)
                        builder.Append(", ");

                    builder.Append(GetMarkerTypeName(arguments[i]));
                }
                builder.Append('>');
            }

            return end;
        }

        private sealed class StateMarkers
        {
            public readonly ProfilerMarker Enter;
            public readonly ProfilerMarker Exit;
            private readonly ProfilerMarker _update;
            private readonly ProfilerMarker _lateUpdate;
            private readonly ProfilerMarker _fixedUpdate;

            public StateMarkers(string typeName)
            {
                Enter = new ProfilerMarker($"HSM.Enter {typeName}");
                Exit = new ProfilerMarker($"HSM.Exit {typeName}");
                _update = new ProfilerMarker($"HSM.Update {typeName}");
                _lateUpdate = new ProfilerMarker($"HSM.LateUpdate {typeName}");
                _fixedUpdate = new ProfilerMarker($"HSM.FixedUpdate {typeName}");
            }

            public ProfilerMarker ForTick<TController>()
                where TController : IController
            {
                if (typeof(TController) == typeof(IUpdateController)) return _update;
                if (typeof(TController) == typeof(ILateUpdateController)) return _lateUpdate;
                return _fixedUpdate;
            }
        }
#endif
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
        /// <para>
        /// It also makes <see cref="TransitionVia{TTransition}"/> throw when the transition's source state is not
        /// active, instead of silently doing nothing.
        /// </para>
        /// </remarks>
        protected virtual bool StrictTransitions => false;
        #endregion
    }
}
