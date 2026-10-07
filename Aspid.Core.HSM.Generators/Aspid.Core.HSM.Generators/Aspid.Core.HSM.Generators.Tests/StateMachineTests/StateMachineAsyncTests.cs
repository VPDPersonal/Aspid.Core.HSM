using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

public class StateMachineAsyncTests
{
    [Fact]
    public async Task ChangeStateAsync_AwaitsAsyncEnterController()
    {
        var factory = new TestStateFactory();
        var asyncState = new AsyncEnterTestState();
        factory.RegisterState(creator: () => asyncState);
        var stateMachine = new TestableStateMachine(factory);

        var task = stateMachine.ChangeStateAsync<AsyncEnterTestState>();
        Assert.False(task.Status.IsCompleted());
        Assert.Equal(0, asyncState.AsyncEnterCompletedCount);

        asyncState.CompleteEnter();
        await task;

        Assert.Equal(1, asyncState.AsyncEnterCompletedCount);
        Assert.Contains(asyncState, stateMachine.CurrentStates);
    }

    [Fact]
    public async Task ChangeStateAsync_ReentrantCall_CancelsPrevious()
    {
        var factory = new TestStateFactory();
        var slowState = new AsyncEnterTestState();
        var fastState = new SimpleTestState();
        factory.RegisterState(creator: () => slowState);
        factory.RegisterState(creator: () => fastState);
        var stateMachine = new TestableStateMachine(factory);

        var firstTask = stateMachine.ChangeStateAsync<AsyncEnterTestState>();
        Assert.False(firstTask.Status.IsCompleted());

        var secondTask = stateMachine.ChangeStateAsync<SimpleTestState>();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await firstTask);

        await secondTask;

        Assert.Contains(fastState, stateMachine.CurrentStates);
        Assert.DoesNotContain(slowState, stateMachine.CurrentStates);
    }

    [Fact]
    public async Task ChangeState_Sync_DuringAsyncTransition_Throws()
    {
        var factory = new TestStateFactory();
        var asyncState = new AsyncEnterTestState();
        factory.RegisterState(creator: () => asyncState);
        factory.RegisterState<SimpleTestState>();
        var stateMachine = new TestableStateMachine(factory);

        var pending = stateMachine.ChangeStateAsync<AsyncEnterTestState>();

        Assert.Throws<InvalidOperationException>(() => stateMachine.ChangeState<SimpleTestState>());

        asyncState.CompleteEnter();
        await pending;
    }

    [Fact]
    public async Task ChangeStateAsync_FallsBackToSyncEnter_WhenStateOnlyImplementsSyncController()
    {
        var factory = new TestStateFactory();
        var syncCtrlState = new ControllableTestState();
        factory.RegisterState(creator: () => syncCtrlState);
        var stateMachine = new TestableStateMachine(factory);

        await stateMachine.ChangeStateAsync<ControllableTestState>();

        Assert.Equal(1, syncCtrlState.OnEnterCalled);
        Assert.Contains(syncCtrlState, stateMachine.CurrentStates);
    }

    // An async enter callback that awaits ChangeStateAsync used to deadlock: the new call waited for the
    // running transition to unwind, and that transition waited for the callback. The new call now supersedes it.
    [Fact]
    public async Task ChangeStateAsync_awaited_at_the_start_of_OnEnterAsync_redirects()
    {
        var factory = new TestStateFactory();
        var redirecting = new RedirectingAsyncEnterState();
        var target = new SimpleTestState();
        factory.RegisterState(creator: () => redirecting);
        factory.RegisterState(creator: () => target);
        var stateMachine = new TestableStateMachine(factory);
        redirecting.Redirect = () => stateMachine.ChangeStateAsync<SimpleTestState>();

        var first = stateMachine.ChangeStateAsync<RedirectingAsyncEnterState>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => WithTimeout(first));
        Assert.True(redirecting.RedirectCompleted);
        Assert.Equal(1, redirecting.ExitCalled);
        Assert.Equal(new IState[] { target }, stateMachine.CurrentStates.ToArray());
    }

    // Same as above, but the redirect comes after the callback's own await: the usual "loaded, now go on" case.
    [Fact]
    public async Task ChangeStateAsync_awaited_after_an_await_in_OnEnterAsync_redirects()
    {
        var factory = new TestStateFactory();
        var redirecting = new RedirectingAsyncEnterState { WaitBeforeRedirect = new UniTaskCompletionSource() };
        var target = new SimpleTestState();
        factory.RegisterState(creator: () => redirecting);
        factory.RegisterState(creator: () => target);
        var stateMachine = new TestableStateMachine(factory);
        redirecting.Redirect = () => stateMachine.ChangeStateAsync<SimpleTestState>();

        var first = stateMachine.ChangeStateAsync<RedirectingAsyncEnterState>();
        Assert.False(first.Status.IsCompleted());

        redirecting.WaitBeforeRedirect.TrySetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => WithTimeout(first));
        Assert.True(redirecting.RedirectCompleted);
        Assert.Equal(1, redirecting.ExitCalled);
        Assert.Equal(new IState[] { target }, stateMachine.CurrentStates.ToArray());
    }

    // Two calls made while the first transition is still running both wait for the same unwind. The one that
    // resumes second must supersede the transition the first one started, not run its core alongside it.
    [Fact]
    public async Task ChangeStateAsync_calls_waiting_for_the_same_unwind_run_one_at_a_time()
    {
        var factory = new TestStateFactory();
        var redirecting = new RedirectingAsyncEnterState();
        var slow = new AsyncEnterTestState();
        var target = new SimpleTestState();
        factory.RegisterState(creator: () => redirecting);
        factory.RegisterState(creator: () => slow);
        factory.RegisterState(creator: () => target);
        var stateMachine = new TestableStateMachine(factory);

        UniTask second = default, third = default;
        redirecting.Redirect = () =>
        {
            second = stateMachine.ChangeStateAsync<AsyncEnterTestState>();
            third = stateMachine.ChangeStateAsync<SimpleTestState>();
            return UniTask.CompletedTask;
        };

        var first = stateMachine.ChangeStateAsync<RedirectingAsyncEnterState>();

        // The first transition was cancelled after its last enter callback had finished, so its outcome
        // depends on where it noticed the cancellation. Only the second and the third outcomes are asserted.
        try { await WithTimeout(first); }
        catch (OperationCanceledException) { }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => WithTimeout(second));
        await WithTimeout(third);

        Assert.Equal(1, slow.EnterCalled);
        Assert.Equal(1, slow.ExitCalled);
        Assert.Equal(0, slow.AsyncEnterCompletedCount);
        Assert.Equal(new IState[] { target }, stateMachine.CurrentStates.ToArray());
    }

    [Fact]
    public async Task TransitionToAsync_consults_the_edge_guard_once()
    {
        var factory = new TestStateFactory();
        factory.RegisterState<ParentTestState>();
        factory.RegisterState<ChildTestState>();
        factory.RegisterState<SiblingChildTestState>();
        var stateMachine = new GuardedStateMachine(factory);
        var transition = new ChildToSiblingSegmentTransition();
        stateMachine.RegisterTransition(transition);
        stateMachine.ChangeState<ChildTestState>();
        stateMachine.ObservedEdges.Clear();

        await WithTimeout(stateMachine.TransitionToAsync<SiblingChildTestState>());

        Assert.Single(stateMachine.ObservedEdges);
        Assert.Equal(1, transition.AfterCount);
        Assert.IsType<SiblingChildTestState>(stateMachine.CurrentStates[^1]);
    }

    // Fails the test instead of hanging the run when a transition never completes.
    private static Task WithTimeout(UniTask task) =>
        task.AsTask().WaitAsync(TimeSpan.FromSeconds(5));
}

public class AsyncEnterTestState : BaseTestState, IAsyncEnterController
{
    private UniTaskCompletionSource _enterTcs = new();

    public int AsyncEnterCompletedCount { get; private set; }

    public void CompleteEnter() => _enterTcs.TrySetResult();

    public async UniTask OnEnterAsync(CancellationToken cancellationToken)
    {
        using var registration = cancellationToken.Register(() => _enterTcs.TrySetCanceled(cancellationToken));
        await _enterTcs.Task;
        AsyncEnterCompletedCount++;
    }
}

/// <summary>State whose async enter awaits <see cref="Redirect"/>, optionally after its own await.</summary>
public class RedirectingAsyncEnterState : BaseTestState, IAsyncEnterController
{
    public Func<UniTask>? Redirect { get; set; }

    public UniTaskCompletionSource? WaitBeforeRedirect { get; set; }

    public bool RedirectCompleted { get; private set; }

    public async UniTask OnEnterAsync(CancellationToken cancellationToken)
    {
        if (WaitBeforeRedirect is not null)
            await WaitBeforeRedirect.Task;

        if (Redirect is not null)
            await Redirect();

        RedirectCompleted = true;
    }
}
