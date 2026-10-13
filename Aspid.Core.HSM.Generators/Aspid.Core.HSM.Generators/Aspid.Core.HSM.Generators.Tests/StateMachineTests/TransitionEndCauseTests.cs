using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

#region Helpers

public sealed class CauseRoot : BaseTestState;

/// <summary>Async enter that waits until its token is cancelled.</summary>
public class CauseBlockingLeaf : BaseTestState, IChildState<CauseRoot>, IAsyncEnterController
{
    public UniTask OnEnterAsync(CancellationToken cancellationToken) => UniTask.Never(cancellationToken);
}

public sealed class CauseOtherBlockingLeaf : CauseBlockingLeaf;

/// <summary>Fully synchronous leaf: a transition into it completes inside the unwind of the one it supersedes.</summary>
public sealed class CauseSyncLeaf : BaseTestState, IChildState<CauseRoot>;

/// <summary>Async enter that cancels itself with its own token, as a factory that gives up on a load would.</summary>
public sealed class CauseSelfCancellingLeaf : BaseTestState, IChildState<CauseRoot>, IAsyncEnterController
{
    public UniTask OnEnterAsync(CancellationToken cancellationToken)
    {
        using var own = new CancellationTokenSource();
        own.Cancel();
        return UniTask.FromCanceled(own.Token);
    }
}

/// <summary>Blocking enter whose exit throws, so rolling it back fails.</summary>
public sealed class CauseFailingRollbackLeaf : CauseBlockingLeaf, IExitController
{
    public void OnExit() => throw new InvalidOperationException("rollback exit failed");
}

public sealed class RootToSyncLeafTransition : ITransition<CauseRoot, CauseSyncLeaf>
{
    public int BeforeCount { get; private set; }

    public void OnBeforeTransition() => BeforeCount++;
}

/// <summary>Blocking enter, and an async exit that holds the rollback until <see cref="ReleaseExit"/>.</summary>
public sealed class CauseSlowRollbackLeaf : BaseTestState, IChildState<CauseRoot>, IAsyncEnterController, IAsyncExitController
{
    private readonly UniTaskCompletionSource _exit = new();

    public void ReleaseExit() => _exit.TrySetResult();

    public UniTask OnEnterAsync(CancellationToken cancellationToken) => UniTask.Never(cancellationToken);

    public UniTask OnExitAsync(CancellationToken cancellationToken) => _exit.Task;
}

#endregion

// ChangeStateAsync ended with the same OperationCanceledException whether a newer transition superseded it, Dispose cut it
// short or its own enter cancelled itself. Callers guessed from IsTransitioning, which a synchronous superseder had
// already reset by the time the exception arrived.
public class TransitionEndCauseTests
{
    private readonly TestStateFactory _factory = new();
    private readonly CauseSlowRollbackLeaf _slowRollback = new();
    private readonly GuardedStateMachine _sm;

    public TransitionEndCauseTests()
    {
        _factory.RegisterState<CauseRoot>();
        _factory.RegisterState<CauseBlockingLeaf>();
        _factory.RegisterState<CauseOtherBlockingLeaf>();
        _factory.RegisterState<CauseSyncLeaf>();
        _factory.RegisterState<CauseSelfCancellingLeaf>();
        _factory.RegisterState<CauseFailingRollbackLeaf>();
        _factory.RegisterState(() => _slowRollback);
        _sm = new GuardedStateMachine(_factory);
        _sm.ChangeState<CauseRoot>();
    }

    [Fact]
    public async Task Superseded_by_an_async_transition()
    {
        var first = _sm.ChangeStateAsync<CauseBlockingLeaf>();
        var second = _sm.ChangeStateAsync<CauseOtherBlockingLeaf>();

        var exception = await Assert.ThrowsAsync<TransitionSupersededException>(async () => await first);

        Assert.Equal(typeof(CauseOtherBlockingLeaf), exception.SupersedingState);
        Assert.Empty(exception.RollbackExceptions);
        Assert.True(_sm.IsTransitioning);
        _sm.Dispose();
        await Assert.ThrowsAsync<StateMachineDisposedException>(async () => await second);
    }

    // The synchronous superseder completes inside the first one's unwind, so IsTransitioning is already false when the
    // exception reaches the caller. The cause is recorded at cancellation and does not change.
    [Fact]
    public async Task Superseded_by_a_fully_synchronous_transition()
    {
        var first = _sm.ChangeStateAsync<CauseBlockingLeaf>();
        var second = _sm.ChangeStateAsync<CauseSyncLeaf>();

        var exception = await Assert.ThrowsAsync<TransitionSupersededException>(async () => await first);
        await second;

        Assert.False(_sm.IsTransitioning);
        Assert.Equal(typeof(CauseSyncLeaf), exception.SupersedingState);
        Assert.IsType<CauseSyncLeaf>(_sm.CurrentStates[^1]);
    }

    [Fact]
    public async Task Dispose_during_an_async_enter()
    {
        var task = _sm.ChangeStateAsync<CauseBlockingLeaf>();

        _sm.Dispose();

        await Assert.ThrowsAsync<StateMachineDisposedException>(async () => await task);
    }

    [Fact]
    public async Task Dispose_while_waiting_for_an_older_transition_to_unwind()
    {
        var older = _sm.ChangeStateAsync<CauseBlockingLeaf>();
        var waiting = _sm.ChangeStateAsync<CauseOtherBlockingLeaf>();

        _sm.Dispose();

        await Assert.ThrowsAsync<TransitionSupersededException>(async () => await older);
        await Assert.ThrowsAsync<StateMachineDisposedException>(async () => await waiting);
    }

    // A call that waited for an older transition's rollback used to go on after Dispose: it ran the guards and, when a
    // guard said no, completed as if nothing had happened.
    [Fact]
    public async Task A_call_waiting_for_a_rollback_is_cut_short_by_Dispose_before_any_guard()
    {
        var older = _sm.ChangeStateAsync<CauseSlowRollbackLeaf>();
        var waiting = _sm.ChangeStateAsync<CauseSyncLeaf>();
        _sm.EdgeGuard = (_, _) => false;
        _sm.ObservedEdges.Clear();

        _sm.Dispose();
        _slowRollback.ReleaseExit();

        await Assert.ThrowsAsync<TransitionSupersededException>(async () => await older);
        await Assert.ThrowsAsync<StateMachineDisposedException>(async () => await waiting);
        Assert.Empty(_sm.ObservedEdges);
    }

    [Fact]
    public async Task A_TransitionToAsync_waiting_for_a_rollback_runs_no_hook_after_Dispose()
    {
        var transition = new RootToSyncLeafTransition();
        _sm.RegisterTransition(transition);
        var older = _sm.ChangeStateAsync<CauseSlowRollbackLeaf>();
        var waiting = _sm.TransitionToAsync<CauseSyncLeaf>();

        _sm.Dispose();
        _slowRollback.ReleaseExit();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await older);
        await Assert.ThrowsAsync<StateMachineDisposedException>(async () => await waiting);
        Assert.Equal(0, transition.BeforeCount);
    }

    // The caller cancelled first; a superseder that arrives while the rollback still runs must not take over the cause.
    [Fact]
    public async Task The_callers_cancellation_wins_over_a_later_supersede()
    {
        using var cts = new CancellationTokenSource();
        var first = _sm.ChangeStateAsync<CauseSlowRollbackLeaf>(cts.Token);
        cts.Cancel();

        var second = _sm.ChangeStateAsync<CauseSyncLeaf>();
        _slowRollback.ReleaseExit();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await first);
        await second;

        Assert.IsType<OperationCanceledException>(exception);
        Assert.IsType<CauseSyncLeaf>(_sm.CurrentStates[^1]);
    }

    [Fact]
    public async Task An_enter_that_cancels_itself_is_a_plain_cancellation()
    {
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await _sm.ChangeStateAsync<CauseSelfCancellingLeaf>());

        Assert.IsType<OperationCanceledException>(exception);
        Assert.IsType<CauseRoot>(Assert.Single(_sm.CurrentStates));
    }

    [Fact]
    public async Task The_callers_token_is_a_plain_cancellation()
    {
        using var cts = new CancellationTokenSource();
        var task = _sm.ChangeStateAsync<CauseBlockingLeaf>(cts.Token);

        cts.Cancel();
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);

        Assert.IsType<OperationCanceledException>(exception);
    }

    // A rollback that throws used to turn the superseded transition into an AggregateException, indistinguishable from a
    // real failure. The cause stays first-class and the rollback failures ride along.
    [Fact]
    public async Task A_failed_rollback_does_not_hide_the_supersede()
    {
        var first = _sm.ChangeStateAsync<CauseFailingRollbackLeaf>();
        var second = _sm.ChangeStateAsync<CauseSyncLeaf>();

        var exception = await Assert.ThrowsAsync<TransitionSupersededException>(async () => await first);
        await second;

        var rollback = Assert.Single(exception.RollbackExceptions);
        Assert.Equal("rollback exit failed", rollback.Message);
        Assert.IsAssignableFrom<OperationCanceledException>(exception.InnerException);
    }

    [Fact]
    public async Task TransitionToAsync_reports_the_supersede()
    {
        var first = _sm.TransitionToAsync<CauseBlockingLeaf>();
        var second = _sm.TransitionToAsync<CauseSyncLeaf>();

        var exception = await Assert.ThrowsAsync<TransitionSupersededException>(async () => await first);
        await second;

        Assert.Equal(typeof(CauseSyncLeaf), exception.SupersedingState);
    }

    [Fact]
    public async Task TransitionViaAsync_reports_the_supersede_with_its_target()
    {
        _sm.RegisterTransition(new RootToSyncLeafTransition());
        var first = _sm.ChangeStateAsync<CauseBlockingLeaf>();
        var second = _sm.TransitionViaAsync<RootToSyncLeafTransition>();

        var exception = await Assert.ThrowsAsync<TransitionSupersededException>(async () => await first);
        await second;

        Assert.Equal(typeof(CauseSyncLeaf), exception.SupersedingState);
    }

    [Fact]
    public async Task Old_catch_blocks_still_catch_every_cause()
    {
        var first = _sm.ChangeStateAsync<CauseBlockingLeaf>();
        _ = _sm.ChangeStateAsync<CauseSyncLeaf>();

        try
        {
            await first;
            Assert.Fail("The superseded transition completed.");
        }
        catch (OperationCanceledException)
        {
        }
    }
}
