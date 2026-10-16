using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

#region Helpers

/// <summary>Sync enter and exit controllers that run a callback, to throw or dispose the machine from inside a change.</summary>
public abstract class EpState : BaseTestState, IEnterController, IExitController
{
    public Action? OnEnterAction { get; set; }

    public Action? OnExitAction { get; set; }

    public int OnEnterCalled { get; private set; }

    public int OnExitCalled { get; private set; }

    public void OnEnter()
    {
        OnEnterCalled++;
        OnEnterAction?.Invoke();
    }

    public void OnExit()
    {
        OnExitCalled++;
        OnExitAction?.Invoke();
    }
}

public sealed class EpRoot : EpState;

public sealed class EpChild : EpState, IChildState<EpRoot>;

public sealed class EpGrandchild : EpState, IChildState<EpChild>;

public sealed class EpOther : EpState;

public abstract class EpDisposableState : BaseTestState, IDisposableController
{
    public bool ThrowOnDispose { get; set; }

    public int DisposeCalled { get; private set; }

    public void Dispose()
    {
        DisposeCalled++;
        if (ThrowOnDispose)
            throw new InvalidOperationException("dispose failed");
    }
}

public sealed class EpDisposableRoot : EpDisposableState;

public sealed class EpDisposableChild : EpDisposableState, IChildState<EpDisposableRoot>;

public abstract class EpExtensionBase : BaseTestState, IExtensionState
{
    public bool ThrowOnDetached { get; set; }

    public IState? DetachedHost { get; private set; }

    public bool CanAttachTo(IState hostState) => true;

    public void OnDetached(IState hostState)
    {
        DetachedHost = hostState;
        if (ThrowOnDetached)
            throw new InvalidOperationException("detach failed");
    }
}

/// <summary>Unbound extension that stays on any leaf.</summary>
public sealed class EpExtension : EpExtensionBase;

/// <summary>Extension bound to <see cref="EpRoot"/>: detached when the root exits.</summary>
public sealed class EpRootExtension : EpExtensionBase, IChildState<EpRoot>;

/// <summary>Root whose async exit waits until cancelled.</summary>
public sealed class EpAsyncExitRoot : BaseTestState, IAsyncExitController
{
    public UniTask OnExitAsync(CancellationToken cancellationToken) => UniTask.Never(cancellationToken);
}

/// <summary>Async enter that waits until cancelled; its sync exit runs a callback.</summary>
public sealed class EpBlockingLeaf : BaseTestState, IAsyncEnterController, IExitController
{
    public Action? OnExitAction { get; set; }

    public UniTask OnEnterAsync(CancellationToken cancellationToken) => UniTask.Never(cancellationToken);

    public void OnExit() => OnExitAction?.Invoke();
}

/// <summary>Async enter that registers a throwing callback on its token, then waits until cancelled.</summary>
public sealed class EpThrowingCallbackLeaf : BaseTestState, IAsyncEnterController
{
    public UniTask OnEnterAsync(CancellationToken cancellationToken)
    {
        cancellationToken.Register(() => throw new InvalidOperationException("callback failed"));
        return UniTask.Never(cancellationToken);
    }
}

public sealed class EpSelfParent : BaseTestState, IChildState<EpSelfParent>;

public sealed class EpCycleA : BaseTestState, IChildState<EpCycleB>;

public sealed class EpCycleB : BaseTestState, IChildState<EpCycleA>;

public sealed class EpCycleLeaf : BaseTestState, IChildState<EpCycleA>;

public sealed class EpThrowingReleaseFactory : TestStateFactory
{
    public Type? ThrowOnReleaseOf { get; set; }

    protected override void ReleaseInternal(IState state)
    {
        base.ReleaseInternal(state);
        if (state.GetType() == ThrowOnReleaseOf)
            throw new InvalidOperationException("release failed");
    }
}

#endregion

// Error paths an audit of 0.0.1-alpha.4 found: a throwing sync exit, Dispose that stops at the first failure or runs
// on a machine without a leaf, async loops that go on after Dispose, a cancelled token, a parent cycle and bad types.
public class ErrorPathTests
{
    private readonly TestStateFactory _factory;
    private readonly TestableStateMachine _sm;

    public ErrorPathTests() : this(new TestStateFactory()) { }

    private ErrorPathTests(TestStateFactory factory)
    {
        _factory = factory;
        _factory.SetRootScope(new TestScope());
        _sm = new TestableStateMachine(_factory);
    }

    private T Register<T>(T state) where T : IState
    {
        _factory.RegisterState(() => state);
        return state;
    }

    private TestScope ScopeOf<T>() where T : IState =>
        Assert.IsType<TestScope>(_factory.GetScope<T>());

    #region Sync exit
    [Fact]
    public void Sync_exit_that_throws_still_exits_releases_and_leaves_the_chain()
    {
        var root = Register(new EpRoot { OnExitAction = () => throw new InvalidOperationException("exit failed") });
        Register(new EpOther());
        _sm.ChangeState<EpRoot>();
        var rootScope = ScopeOf<EpRoot>();

        var exception = Assert.Throws<InvalidOperationException>(() => _sm.ChangeState<EpOther>());

        Assert.Equal("exit failed", exception.Message);
        Assert.Equal(1, root.ExitCalled);
        Assert.Contains(root, _factory.ReleasedStates);
        Assert.True(rootScope.IsDisposed);
        Assert.Equal(_sm.ExitingStateCallCount, _sm.ExitedStateCallCount);
        Assert.IsType<EmptyState>(Assert.Single(_sm.CurrentStates));
    }

    [Fact]
    public void Retry_after_a_throwing_sync_exit_does_not_exit_the_state_again()
    {
        var root = Register(new EpRoot { OnExitAction = () => throw new InvalidOperationException("exit failed") });
        var other = Register(new EpOther());
        _sm.ChangeState<EpRoot>();
        Assert.Throws<InvalidOperationException>(() => _sm.ChangeState<EpOther>());

        root.OnExitAction = null;
        _sm.ChangeState<EpOther>();

        Assert.Equal(1, root.OnExitCalled);
        Assert.Equal(1, root.ExitCalled);
        Assert.Same(other, Assert.Single(_sm.CurrentStates));
    }

    [Fact]
    public void Sync_exit_whose_bound_extension_throws_on_detach_still_exits_the_state()
    {
        var root = Register(new EpRoot());
        Register(new EpOther());
        var extension = Register(new EpRootExtension { ThrowOnDetached = true });
        _sm.ChangeState<EpRoot>();
        _sm.AttachExtension<EpRootExtension>();
        var rootScope = ScopeOf<EpRoot>();

        Assert.Throws<InvalidOperationException>(() => _sm.ChangeState<EpOther>());

        // The exit never started, so its controllers are not called; IState.Exit and Release still run.
        Assert.Equal(0, root.OnExitCalled);
        Assert.Equal(1, root.ExitCalled);
        Assert.Equal(1, extension.ExitCalled);
        Assert.True(rootScope.IsDisposed);
        Assert.Empty(_sm.ActiveExtensions);
        Assert.IsType<EmptyState>(Assert.Single(_sm.CurrentStates));
    }
    #endregion

    #region Disposed machine
    [Fact]
    public void Ticks_after_Dispose_reach_no_state_or_extension()
    {
        var state = Register(new FullUpdateTestState());
        var extension = Register(new TestExtensionState());
        _sm.ChangeState<FullUpdateTestState>();
        _sm.AttachExtension<TestExtensionState>();

        _sm.Dispose();
        _sm.CallUpdate(0.1f);
        _sm.CallLateUpdate(0.1f);
        _sm.CallFixedUpdate(0.1f);

        Assert.Equal(0, state.UpdateCallCount);
        Assert.Equal(0, state.LateUpdateCallCount);
        Assert.Equal(0, state.FixedUpdateCallCount);
        Assert.Equal(0, extension.UpdateCount);
    }

    [Fact]
    public void AttachExtension_after_Dispose_does_nothing()
    {
        Register(new SimpleTestState());
        var extension = Register(new TestExtensionState());
        _sm.ChangeState<SimpleTestState>();

        _sm.Dispose();
        _sm.AttachExtension<TestExtensionState>();

        Assert.Empty(_sm.ActiveExtensions);
        Assert.Equal(0, extension.EnterCalled);
        Assert.Null(_factory.GetScope<TestExtensionState>());
    }

    [Fact]
    public void IsDisposed_is_set_by_Dispose()
    {
        Assert.False(_sm.IsDisposed);

        _sm.Dispose();

        Assert.True(_sm.IsDisposed);
    }
    #endregion

    #region Dispose failures
    [Fact]
    public void Dispose_runs_every_step_when_a_state_and_an_extension_throw()
    {
        var root = Register(new EpDisposableRoot { ThrowOnDispose = true });
        var child = Register(new EpDisposableChild());
        var extension = Register(new EpExtension { ThrowOnDetached = true });
        _sm.ChangeState<EpDisposableChild>();
        _sm.AttachExtension<EpExtension>();
        var rootScope = ScopeOf<EpDisposableRoot>();
        var childScope = ScopeOf<EpDisposableChild>();

        var exception = Assert.Throws<AggregateException>(_sm.Dispose);

        Assert.Equal(["detach failed", "dispose failed"], exception.InnerExceptions.Select(inner => inner.Message));
        Assert.Equal(1, extension.ExitCalled);
        Assert.Empty(_sm.ActiveExtensions);
        Assert.Equal(1, root.DisposeCalled);
        Assert.Equal(1, child.DisposeCalled);
        Assert.True(rootScope.IsDisposed);
        Assert.True(childScope.IsDisposed);
        Assert.Equal(1, _sm.DisposedCallCount);
    }

    [Fact]
    public void Dispose_rethrows_a_single_failure_as_it_is_and_a_second_Dispose_does_nothing()
    {
        var root = Register(new EpDisposableRoot { ThrowOnDispose = true });
        _sm.ChangeState<EpDisposableRoot>();
        var rootScope = ScopeOf<EpDisposableRoot>();

        var exception = Assert.Throws<InvalidOperationException>(_sm.Dispose);
        _sm.Dispose();

        Assert.Equal("dispose failed", exception.Message);
        Assert.True(rootScope.IsDisposed);
        Assert.Equal(1, root.DisposeCalled);
        Assert.Equal(1, _sm.DisposingCallCount);
        Assert.Equal(1, _sm.DisposedCallCount);
    }

    [Fact]
    public async Task Dispose_completes_when_a_callback_on_the_transition_token_throws()
    {
        Register(new EpThrowingCallbackLeaf());
        var task = _sm.ChangeStateAsync<EpThrowingCallbackLeaf>();
        var leafScope = ScopeOf<EpThrowingCallbackLeaf>();

        var exception = Assert.Throws<InvalidOperationException>(_sm.Dispose);

        Assert.Equal("callback failed", exception.Message);
        Assert.True(leafScope.IsDisposed);
        Assert.Equal(1, _sm.DisposedCallCount);
        await Assert.ThrowsAsync<StateMachineDisposedException>(async () => await task);
    }

    [Fact]
    public void Release_disposes_the_scope_even_when_ReleaseInternal_throws()
    {
        var factory = new EpThrowingReleaseFactory { ThrowOnReleaseOf = typeof(SimpleTestState) };
        factory.RegisterState<SimpleTestState>();
        factory.SetRootScope(new TestScope());
        var state = factory.CreateState(typeof(SimpleTestState));
        var scope = Assert.IsType<TestScope>(factory.GetScope<SimpleTestState>());

        Assert.Throws<InvalidOperationException>(() => factory.Release(state));

        Assert.True(scope.IsDisposed);
        Assert.Null(factory.GetScope<SimpleTestState>());
    }

    [Fact]
    public async Task Dispose_while_a_root_state_awaits_its_async_exit_detaches_extensions_and_keeps_a_leaf()
    {
        Register(new EpAsyncExitRoot());
        Register(new EpOther());
        var extension = Register(new EpExtension());
        _sm.ChangeState<EpAsyncExitRoot>();
        _sm.AttachExtension<EpExtension>();
        var task = _sm.ChangeStateAsync<EpOther>();
        Assert.True(_sm.IsTransitioning);

        _sm.Dispose();

        await Assert.ThrowsAsync<StateMachineDisposedException>(async () => await task);
        Assert.Empty(_sm.ActiveExtensions);
        Assert.Equal(1, extension.ExitCalled);
        Assert.IsType<EmptyState>(extension.DetachedHost);
        Assert.IsType<EmptyState>(Assert.Single(_sm.CurrentStates));
        Assert.Equal(1, _sm.DisposedCallCount);
    }
    #endregion

    #region Async loops after Dispose
    [Fact]
    public async Task Async_exit_that_disposes_the_machine_stops_the_exit_loop()
    {
        var root = Register(new EpRoot());
        var child = Register(new EpChild());
        Register(new EpOther());
        _sm.ChangeState<EpChild>();
        child.OnExitAction = () => _sm.Dispose();

        // The whole call ends synchronously, and UniTask reports any OperationCanceledException it ends with as a plain
        // one, so the end cause is not checked here.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await _sm.ChangeStateAsync<EpOther>());

        Assert.Equal(1, child.ExitCalled);
        Assert.Equal(0, root.OnExitCalled);
        Assert.Equal(0, root.ExitCalled);
    }

    [Fact]
    public async Task Async_change_to_an_ancestor_whose_exit_disposes_the_machine_does_not_complete()
    {
        Register(new EpRoot());
        var child = Register(new EpChild());
        _sm.ChangeState<EpChild>();
        child.OnExitAction = () => _sm.Dispose();
        var changedBefore = _sm.ChangedStateCallCount;

        // Ends synchronously, as above.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await _sm.ChangeStateAsync<EpRoot>());

        Assert.Equal(changedBefore, _sm.ChangedStateCallCount);
    }

    [Fact]
    public async Task Rollback_exit_that_disposes_the_machine_stops_the_rollback()
    {
        var root = Register(new EpRoot());
        var child = Register(new EpChild());
        var grandchild = Register(new EpGrandchild());
        grandchild.OnEnterAction = () => throw new InvalidOperationException("enter failed");
        child.OnExitAction = () => _sm.Dispose();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _sm.ChangeStateAsync<EpGrandchild>());

        Assert.Equal("enter failed", exception.Message);
        Assert.Equal(1, grandchild.ExitCalled);
        Assert.Equal(1, child.ExitCalled);
        Assert.Equal(0, root.ExitCalled);
        Assert.Equal(0, _sm.ChangedStateCallCount);
    }
    #endregion

    #region Cancelled token
    [Fact]
    public async Task ChangeStateAsync_with_a_cancelled_token_leaves_the_chain_unchanged()
    {
        var simple = Register(new SimpleTestState());
        Register(new AnotherTestState());
        _sm.ChangeState<SimpleTestState>();
        var changingBefore = _sm.ChangingStateCallCount;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await _sm.ChangeStateAsync<AnotherTestState>(cts.Token));

        Assert.Equal(0, simple.ExitCalled);
        Assert.Same(simple, Assert.Single(_sm.CurrentStates));
        Assert.Equal(changingBefore, _sm.ChangingStateCallCount);
    }

    [Fact]
    public async Task TransitionToAsync_with_a_cancelled_token_runs_no_transition_hook()
    {
        var simple = Register(new SimpleTestState());
        Register(new AnotherTestState());
        var transition = new TestTransition();
        _sm.RegisterTransition(transition);
        _sm.ChangeState<SimpleTestState>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await _sm.TransitionToAsync<AnotherTestState>(cts.Token));

        Assert.Equal(0, transition.BeforeCount);
        Assert.Equal(0, simple.ExitCalled);
        Assert.Same(simple, Assert.Single(_sm.CurrentStates));
    }

    [Fact]
    public async Task TransitionViaAsync_with_a_cancelled_token_runs_no_transition_hook()
    {
        var simple = Register(new SimpleTestState());
        Register(new AnotherTestState());
        var transition = new TestTransition();
        _sm.RegisterTransition(transition);
        _sm.ChangeState<SimpleTestState>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await _sm.TransitionViaAsync<TestTransition>(cts.Token));

        Assert.Equal(0, transition.BeforeCount);
        Assert.Equal(0, simple.ExitCalled);
        Assert.Same(simple, Assert.Single(_sm.CurrentStates));
    }

    [Fact]
    public async Task Token_cancelled_while_waiting_for_a_superseded_transition_stops_before_the_change()
    {
        using var cts = new CancellationTokenSource();
        Register(new EpBlockingLeaf { OnExitAction = cts.Cancel });
        Register(new AnotherTestState());
        var first = _sm.ChangeStateAsync<EpBlockingLeaf>();

        // The superseded transition's rollback exits the leaf, which cancels the second call's token.
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await _sm.ChangeStateAsync<AnotherTestState>(cts.Token));

        await Assert.ThrowsAsync<TransitionSupersededException>(async () => await first);
        Assert.Equal(1, _sm.ChangingStateCallCount);
        Assert.Equal(1, _sm.ChangedStateCallCount);
        Assert.IsType<EmptyState>(Assert.Single(_sm.CurrentStates));
    }
    #endregion

    #region Parent cycle
    // Before cycle detection the chain walk never ended: fail instead of hanging the run.
    private static T RunWithTimeout<T>(Func<T> action)
    {
        var task = Task.Run(action);
        Assert.True(task.Wait(TimeSpan.FromSeconds(5)), "The call did not return.");
        return task.Result;
    }

    [Fact]
    public void BuildTypeChain_throws_on_a_state_that_is_its_own_parent()
    {
        var exception = RunWithTimeout(() => Assert.Throws<InvalidOperationException>(
            () => _factory.BuildTypeChain(typeof(EpSelfParent), new List<Type>())));

        Assert.Contains("EpSelfParent -> EpSelfParent", exception.Message);
    }

    [Fact]
    public void BuildTypeChain_throws_on_two_states_that_are_each_others_parent()
    {
        var exception = RunWithTimeout(() => Assert.Throws<InvalidOperationException>(
            () => _factory.BuildTypeChain(typeof(EpCycleLeaf), new List<Type>())));

        Assert.Contains("EpCycleA -> EpCycleB -> EpCycleA", exception.Message);
    }

    [Fact]
    public void ChangeState_into_a_parent_cycle_throws_and_keeps_the_chain()
    {
        var simple = Register(new SimpleTestState());
        _sm.ChangeState<SimpleTestState>();

        RunWithTimeout(() => Assert.Throws<InvalidOperationException>(() => _sm.ChangeState<EpCycleLeaf>()));

        Assert.Equal(0, simple.ExitCalled);
        Assert.Same(simple, Assert.Single(_sm.CurrentStates));
    }
    #endregion

    #region Argument checks
    [Fact]
    public void Sync_changes_reject_null_and_non_state_types_and_keep_the_chain()
    {
        var simple = Register(new SimpleTestState());
        _sm.ChangeState<SimpleTestState>();

        Assert.Throws<ArgumentNullException>(() => _sm.ChangeState(null!));
        Assert.Throws<ArgumentException>(() => _sm.ChangeState(typeof(string)));
        Assert.Throws<ArgumentNullException>(() => _sm.TransitionTo(null!));
        Assert.Throws<ArgumentException>(() => _sm.TransitionTo(typeof(string)));
        Assert.Throws<ArgumentNullException>(() => _sm.TransitionVia(null!));
        Assert.Throws<ArgumentException>(() => _sm.TransitionVia(typeof(string)));

        Assert.Equal(0, simple.ExitCalled);
        Assert.Same(simple, Assert.Single(_sm.CurrentStates));
    }

    [Fact]
    public async Task Async_changes_reject_null_and_non_state_types_and_keep_the_chain()
    {
        var simple = Register(new SimpleTestState());
        _sm.ChangeState<SimpleTestState>();

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await _sm.ChangeStateAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(async () => await _sm.ChangeStateAsync(typeof(string)));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await _sm.TransitionToAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(async () => await _sm.TransitionToAsync(typeof(string)));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await _sm.TransitionViaAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(async () => await _sm.TransitionViaAsync(typeof(string)));

        Assert.Equal(0, simple.ExitCalled);
        Assert.Same(simple, Assert.Single(_sm.CurrentStates));
    }
    #endregion
}
