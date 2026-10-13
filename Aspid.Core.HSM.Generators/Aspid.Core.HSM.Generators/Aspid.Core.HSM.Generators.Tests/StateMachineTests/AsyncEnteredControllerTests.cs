using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

#region Helpers

public sealed class StageRoot : BaseTestState;

public sealed class StageNext : BaseTestState, IChildState<StageRoot>;

public sealed class StageElsewhere : BaseTestState, IChildState<StageRoot>;

public sealed class StageSelfCancellingNext : BaseTestState, IChildState<StageRoot>, IAsyncEnterController
{
    public UniTask OnEnterAsync(CancellationToken cancellationToken)
    {
        using var own = new CancellationTokenSource();
        own.Cancel();
        return UniTask.FromCanceled(own.Token);
    }
}

public sealed class StageBlockingNext : BaseTestState, IChildState<StageRoot>, IAsyncEnterController
{
    public UniTask OnEnterAsync(CancellationToken cancellationToken) => UniTask.Never(cancellationToken);
}

/// <summary>Waits for <see cref="Signal"/>, then runs <see cref="Next"/>.</summary>
public sealed class FinishStage(IStateMachine stateMachine) : AsyncEnteredController(stateMachine)
{
    private readonly UniTaskCompletionSource _signal = new();

    public Func<FinishStage, UniTask>? Next { get; set; }

    public Exception? ThrowInRun { get; set; }

    public List<Exception> Failures { get; } = [];

    public bool Finished { get; private set; }

    /// <summary>When set, RunAsync goes on waiting for the state's lifetime after Next, as a stage that handled it would.</summary>
    public bool WaitAfterNext { get; set; }

    public void Signal() => _signal.TrySetResult();

    public UniTask Go<TState>() where TState : IState => ChangeStateAsync<TState>();

    protected override async UniTask RunAsync(CancellationToken stateLifetime)
    {
        if (ThrowInRun is { } exception)
            throw exception;

        await _signal.Task.AttachExternalCancellation(stateLifetime);
        await Next!(this);
        Finished = true;

        if (WaitAfterNext)
            await UniTask.Never(stateLifetime);
    }

    protected override void OnFailed(Exception exception) => Failures.Add(exception);
}

/// <summary>Hands OnEntered to its stage, as a [ControllerGroup] holding it would.</summary>
public sealed class StageState(FinishStage stage) : BaseTestState, IChildState<StageRoot>, IEnteredController
{
    void IEnteredController.OnEntered(CancellationToken stateLifetime) =>
        ((IEnteredController)stage).OnEntered(stateLifetime);
}

#endregion

public class AsyncEnteredControllerTests
{
    private readonly TestStateFactory _factory = new();
    private readonly TestableStateMachine _sm;
    private readonly FinishStage _stage;

    public AsyncEnteredControllerTests()
    {
        _sm = new TestableStateMachine(_factory);
        _stage = new FinishStage(_sm);
        _factory.RegisterState<StageRoot>();
        _factory.RegisterState<StageNext>();
        _factory.RegisterState<StageElsewhere>();
        _factory.RegisterState<StageSelfCancellingNext>();
        _factory.RegisterState<StageBlockingNext>();
        _factory.RegisterState(() => new StageState(_stage));
    }

    [Fact]
    public async Task Runs_after_the_enter_and_changes_state_on_its_event()
    {
        _stage.Next = stage => stage.Go<StageNext>();
        await _sm.ChangeStateAsync<StageState>();

        _stage.Signal();

        Assert.True(_stage.Finished);
        Assert.IsType<StageNext>(_sm.CurrentStates[^1]);
        Assert.Empty(_stage.Failures);
    }

    [Fact]
    public async Task The_state_exiting_while_it_waits_is_silent()
    {
        await _sm.ChangeStateAsync<StageState>();

        await _sm.ChangeStateAsync<StageElsewhere>();

        Assert.False(_stage.Finished);
        Assert.Empty(_stage.Failures);
    }

    [Fact]
    public async Task Dispose_while_it_waits_is_silent()
    {
        await _sm.ChangeStateAsync<StageState>();

        _sm.Dispose();

        Assert.Empty(_stage.Failures);
    }

    // The next transition exits the stage's state, so stateLifetime is cancelled by the time it fails; the failure must
    // still reach OnFailed.
    [Fact]
    public async Task A_next_transition_whose_enter_cancels_itself_is_a_failure()
    {
        _stage.Next = stage => stage.Go<StageSelfCancellingNext>();
        await _sm.ChangeStateAsync<StageState>();

        _stage.Signal();

        var failure = Assert.Single(_stage.Failures);
        Assert.IsType<OperationCanceledException>(failure);
    }

    [Fact]
    public async Task A_next_transition_superseded_by_a_newer_one_is_silent()
    {
        _stage.Next = stage => stage.Go<StageBlockingNext>();
        await _sm.ChangeStateAsync<StageState>();
        _stage.Signal();

        await _sm.ChangeStateAsync<StageElsewhere>();

        Assert.Empty(_stage.Failures);
    }

    // A transition failure the stage caught used to stay recorded, and the state's later exit reported it to OnFailed.
    [Fact]
    public async Task A_transition_failure_the_stage_handled_is_not_reported_later()
    {
        _stage.WaitAfterNext = true;
        _stage.Next = async stage =>
        {
            try { await stage.Go<StageSelfCancellingNext>(); }
            catch (OperationCanceledException) { }
        };
        await _sm.ChangeStateAsync<StageState>();

        _stage.Signal();
        await _sm.ChangeStateAsync<StageElsewhere>();

        Assert.True(_stage.Finished);
        Assert.Empty(_stage.Failures);
    }

    [Fact]
    public async Task An_exception_from_RunAsync_is_a_failure()
    {
        _stage.ThrowInRun = new InvalidOperationException("stage failed");

        await _sm.ChangeStateAsync<StageState>();

        Assert.Equal("stage failed", Assert.Single(_stage.Failures).Message);
    }
}
