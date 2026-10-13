using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

#region Helpers

public sealed class DisposeRootState : BaseTestState, IDisposableController
{
    public int DisposeCalled { get; private set; }

    public void Dispose() => DisposeCalled++;
}

/// <summary>Async enter that waits until cancelled.</summary>
public sealed class DisposeBlockingLeafState : BaseTestState, IChildState<DisposeRootState>,
    IAsyncEnterController, IExitController, IDisposableController
{
    public bool EnterCancelled { get; private set; }

    public int OnExitCalled { get; private set; }

    public int DisposeCalled { get; private set; }

    public async UniTask OnEnterAsync(CancellationToken cancellationToken)
    {
        try { await UniTask.Never(cancellationToken); }
        catch (OperationCanceledException) { EnterCancelled = true; throw; }
    }

    public void OnExit() => OnExitCalled++;

    public void Dispose() => DisposeCalled++;
}

#endregion

// Dispose used to leave a running async transition alive: it kept awaiting, and later rolled back states whose
// scopes Dispose had already destroyed.
public class DisposeDuringTransitionTests
{
    private readonly DisposeRootState _root = new();
    private readonly DisposeBlockingLeafState _leaf = new();
    private readonly TestStateFactory _factory = new();
    private readonly TestScope _rootScope = new();
    private readonly TestableStateMachine _sm;

    public DisposeDuringTransitionTests()
    {
        _factory.RegisterState(() => _root);
        _factory.RegisterState(() => _leaf);
        _factory.SetRootScope(_rootScope);
        _sm = new TestableStateMachine(_factory);
    }

    [Fact]
    public async Task Dispose_cancels_the_running_transition()
    {
        var task = _sm.ChangeStateAsync<DisposeBlockingLeafState>();
        Assert.True(_sm.IsTransitioning);

        _sm.Dispose();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);
        Assert.False(_sm.IsTransitioning);
        Assert.True(_leaf.EnterCancelled);
    }

    [Fact]
    public async Task Dispose_disposes_the_half_entered_state_without_rolling_it_back()
    {
        var task = _sm.ChangeStateAsync<DisposeBlockingLeafState>();
        var leafScope = Assert.IsType<TestScope>(_factory.GetScope<DisposeBlockingLeafState>());

        _sm.Dispose();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);

        // Like every state Dispose finds, the half-entered one is disposed, not exited.
        Assert.Equal(1, _leaf.DisposeCalled);
        Assert.Equal(1, _root.DisposeCalled);
        Assert.Equal(0, _leaf.OnExitCalled);
        Assert.Equal(0, _leaf.ExitCalled);
        Assert.Equal(0, _root.ExitCalled);
        Assert.True(leafScope.IsDisposed);
        Assert.Equal(1, _sm.DisposingCallCount);
        Assert.Equal(1, _sm.DisposedCallCount);
    }

    [Fact]
    public void A_second_Dispose_does_nothing()
    {
        _sm.ChangeState<DisposeRootState>();

        _sm.Dispose();
        _sm.Dispose();

        Assert.Equal(1, _root.DisposeCalled);
        Assert.Equal(1, _sm.DisposingCallCount);
    }
}
