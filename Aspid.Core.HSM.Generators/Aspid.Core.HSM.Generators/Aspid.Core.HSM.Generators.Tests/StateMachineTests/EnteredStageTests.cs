using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

#region Helpers

/// <summary>A state whose entered stage is <see cref="Stage"/>; optionally a failure handler too.</summary>
public abstract class StagedState : BaseTestState, IAsyncEnteredController
{
    public Func<StateEntry, UniTask>? Stage { get; set; }

    public int StageStarted { get; private set; }

    public StateEntry? Entry { get; private set; }

    public UniTask OnEnteredAsync(StateEntry entry)
    {
        StageStarted++;
        Entry = entry;
        return Stage?.Invoke(entry) ?? UniTask.CompletedTask;
    }
}

/// <summary>Root of the branch; handles the failures of the stages below it when <see cref="Handles"/> is set.</summary>
public sealed class StageBranch : StagedState, IEnteredFailedController
{
    public bool Handles { get; set; } = true;

    public List<(IState State, Exception Exception)> Failures { get; } = [];

    public Action? ThrowInHandler { get; set; }

    public void OnEnteredFailed(IState state, Exception exception)
    {
        if (!Handles)
            throw new InvalidOperationException("not expected");

        ThrowInHandler?.Invoke();
        Failures.Add((state, exception));
    }
}

public sealed class StageLoading : StagedState, IChildState<StageBranch>;

public sealed class StageDriving : StagedState, IChildState<StageBranch>;

public sealed class StageOther : StagedState, IChildState<StageBranch>;

/// <summary>Handles its own stage's failures, ahead of the branch.</summary>
public sealed class StageWithOwnHandler : StagedState, IChildState<StageBranch>, IEnteredFailedController
{
    public List<Exception> Failures { get; } = [];

    public void OnEnteredFailed(IState state, Exception exception) => Failures.Add(exception);
}

public sealed class StageThrowingEnter : BaseTestState, IChildState<StageBranch>, IAsyncEnterController
{
    public UniTask OnEnterAsync(CancellationToken cancellationToken) =>
        throw new InvalidOperationException("enter failed");
}

public sealed class StageSelfCancellingEnter : BaseTestState, IChildState<StageBranch>, IAsyncEnterController
{
    public UniTask OnEnterAsync(CancellationToken cancellationToken)
    {
        using var own = new CancellationTokenSource();
        own.Cancel();
        return UniTask.FromCanceled(own.Token);
    }
}

public sealed class StageBlockingEnter : BaseTestState, IChildState<StageBranch>, IAsyncEnterController
{
    public UniTask OnEnterAsync(CancellationToken cancellationToken) => UniTask.Never(cancellationToken);
}

public sealed class StageNonGenericChild : BaseTestState, IChildState
{
    public Type ParentState => typeof(StageBranch);
}

/// <summary>A root outside the branch whose enter throws: a transition to it leaves the branch before it fails.</summary>
public sealed class StageThrowingRoot : BaseTestState, IAsyncEnterController
{
    public UniTask OnEnterAsync(CancellationToken cancellationToken) =>
        throw new InvalidOperationException("root enter failed");
}

public sealed class StageLoadingToThrowing : ITransition<StageLoading, StageThrowingEnter>;

/// <summary>Implements both entered interfaces: only the async one may run.</summary>
public sealed class StageBoth : StagedState, IChildState<StageBranch>, IEnteredController
{
    public int SyncCalled { get; private set; }

    public void OnEntered(StateEntry entry) => SyncCalled++;
}

public sealed class StageMachine(StateFactory factory) : StateMachineBase(factory)
{
    public List<(IState State, Exception Exception)> Fallback { get; } = [];

    protected override void OnEnteredFailed(IState state, Exception exception) => Fallback.Add((state, exception));
}

#endregion

// The async counterpart of IEnteredController had to be wired by hand in every state, with the failure policy and the
// "transition through the stage" in a consumer base class. The machine now runs the stage and reports its outcome.
public class EnteredStageTests
{
    private readonly StageBranch _branch = new();
    private readonly StageLoading _loading = new();
    private readonly StageDriving _driving = new();
    private readonly StageOther _other = new();
    private readonly StageWithOwnHandler _ownHandler = new();
    private readonly StageBoth _both = new();
    private readonly TestStateFactory _factory = new();
    private readonly StageMachine _sm;

    public EnteredStageTests()
    {
        _factory.RegisterState(() => _branch);
        _factory.RegisterState(() => _loading);
        _factory.RegisterState(() => _driving);
        _factory.RegisterState(() => _other);
        _factory.RegisterState(() => _ownHandler);
        _factory.RegisterState(() => _both);
        _factory.RegisterState<StageThrowingEnter>();
        _factory.RegisterState<StageSelfCancellingEnter>();
        _factory.RegisterState<StageBlockingEnter>();
        _factory.RegisterState<StageNonGenericChild>();
        _factory.RegisterState<StageThrowingRoot>();
        _sm = new StageMachine(_factory);
    }

    private Exception SingleFailure() => Assert.Single(_branch.Failures).Exception;

    private void AssertSilent()
    {
        Assert.Empty(_branch.Failures);
        Assert.Empty(_ownHandler.Failures);
        Assert.Empty(_sm.Fallback);
    }

    [Fact]
    public async Task The_stage_runs_after_the_transition_and_moves_on_through_its_entry()
    {
        var finish = new UniTaskCompletionSource();
        var transitioningAtStart = (bool?)null;
        _loading.Stage = async entry =>
        {
            transitioningAtStart = _sm.IsTransitioning;
            await finish.Task;
            await entry.ChangeStateAsync<StageDriving>();
        };

        await _sm.ChangeStateAsync<StageLoading>();
        Assert.Equal(1, _loading.StageStarted);
        Assert.False(transitioningAtStart);

        finish.TrySetResult();

        Assert.IsType<StageDriving>(_sm.CurrentStates[^1]);
        Assert.Equal(1, _driving.StageStarted);
        AssertSilent();
    }

    [Fact]
    public void A_sync_ChangeState_starts_the_stage_too()
    {
        _sm.ChangeState<StageLoading>();

        Assert.Equal(1, _branch.StageStarted);
        Assert.Equal(1, _loading.StageStarted);
    }

    [Fact]
    public async Task A_state_with_both_entered_interfaces_runs_only_the_async_one()
    {
        await _sm.ChangeStateAsync<StageBoth>();

        Assert.Equal(1, _both.StageStarted);
        Assert.Equal(0, _both.SyncCalled);
    }

    [Fact]
    public async Task Exiting_the_state_mid_stage_is_silent()
    {
        _loading.Stage = entry => UniTask.Never(entry.Lifetime);
        await _sm.ChangeStateAsync<StageLoading>();

        await _sm.ChangeStateAsync<StageOther>();

        Assert.True(_loading.Entry!.Lifetime.IsCancellationRequested);
        AssertSilent();
    }

    [Fact]
    public async Task A_wait_on_a_token_linked_to_the_lifetime_is_silent_on_exit()
    {
        _loading.Stage = async entry =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(entry.Lifetime);
            await UniTask.Never(linked.Token);
        };
        await _sm.ChangeStateAsync<StageLoading>();

        await _sm.ChangeStateAsync<StageOther>();

        AssertSilent();
    }

    [Fact]
    public async Task A_superseded_next_transition_is_silent()
    {
        _loading.Stage = entry => entry.ChangeStateAsync<StageBlockingEnter>();
        await _sm.ChangeStateAsync<StageLoading>();

        await _sm.ChangeStateAsync<StageOther>();

        AssertSilent();
    }

    [Fact]
    public async Task Dispose_mid_stage_is_silent()
    {
        _loading.Stage = entry => UniTask.Never(entry.Lifetime);
        await _sm.ChangeStateAsync<StageLoading>();

        _sm.Dispose();

        AssertSilent();
    }

    [Fact]
    public async Task Dispose_during_the_stages_transition_is_silent()
    {
        _loading.Stage = entry => entry.ChangeStateAsync<StageBlockingEnter>();
        await _sm.ChangeStateAsync<StageLoading>();

        _sm.Dispose();

        AssertSilent();
    }

    [Fact]
    public async Task A_throwing_stage_reaches_the_nearest_handler_up_the_chain()
    {
        _loading.Stage = _ => throw new InvalidOperationException("stage failed");

        await _sm.ChangeStateAsync<StageLoading>();

        var (state, exception) = Assert.Single(_branch.Failures);
        Assert.Same(_loading, state);
        Assert.Equal("stage failed", exception.Message);
    }

    [Fact]
    public async Task The_failed_states_own_handler_comes_first()
    {
        _ownHandler.Stage = _ => throw new InvalidOperationException("stage failed");

        await _sm.ChangeStateAsync<StageWithOwnHandler>();

        Assert.Single(_ownHandler.Failures);
        Assert.Empty(_branch.Failures);
    }

    [Fact]
    public async Task Without_a_handler_the_machine_fallback_runs()
    {
        var bare = new TestStateFactory();
        bare.RegisterState(() => new StageLoadingRoot { Stage = _ => throw new InvalidOperationException("root failed") });
        var sm = new StageMachine(bare);

        await sm.ChangeStateAsync<StageLoadingRoot>();

        Assert.Equal("root failed", Assert.Single(sm.Fallback).Exception.Message);
    }

    [Fact]
    public async Task A_next_transition_that_fails_before_the_exit_is_a_failure()
    {
        _loading.Stage = entry => entry.ChangeStateAsync<StageNonGenericChild>();

        await _sm.ChangeStateAsync<StageLoading>();

        Assert.IsType<InvalidOperationException>(SingleFailure());
        Assert.IsType<StageLoading>(_sm.CurrentStates[^1]);
    }

    // The transition exits the stage's state before it fails, so the lifetime is cancelled by then: the failure must still
    // be reported.
    [Fact]
    public async Task A_next_transition_that_fails_after_the_exit_is_a_failure()
    {
        _loading.Stage = entry => entry.ChangeStateAsync<StageThrowingEnter>();

        await _sm.ChangeStateAsync<StageLoading>();

        Assert.Equal("enter failed", SingleFailure().Message);
        Assert.True(_loading.Entry!.Lifetime.IsCancellationRequested);
    }

    [Fact]
    public async Task A_next_enter_that_cancels_itself_is_a_failure()
    {
        _loading.Stage = entry => entry.ChangeStateAsync<StageSelfCancellingEnter>();

        await _sm.ChangeStateAsync<StageLoading>();

        Assert.IsType<OperationCanceledException>(SingleFailure());
    }

    [Fact]
    public async Task A_transition_failure_the_stage_handled_is_not_reported_on_the_later_exit()
    {
        _loading.Stage = async entry =>
        {
            try { await entry.ChangeStateAsync<StageNonGenericChild>(); }
            catch (InvalidOperationException) { }

            await UniTask.Never(entry.Lifetime);
        };
        await _sm.ChangeStateAsync<StageLoading>();

        await _sm.ChangeStateAsync<StageOther>();

        AssertSilent();
    }

    // The parent's lifetime outlives a change between its children: its stage goes on, the old leaf's stage stops.
    [Fact]
    public async Task A_leaf_moving_to_a_sibling_leaves_the_parents_stage_running()
    {
        var parentRunning = true;
        _branch.Stage = async entry =>
        {
            await UniTask.Never(entry.Lifetime).SuppressCancellationThrow();
            parentRunning = false;
        };
        _loading.Stage = entry => UniTask.Never(entry.Lifetime);
        await _sm.ChangeStateAsync<StageLoading>();

        await _sm.ChangeStateAsync<StageDriving>();

        Assert.True(parentRunning);
        Assert.False(_branch.Entry!.Lifetime.IsCancellationRequested);
        Assert.True(_loading.Entry!.Lifetime.IsCancellationRequested);
        Assert.Equal(1, _branch.StageStarted);
        AssertSilent();
    }

    [Fact]
    public async Task The_branch_handler_sees_a_failed_transition_out_of_the_branch()
    {
        _loading.Stage = entry => entry.ChangeStateAsync<StageThrowingRoot>();

        await _sm.ChangeStateAsync<StageLoading>();

        Assert.DoesNotContain(_branch, _sm.CurrentStates);
        Assert.Equal("root enter failed", SingleFailure().Message);
        Assert.Empty(_sm.Fallback);
    }

    [Fact]
    public async Task The_own_handler_is_asked_after_its_state_exited()
    {
        _ownHandler.Stage = entry => entry.ChangeStateAsync<StageThrowingEnter>();

        await _sm.ChangeStateAsync<StageWithOwnHandler>();

        Assert.Equal("enter failed", Assert.Single(_ownHandler.Failures).Message);
        Assert.Empty(_branch.Failures);
    }

    [Fact]
    public async Task A_stage_that_outlived_its_state_does_not_move_the_machine()
    {
        var finish = new UniTaskCompletionSource();
        _loading.Stage = async entry =>
        {
            await finish.Task;
            await entry.ChangeStateAsync<StageDriving>();
        };
        await _sm.ChangeStateAsync<StageLoading>();
        await _sm.ChangeStateAsync<StageOther>();

        finish.TrySetResult();

        Assert.IsType<StageOther>(_sm.CurrentStates[^1]);
        Assert.Equal(0, _driving.StageStarted);
        AssertSilent();
    }

    [Fact]
    public async Task A_handled_failure_is_not_reported_when_a_linked_wait_ends_on_exit()
    {
        _loading.Stage = async entry =>
        {
            try { await entry.ChangeStateAsync<StageNonGenericChild>(); }
            catch (InvalidOperationException) { }

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(entry.Lifetime);
            await UniTask.Never(linked.Token);
        };
        await _sm.ChangeStateAsync<StageLoading>();

        await _sm.ChangeStateAsync<StageOther>();

        AssertSilent();
    }

    [Fact]
    public async Task A_cancellation_of_the_stage_itself_is_reported_as_it_is()
    {
        _loading.Stage = async entry =>
        {
            try { await entry.ChangeStateAsync<StageNonGenericChild>(); }
            catch (InvalidOperationException) { }

            using var timeout = new CancellationTokenSource();
            timeout.Cancel();
            await UniTask.Never(timeout.Token);
        };

        await _sm.ChangeStateAsync<StageLoading>();

        Assert.IsType<OperationCanceledException>(SingleFailure());
    }

    [Fact]
    public async Task A_failure_after_Dispose_asks_no_handler()
    {
        var published = new List<Exception>();
        void OnUnobserved(Exception exception) => published.Add(exception);
        var finish = new UniTaskCompletionSource();
        _loading.Stage = async _ =>
        {
            await finish.Task;
            throw new InvalidOperationException("late failure");
        };
        await _sm.ChangeStateAsync<StageLoading>();
        _sm.Dispose();

        UniTaskScheduler.UnobservedTaskException += OnUnobserved;
        try
        {
            finish.TrySetResult();
        }
        finally
        {
            UniTaskScheduler.UnobservedTaskException -= OnUnobserved;
        }

        AssertSilent();
        Assert.Contains(published, exception => exception.Message == "late failure");
    }

    [Fact]
    public async Task A_failed_TransitionToAsync_through_the_entry_is_reported()
    {
        _sm.RegisterTransition(new StageLoadingToThrowing());
        _loading.Stage = entry => entry.TransitionToAsync<StageThrowingEnter>();

        await _sm.ChangeStateAsync<StageLoading>();

        Assert.Equal("enter failed", SingleFailure().Message);
    }

    [Fact]
    public async Task A_failed_TransitionViaAsync_through_the_entry_is_reported()
    {
        _sm.RegisterTransition(new StageLoadingToThrowing());
        _loading.Stage = entry => entry.TransitionViaAsync<StageLoadingToThrowing>();

        await _sm.ChangeStateAsync<StageLoading>();

        Assert.Equal("enter failed", SingleFailure().Message);
    }

    [Fact]
    public async Task A_throwing_handler_does_not_escape()
    {
        var published = new List<Exception>();
        void OnUnobserved(Exception exception) => published.Add(exception);
        _branch.ThrowInHandler = () => throw new InvalidOperationException("handler failed");
        _loading.Stage = _ => throw new InvalidOperationException("stage failed");

        UniTaskScheduler.UnobservedTaskException += OnUnobserved;
        try
        {
            await _sm.ChangeStateAsync<StageLoading>();
        }
        finally
        {
            UniTaskScheduler.UnobservedTaskException -= OnUnobserved;
        }

        Assert.Contains(published, exception => exception.ToString().Contains("handler failed"));
    }
}

public sealed class StageLoadingRoot : StagedState;
