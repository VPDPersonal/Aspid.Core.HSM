using System;
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

    // Started from inside an enter callback, a transition would await the one it is running in.
    [Fact]
    public async Task ChangeStateAsync_FromInsideAnEnterCallback_Throws()
    {
        var factory = new TestStateFactory();
        var redirecting = new RedirectingAsyncEnterState();
        factory.RegisterState(creator: () => redirecting);
        factory.RegisterState<SimpleTestState>();
        var stateMachine = new TestableStateMachine(factory);
        redirecting.Redirect = () => stateMachine.ChangeStateAsync<SimpleTestState>();

        await stateMachine.ChangeStateAsync<RedirectingAsyncEnterState>();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await redirecting.Inner!.Value);
        Assert.Same(redirecting, Assert.Single(stateMachine.CurrentStates));
    }
}

public class RedirectingAsyncEnterState : BaseTestState, IAsyncEnterController
{
    public Func<UniTask>? Redirect { get; set; }

    public UniTask? Inner { get; private set; }

    public UniTask OnEnterAsync(CancellationToken cancellationToken)
    {
        Inner = Redirect?.Invoke();
        return UniTask.CompletedTask;
    }
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
