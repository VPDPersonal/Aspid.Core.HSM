using System;
using System.Threading;
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
        /// the new call supersedes the transition that runs the callback. An async exit callback must not do this:
        /// its state stays active, so the new transition exits it again and the callback runs again.
        /// </para>
        /// </remarks>
        /// <typeparam name="TState">The target leaf state type.</typeparam>
        /// <param name="cancellationToken">Cancellation token for the transition.</param>
        /// <exception cref="InvalidOperationException">
        /// A synchronous state change is in progress, for example when called from a synchronous enter or exit callback.
        /// </exception>
        public UniTask ChangeStateAsync<TState>(CancellationToken cancellationToken = default)
            where TState : IState =>
            ChangeStateAsync(typeof(TState), cancellationToken);

        /// <inheritdoc cref="ChangeStateAsync{TState}"/>
        /// <param name="stateType">The target leaf state type.</param>
        /// <param name="cancellationToken">Cancellation token for the transition.</param>
        public async UniTask ChangeStateAsync(Type stateType, CancellationToken cancellationToken = default)
        {
            ThrowIfSyncChangeInProgress();

            if (!IsStateEnabled(stateType))
                return;

            await SupersedeActiveTransitionAsync();

            // Read the leaf only after the superseded transition has unwound: the edge being guarded
            // is the one actually taken, not the one that was current when this call was made.
            if (!IsTransitionEnabled(_currentStates[^1].GetType(), stateType))
                return;

            await RunTransitionCoreAsync(stateType, cancellationToken);
        }

        private async UniTask SupersedeActiveTransitionAsync()
        {
            // A loop, not a single check: several callers can wait for the same unwind. The first one to
            // resume starts a new transition, which the next one must supersede in turn.
            while (_activeTransitionCts is { } previous)
            {
                // Read before Cancel: the core can unwind inside Cancel and reset the field.
                var unwound = _activeTransitionUnwound;
                previous.Cancel();
                await unwound;
            }
        }

        // Runs the core with no guard checks: every caller has checked the guards already.
        private async UniTask RunTransitionCoreAsync(Type stateType, CancellationToken cancellationToken)
        {
            // Guards and transition hooks run between the caller's supersede and this point.
            // If one of them started a transition, supersede it too: two cores must never run at once.
            await SupersedeActiveTransitionAsync();

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var unwound = new UniTaskCompletionSource();
            _activeTransitionCts = linked;
            _activeTransitionUnwound = unwound.Task;
            try
            {
                await ChangeStateCoreAsync(stateType, linked.Token);
            }
            finally
            {
                if (ReferenceEquals(_activeTransitionCts, linked))
                {
                    _activeTransitionCts = null;
                    _activeTransitionUnwound = default;
                }

                // Signal last: a waiting superseder resumes inside this call and must find the machine idle.
                unwound.TrySetResult();
            }
        }

        private async UniTask ChangeStateCoreAsync(Type stateType, CancellationToken token)
        {
            OnChangingState();
            {
                // Rented per in-flight transition: the chain is read across awaits, so a buffer shared
                // with the factory or with another transition would be rewritten underneath this loop.
                var newChain = RentChainBuffer();
                try
                {
                    _stateFactory.CreateState(stateType, _currentStates, newChain);
                    var divergeIndex = FindDivergeIndex(newChain);

                    for (var i = _currentStates.Count - 1; i >= divergeIndex; i--)
                    {
                        await ExitStateAsync(_currentStates[i], token);
                        _currentStates.RemoveAt(i);
                    }

                    for (var i = divergeIndex; i < newChain.Count; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        var state = newChain[i];
                        _currentStates.Add(state);
                        await EnterStateAsync(state, token);
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

        private async UniTask ExitStateAsync(IState state, CancellationToken cancellationToken)
        {
            OnExitingState(state);
            {
                // Stop waiting on cancellation even if the callback ignores the token. A callback that awaits
                // the superseding transition would otherwise deadlock: that transition waits for this unwind.
                if (state is IAsyncExitController asyncExit)
                    await asyncExit.OnExitAsync(cancellationToken).AttachExternalCancellation(cancellationToken);
                else
                    state.GetController<IExitController>()?.OnExit();
                state.Exit();
            }
            OnExitedState(state);

            _stateFactory.Release(state);
        }

        private async UniTask EnterStateAsync(IState state, CancellationToken cancellationToken)
        {
            OnEnteringState(state);
            {
                _stateFactory.MarkInitialized(state);
                state.Enter();
                // Same as in ExitStateAsync: a redirecting callback awaits the transition that supersedes this one.
                if (state is IAsyncEnterController asyncEnter)
                    await asyncEnter.OnEnterAsync(cancellationToken).AttachExternalCancellation(cancellationToken);
                else
                    state.GetController<IEnterController>()?.OnEnter();
            }
            OnEnteredState(state);
        }
    }
}
