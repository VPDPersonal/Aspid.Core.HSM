using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

#region Helpers

public abstract class LoggedState : IState
{
    public List<string>? Log { get; set; }

    public int EnterCalled { get; private set; }

    public int ExitCalled { get; private set; }

    public void Enter()
    {
        EnterCalled++;
        Log?.Add("enter " + GetType().Name);
    }

    public void Exit()
    {
        ExitCalled++;
        Log?.Add("exit " + GetType().Name);
    }
}

public sealed class RbRoot : LoggedState;

public sealed class RbFamily : LoggedState, IChildState<RbRoot>;

public sealed class RbOldLeaf : LoggedState, IChildState<RbRoot>;

public sealed class RbLeaf : LoggedState, IChildState<RbFamily>;

/// <summary>Extension whose detach throws on demand; bound to <see cref="RbFamily"/> or unbound.</summary>
public abstract class RbThrowingExtension : LoggedState, IExtensionState
{
    public bool ThrowOnDetached { get; set; }

    public abstract bool CanAttachTo(IState hostState);

    public void OnDetached(IState hostState)
    {
        if (ThrowOnDetached)
            throw new InvalidOperationException("detach failed");
    }
}

public sealed class RbFamilyExtension : RbThrowingExtension, IChildState<RbFamily>
{
    public override bool CanAttachTo(IState hostState) => true;
}

/// <summary>Unbound extension that cannot stay on <see cref="RbRoot"/>, so a rollback to the root detaches it.</summary>
public sealed class RbLeafOnlyExtension : RbThrowingExtension
{
    public override bool CanAttachTo(IState hostState) => hostState is not RbRoot;
}

/// <summary>Leaf whose async enter blocks until completed, cancelled or failed from the test.</summary>
public sealed class RbBlockingLeaf : LoggedState, IChildState<RbFamily>, IAsyncEnterController, IAsyncExitController
{
    private readonly UniTaskCompletionSource _enter = new();

    public bool ThrowOnExit { get; set; }

    public void FailEnter(Exception exception) => _enter.TrySetException(exception);

    public async UniTask OnEnterAsync(CancellationToken cancellationToken)
    {
        Log?.Add("enterAsync " + nameof(RbBlockingLeaf));
        using var registration = cancellationToken.Register(() => _enter.TrySetCanceled(cancellationToken));
        await _enter.Task;
    }

    public UniTask OnExitAsync(CancellationToken cancellationToken)
    {
        Log?.Add("exitAsync " + nameof(RbBlockingLeaf));
        if (ThrowOnExit)
            throw new InvalidOperationException("exit failed");

        return UniTask.CompletedTask;
    }
}

/// <summary>Leaf whose async exit blocks until cancelled.</summary>
public sealed class RbSlowExitLeaf : LoggedState, IChildState<RbFamily>, IAsyncExitController
{
    public async UniTask OnExitAsync(CancellationToken cancellationToken)
    {
        Log?.Add("exitAsync " + nameof(RbSlowExitLeaf));
        await UniTask.Never(cancellationToken);
    }
}

#endregion

// A cancelled or failed ChangeStateAsync used to leave the chain half-changed: some old states exited, the
// state being entered half-entered with its scope active, and nothing released.
public class AsyncRollbackTests
{
    private readonly List<string> _log = [];
    private readonly RbRoot _root = new();
    private readonly RbFamily _family = new();
    private readonly RbOldLeaf _oldLeaf = new();
    private readonly RbBlockingLeaf _blockingLeaf = new();
    private readonly RbSlowExitLeaf _slowExitLeaf = new();
    private readonly RbLeaf _leaf = new();
    private readonly RbFamilyExtension _familyExtension = new();
    private readonly RbLeafOnlyExtension _leafOnlyExtension = new();
    private readonly TestStateFactory _factory = new();
    private readonly TestScope _rootScope = new();
    private readonly TestableStateMachine _sm;

    public AsyncRollbackTests()
    {
        foreach (var state in new LoggedState[] { _root, _family, _oldLeaf, _blockingLeaf, _slowExitLeaf, _leaf })
            state.Log = _log;

        _factory.RegisterState(() => _root);
        _factory.RegisterState(() => _family);
        _factory.RegisterState(() => _oldLeaf);
        _factory.RegisterState(() => _blockingLeaf);
        _factory.RegisterState(() => _slowExitLeaf);
        _factory.RegisterState(() => _leaf);
        _factory.RegisterState(() => _familyExtension);
        _factory.RegisterState(() => _leafOnlyExtension);
        _factory.SetRootScope(_rootScope);

        _sm = new TestableStateMachine(_factory);
    }

    [Fact]
    public async Task Cancelled_enter_rolls_back_to_the_shared_ancestors()
    {
        _sm.ChangeState<RbOldLeaf>();
        _log.Clear();
        using var cts = new CancellationTokenSource();

        var task = _sm.ChangeStateAsync<RbBlockingLeaf>(cts.Token);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);
        Assert.Equal(new IState[] { _root }, _sm.CurrentStates.ToArray());
        Assert.Equal(
            new[]
            {
                "exit RbOldLeaf",
                "enter RbFamily",
                "enter RbBlockingLeaf", "enterAsync RbBlockingLeaf",
                // Rollback, from the tail: the half-entered leaf is exited too.
                "exitAsync RbBlockingLeaf", "exit RbBlockingLeaf",
                "exit RbFamily",
            },
            _log);
        Assert.Equal(1, _root.EnterCalled);
        Assert.Equal(0, _root.ExitCalled);
    }

    [Fact]
    public async Task Rolled_back_states_are_released_with_their_scopes()
    {
        _sm.ChangeState<RbOldLeaf>();
        using var cts = new CancellationTokenSource();
        var task = _sm.ChangeStateAsync<RbBlockingLeaf>(cts.Token);
        var familyScope = Assert.IsType<TestScope>(_factory.GetScope<RbFamily>());
        var leafScope = Assert.IsType<TestScope>(_factory.GetScope<RbBlockingLeaf>());

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);

        Assert.Contains(_family, _factory.ReleasedStates);
        Assert.Contains(_blockingLeaf, _factory.ReleasedStates);
        Assert.True(familyScope.IsDisposed);
        Assert.True(leafScope.IsDisposed);
        Assert.Null(_factory.GetScope<RbFamily>());
        Assert.Null(_factory.GetScope<RbBlockingLeaf>());
        Assert.NotNull(_factory.GetScope<RbRoot>());
    }

    [Fact]
    public async Task Failed_enter_rolls_back_and_rethrows_the_original_exception()
    {
        _sm.ChangeState<RbOldLeaf>();

        var task = _sm.ChangeStateAsync<RbBlockingLeaf>();
        _blockingLeaf.FailEnter(new InvalidOperationException("enter failed"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await task);
        Assert.Equal("enter failed", exception.Message);
        Assert.Equal(new IState[] { _root }, _sm.CurrentStates.ToArray());
        Assert.Equal(1, _blockingLeaf.ExitCalled);
        Assert.Equal(1, _family.ExitCalled);
    }

    [Fact]
    public async Task Cancelled_exit_is_completed_and_the_rest_is_rolled_back()
    {
        _sm.ChangeState<RbSlowExitLeaf>();
        _log.Clear();
        using var cts = new CancellationTokenSource();

        var task = _sm.ChangeStateAsync<RbOldLeaf>(cts.Token);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);
        Assert.Equal(new IState[] { _root }, _sm.CurrentStates.ToArray());
        // The interrupted exit is finished, not restarted; the old family not yet exited is rolled back.
        Assert.Equal(
            new[] { "exitAsync RbSlowExitLeaf", "exit RbSlowExitLeaf", "exit RbFamily" },
            _log);
        Assert.Contains(_slowExitLeaf, _factory.ReleasedStates);
        Assert.Equal(0, _oldLeaf.EnterCalled);
    }

    [Fact]
    public async Task Nothing_shared_falls_back_to_the_empty_state()
    {
        using var cts = new CancellationTokenSource();

        var task = _sm.ChangeStateAsync<RbBlockingLeaf>(cts.Token);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);
        Assert.IsType<EmptyState>(Assert.Single(_sm.CurrentStates));
        Assert.Equal(1, _root.ExitCalled);

        // The machine is usable afterwards.
        _sm.ChangeState<RbOldLeaf>();
        Assert.Equal(new IState[] { _root, _oldLeaf }, _sm.CurrentStates.ToArray());
    }

    [Fact]
    public async Task Superseding_change_starts_from_the_rolled_back_chain()
    {
        _sm.ChangeState<RbOldLeaf>();

        var first = _sm.ChangeStateAsync<RbBlockingLeaf>();
        var second = _sm.ChangeStateAsync<RbOldLeaf>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await first);
        await second;

        Assert.Equal(new IState[] { _root, _oldLeaf }, _sm.CurrentStates.ToArray());
        Assert.Equal(1, _root.EnterCalled);
        Assert.Equal(1, _family.ExitCalled);
        Assert.Equal(2, _oldLeaf.EnterCalled);
    }

    [Fact]
    public async Task Rollback_failure_is_reported_together_with_the_cause()
    {
        _sm.ChangeState<RbOldLeaf>();
        _blockingLeaf.ThrowOnExit = true;
        using var cts = new CancellationTokenSource();

        var task = _sm.ChangeStateAsync<RbBlockingLeaf>(cts.Token);
        cts.Cancel();

        var exception = await Assert.ThrowsAsync<AggregateException>(async () => await task);
        Assert.IsAssignableFrom<OperationCanceledException>(exception.InnerExceptions[0]);
        Assert.Equal("exit failed", exception.InnerExceptions[1].Message);

        // The failing state is still exited, removed and released; the rollback carries on above it.
        Assert.Equal(new IState[] { _root }, _sm.CurrentStates.ToArray());
        Assert.Equal(1, _blockingLeaf.ExitCalled);
        Assert.Contains(_blockingLeaf, _factory.ReleasedStates);
        Assert.Equal(1, _family.ExitCalled);
    }

    [Fact]
    public async Task Throwing_bound_extension_does_not_stop_its_parent_from_being_released()
    {
        _sm.ChangeState<RbLeaf>();
        _sm.AttachExtension<RbFamilyExtension>();
        _familyExtension.ThrowOnDetached = true;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _sm.ChangeStateAsync<RbOldLeaf>());

        // The detach threw before the family's own exit, yet the family is exited and released, and the
        // extension does not stay attached under the family's disposed scope.
        Assert.Equal("detach failed", exception.Message);
        Assert.Equal(1, _family.ExitCalled);
        Assert.Contains(_family, _factory.ReleasedStates);
        Assert.Contains(_familyExtension, _factory.ReleasedStates);
        Assert.Empty(_sm.ActiveExtensions);
        Assert.Equal(new IState[] { _root }, _sm.CurrentStates.ToArray());
    }

    [Fact]
    public async Task Rollback_closes_the_change()
    {
        _sm.ChangeState<RbOldLeaf>();
        using var cts = new CancellationTokenSource();

        var task = _sm.ChangeStateAsync<RbBlockingLeaf>(cts.Token);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);

        // OnChangingState/OnChangedState stay paired for subclasses that bracket a change with them.
        Assert.Equal(_sm.ChangingStateCallCount, _sm.ChangedStateCallCount);
    }

    [Fact]
    public async Task Throwing_detach_during_the_rollback_does_not_hide_the_cause()
    {
        _sm.ChangeState<RbOldLeaf>();
        _sm.AttachExtension<RbLeafOnlyExtension>();
        _leafOnlyExtension.ThrowOnDetached = true;
        using var cts = new CancellationTokenSource();

        var task = _sm.ChangeStateAsync<RbBlockingLeaf>(cts.Token);
        cts.Cancel();

        var exception = await Assert.ThrowsAsync<AggregateException>(async () => await task);
        Assert.IsAssignableFrom<OperationCanceledException>(exception.InnerExceptions[0]);
        Assert.Equal("detach failed", exception.InnerExceptions[1].Message);
        Assert.Empty(_sm.ActiveExtensions);
    }
}
