#nullable enable
using System;
using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public partial class StateMachineBase
    {
        // Completes once the in-flight core transition has unwound. It is assigned together with
        // _activeTransitionCts and before the core starts, so a call made from inside the core's first
        // synchronous segment also waits for the unwind. default(UniTask) is an already-completed task.
        private UniTask _activeTransitionUnwound;

        // The in-flight core transition, set and cleared together with _activeTransitionCts. A superseder and Dispose
        // record on it why they cancel before they call Cancel, so the caller's exception does not depend on timing.
        private TransitionRun? _activeTransitionRun;

        /// <summary>
        /// Asynchronously transitions to <typeparamref name="TState"/>. Cancels any in-progress
        /// async transition and waits for it to unwind before mutating the state chain, so two
        /// overlapping calls never corrupt <c>_currentStates</c>. States implementing
        /// <see cref="IAsyncEnterController"/> or <see cref="IAsyncExitController"/> are awaited;
        /// others fall back to synchronous controllers.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A cancelled transition stops waiting for its async enter/exit callback at once, even if the callback
        /// ignores the token. The callback keeps running on its own, so it must observe the token to stop early.
        /// </para>
        /// <para>
        /// For the same reason, an async enter callback can redirect the machine by awaiting this method:
        /// the new call supersedes the transition that runs the callback.
        /// </para>
        /// <para>
        /// A cancelled or failed transition rolls back: an exit that has started completes and releases its state,
        /// and every state below the point where the old and new chains diverge is exited and released. The chain
        /// ends at the ancestors both chains share, or at <see cref="EmptyState"/>.
        /// </para>
        /// </remarks>
        /// <typeparam name="TState">The target leaf state type.</typeparam>
        /// <param name="cancellationToken">Cancellation token for the transition.</param>
        /// <exception cref="InvalidOperationException">
        /// A synchronous state change is in progress, for example when called from a synchronous enter or exit callback.
        /// </exception>
        /// <exception cref="TransitionSupersededException">
        /// A newer async transition superseded this one. Failures of its rollback are in
        /// <see cref="TransitionSupersededException.RollbackExceptions"/>, not in an <see cref="AggregateException"/>.
        /// </exception>
        /// <exception cref="StateMachineDisposedException">
        /// The machine was disposed before the transition completed, also while it waited for an older one to unwind.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// <paramref name="cancellationToken"/> was cancelled, or an enter or exit callback cancelled itself.
        /// </exception>
        /// <exception cref="AggregateException">
        /// The transition failed or was cancelled by its own token or callback, and then a rollback exit,
        /// <see cref="OnChangedState"/> or an extension's detach threw too; the original exception comes first.
        /// Or several extensions threw while the transition detached them.
        /// </exception>
        public UniTask ChangeStateAsync<TState>(CancellationToken cancellationToken = default)
            where TState : IState =>
            ChangeStateAsync(typeof(TState), cancellationToken);

        /// <inheritdoc cref="ChangeStateAsync{TState}"/>
        /// <param name="stateType">The target leaf state type.</param>
        /// <param name="cancellationToken">Cancellation token for the transition.</param>
        /// <exception cref="ArgumentNullException"><paramref name="stateType"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="stateType"/> does not implement <see cref="IState"/>.</exception>
        public async UniTask ChangeStateAsync(Type stateType, CancellationToken cancellationToken = default)
        {
            ThrowIfNotStateType(stateType, nameof(stateType));

            // A disposed machine ignores state changes, also those a lifetime callback requests while Dispose runs.
            if (_isDisposed)
                return;

            ThrowIfSyncChangeInProgress();

            // Before anything runs: a cancelled transition would still exit the old states and enter nothing.
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsStateEnabled(stateType))
                return;

            await SupersedeActiveTransitionAsync(stateType);
            ThrowIfEndedWhileWaiting(cancellationToken);

            // Read the leaf only after the superseded transition has unwound: the edge being guarded
            // is the one actually taken, not the one that was current when this call was made.
            if (!IsTransitionEnabled(_currentStates[^1].GetType(), stateType))
                return;

            await RunTransitionCoreAsync(stateType, cancellationToken);
        }

        private async UniTask SupersedeActiveTransitionAsync(Type supersedingState)
        {
            // A loop, not a single check: several callers can wait for the same unwind. The first one to
            // resume starts a new transition, which the next one must supersede in turn.
            while (_activeTransitionCts is { } previous)
            {
                // Read before Cancel: the core can unwind inside Cancel and reset the fields.
                var unwound = _activeTransitionUnwound;
                _activeTransitionRun?.MarkSuperseded(supersedingState);
                previous.Cancel();
                await unwound;
            }
        }

        // Disposed or cancelled while the call waited for an older transition to unwind: it never starts, and no guard or
        // transition hook runs. A call made after Dispose is ignored at its entry instead.
        private void ThrowIfEndedWhileWaiting(CancellationToken cancellationToken)
        {
            if (_isDisposed)
                throw new StateMachineDisposedException(null, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
        }

        // Runs the core with no guard checks: every caller has checked the guards already.
        private async UniTask RunTransitionCoreAsync(Type stateType, CancellationToken cancellationToken)
        {
            // Guards and transition hooks run between the caller's supersede and this point.
            // If one of them started a transition, supersede it too: two cores must never run at once.
            await SupersedeActiveTransitionAsync(stateType);
            ThrowIfEndedWhileWaiting(cancellationToken);

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var run = new TransitionRun(linked.Token);
            var unwound = new UniTaskCompletionSource();
            _activeTransitionCts = linked;
            _activeTransitionRun = run;
            _activeTransitionUnwound = unwound.Task;
            var succeeded = false;
            try
            {
                await ChangeStateCoreAsync(stateType, run);
                succeeded = true;
            }
            catch (Exception exception) when (run.EndedBySupersedeOrDispose)
            {
                throw run.ToException(exception);
            }
            finally
            {
                if (ReferenceEquals(_activeTransitionCts, linked))
                {
                    _activeTransitionCts = null;
                    _activeTransitionRun = null;
                    _activeTransitionUnwound = default;
                }

                try
                {
                    // IsTransitioning is false here. Notified before a waiting superseder resumes, so the entries of
                    // this completed transition are consumed by it and cannot wait for the superseder's outcome.
                    if (succeeded)
                        NotifyEntered();
                }
                finally
                {
                    // Signal last: a waiting superseder resumes inside this call and must find the machine idle.
                    unwound.TrySetResult();
                }
            }
        }

        private async UniTask ChangeStateCoreAsync(Type stateType, TransitionRun run)
        {
            var token = run.Token;
            OnChangingState();
            {
                // Rented per in-flight transition: the chain is read across awaits, so a buffer shared
                // with the factory or with another transition would be rewritten underneath this loop.
                var newChain = RentTypeChainBuffer();
                var divergeIndex = -1;
                var awaitingEnteredStart = _awaitingEntered.Count;
                try
                {
                    _stateFactory.BuildTypeChain(stateType, newChain);
                    divergeIndex = FindDivergeIndex(newChain);

                    // An exit callback that disposes the machine stops the loop after its own state: Dispose does not
                    // exit states, and the next exit would release a state whose scope Dispose has destroyed.
                    for (var i = _currentStates.Count - 1; i >= divergeIndex && !_isDisposed; i--)
                    {
                        // An exit that has started always completes (see ExitStateAsync), so the state
                        // leaves the chain even when its exit is cancelled or throws.
                        try { await ExitStateAsync(_currentStates[i], token); }
                        finally { _currentStates.RemoveAt(i); }
                    }

                    for (var i = divergeIndex; i < newChain.Count; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        var state = _stateFactory.CreateState(newChain[i]);
                        _currentStates.Add(state);
                        await EnterStateAsync(state, token);
                    }

                    // Disposed by the last callback, with nothing left to exit or enter after it: the transition was
                    // still cut short. Dispose has cancelled the token, so this ends as a disposed transition.
                    if (_isDisposed)
                        throw new OperationCanceledException(token);
                }
                catch (Exception exception)
                {
                    // Decided here, when the failure starts: a cancellation requested later, during the rollback,
                    // did not cause it.
                    run.FailedAfterCancellation = token.IsCancellationRequested;
                    DropAwaitingEntered(awaitingEnteredStart);

                    // A chain that failed to build left the active chain as it was: nothing to roll back,
                    // but the change is still closed.
                    await RollBackAsync(divergeIndex >= 0 ? divergeIndex : _currentStates.Count, exception);
                    throw;
                }
                finally
                {
                    ReturnTypeChainBuffer(newChain);
                }
            }
            OnChangedState();
            AutoDetachIncompatibleExtensions();
        }

        // Why the running transition was cancelled. Whoever cancels it records the cause first; the first cause wins.
        private sealed class TransitionRun
        {
            private enum EndCause
            {
                None,
                Superseded,
                Disposed,
            }

            private EndCause _cause;
            private Type? _supersedingState;

            public TransitionRun(CancellationToken token) => Token = token;

            public CancellationToken Token { get; }

            // Whether the core's failure began after its token was cancelled, i.e. the cancellation caused it.
            public bool FailedAfterCancellation { get; set; }

            public bool EndedBySupersedeOrDispose => _cause != EndCause.None && FailedAfterCancellation;

            // A transition already cancelled by its caller's token keeps that cause: it ends as the caller asked, even if
            // a superseder or Dispose arrives while it is still unwinding.
            public void MarkSuperseded(Type supersedingState)
            {
                if (_cause != EndCause.None || Token.IsCancellationRequested)
                    return;

                _cause = EndCause.Superseded;
                _supersedingState = supersedingState;
            }

            public void MarkDisposed()
            {
                if (_cause == EndCause.None && !Token.IsCancellationRequested)
                    _cause = EndCause.Disposed;
            }

            // The core rethrows its cancellation, or an AggregateException with the cancellation first and the
            // failures of the rollback after it. Those failures go to RollbackExceptions instead of hiding the cause.
            public OperationCanceledException ToException(Exception exception)
            {
                var cause = exception;
                IReadOnlyList<Exception> rollbackExceptions = Array.Empty<Exception>();

                if (exception is AggregateException { InnerExceptions: { Count: > 0 } inner })
                {
                    cause = inner[0];
                    var rest = new Exception[inner.Count - 1];
                    for (var i = 1; i < inner.Count; i++)
                        rest[i - 1] = inner[i];

                    rollbackExceptions = rest;
                }

                return _cause == EndCause.Disposed
                    ? new StateMachineDisposedException(cause, Token)
                    : new TransitionSupersededException(_supersedingState!, rollbackExceptions, cause, Token);
            }
        }

        // Undoes a transition that was cancelled or threw: every state from divergeIndex down — old states not
        // yet exited and new states entered so far, including the one whose enter was interrupted — is exited
        // from the tail and released. What remains are the ancestors the old and new chains share, all fully
        // entered. Rollback exits are not cancellable, so no state is left half-exited. A state whose rollback
        // exit throws is still removed and released. The change is then closed the same way as a failed sync one.
        private async UniTask RollBackAsync(int divergeIndex, Exception cause)
        {
            List<Exception>? failures = null;

            // Dispose does not exit states; the ones this transition reached are disposed with the rest. A rollback
            // exit that disposes the machine stops the rollback after its own state the same way.
            for (var i = _currentStates.Count - 1; i >= divergeIndex && !_isDisposed; i--)
            {
                try { await ExitStateAsync(_currentStates[i], CancellationToken.None); }
                catch (Exception exception) { (failures ??= new List<Exception>()).Add(exception); }
                finally { _currentStates.RemoveAt(i); }
            }

            if (_isDisposed)
            {
                // Not closed: Dispose has detached every extension, and a disposed machine gets no OnChangedState.
                // The chain still keeps a leaf, as at start.
                if (_currentStates.Count == 0)
                    _currentStates.Add(new EmptyState());

                if (failures is not null)
                {
                    failures.Insert(0, cause);
                    throw new AggregateException("A state change failed, and rolling it back failed as well.", failures);
                }

                return;
            }

            CloseFailedChange(cause, failures);
        }

        // An exit, once started, always completes: IState.Exit and Release run even when the bound extensions'
        // detach, OnExitingState or the exit controllers are cancelled or throw. The callbacks that did not get to
        // run are not called again. OnExitedState runs only if OnExitingState did, so the hooks stay paired.
        private async UniTask ExitStateAsync(IState state, CancellationToken cancellationToken)
        {
            var isExiting = false;
            var isExited = false;
            try
            {
                EndStateLifetime(state);
                DetachExtensionsBoundTo(state);

                isExiting = true;
                OnExitingState(state);

                if (state is IAsyncExitController asyncExit)
                {
                    // Stop waiting on cancellation even if the callback ignores the token. A callback that awaits
                    // the superseding transition would otherwise deadlock: that transition waits for this unwind.
                    await asyncExit.OnExitAsync(cancellationToken).AttachExternalCancellation(cancellationToken);
                }
                else
                {
                    // No await here, so one sample, the same as the synchronous ExitState.
#if ENABLE_PROFILER
                    using (GetMarkers(state).Exit.Auto())
#endif
                    {
                        try
                        {
                            state.GetController<IExitController>()?.OnExit();
                        }
                        finally
                        {
                            isExited = true;
                            state.Exit();
                        }
                    }
                }
            }
            finally
            {
                try
                {
                    if (!isExited)
                    {
                        // Only synchronous segments are measured: a profiler sample cannot span an await.
#if ENABLE_PROFILER
                        using (GetMarkers(state).Exit.Auto())
#endif
                        state.Exit();
                    }
                }
                finally
                {
                    try
                    {
                        if (isExiting)
                            OnExitedState(state);
                    }
                    finally
                    {
                        _stateFactory.Release(state);
                    }
                }
            }
        }

        private async UniTask EnterStateAsync(IState state, CancellationToken cancellationToken)
        {
            OnEnteringState(state);
            {
                if (state is IAsyncEnterController asyncEnter)
                {
                    // Only synchronous segments are measured: a profiler sample cannot span an await.
#if ENABLE_PROFILER
                    using (GetMarkers(state).Enter.Auto())
#endif
                    {
                        _stateFactory.MarkInitialized(state);
                        state.Enter();
                    }

                    // Same as in ExitStateAsync: a redirecting callback awaits the transition that supersedes this one.
                    await asyncEnter.OnEnterAsync(cancellationToken).AttachExternalCancellation(cancellationToken);
                }
                else
                {
                    // No await here, so one sample, the same as the synchronous EnterState.
#if ENABLE_PROFILER
                    using (GetMarkers(state).Enter.Auto())
#endif
                    {
                        _stateFactory.MarkInitialized(state);
                        state.Enter();
                        state.GetController<IEnterController>()?.OnEnter();
                    }
                }
            }
            OnEnteredState(state);
            AwaitEntered(state);
        }
    }
}
