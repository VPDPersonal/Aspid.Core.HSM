using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

#region Helpers

public abstract class EnteredLoggingState : IState, IEnteredController, IExitController
{
    public List<string>? Log { get; set; }

    public IStateMachine? Machine { get; set; }

    public Action? OnEnteredAction { get; set; }

    public int EnteredCalled { get; private set; }

    public CancellationToken Lifetime { get; private set; }

    public bool? LifetimeCancelledInOnExit { get; private set; }

    public bool? TransitioningInOnEntered { get; private set; }

    public void Enter() => Log?.Add("enter " + GetType().Name);

    public void OnEntered(CancellationToken stateLifetime)
    {
        EnteredCalled++;
        Lifetime = stateLifetime;
        TransitioningInOnEntered = Machine?.IsTransitioning;
        Log?.Add("entered " + GetType().Name);
        OnEnteredAction?.Invoke();
    }

    public void OnExit()
    {
        LifetimeCancelledInOnExit = Lifetime.IsCancellationRequested;
        Log?.Add("exit " + GetType().Name);
    }
}

public sealed class EnRoot : EnteredLoggingState;

public sealed class EnLeaf : EnteredLoggingState, IChildState<EnRoot>;

public sealed class EnSiblingLeaf : EnteredLoggingState, IChildState<EnRoot>;

public sealed class EnOtherRoot : EnteredLoggingState;

/// <summary>Async enter that blocks until completed or cancelled; it also implements the sync enter.</summary>
public sealed class EnAsyncLeaf : EnteredLoggingState, IChildState<EnRoot>, IAsyncEnterController
{
    private readonly UniTaskCompletionSource _enter = new();

    public void CompleteEnter() => _enter.TrySetResult();

    public async UniTask OnEnterAsync(CancellationToken cancellationToken)
    {
        using var registration = cancellationToken.Register(() => _enter.TrySetCanceled(cancellationToken));
        await _enter.Task;
    }
}

/// <summary>Redirects the machine from its sync Enter: the change is queued until this one completes.</summary>
public sealed class EnRedirectingLeaf : EnteredLoggingState, IChildState<EnRoot>, IEnterController
{
    public void OnEnter() => Machine!.ChangeState<EnSiblingLeaf>();
}

/// <summary>Queues a change from its sync enter into a state whose constructor throws.</summary>
public sealed class EnFailingRedirectLeaf : EnteredLoggingState, IChildState<EnRoot>, IEnterController
{
    public void OnEnter() => Machine!.ChangeState<EnThrowingLeaf>();
}

/// <summary>Queues a change from its sync enter, then disposes the machine.</summary>
public sealed class EnDisposingLeaf : EnteredLoggingState, IChildState<EnRoot>, IEnterController
{
    public void OnEnter()
    {
        Machine!.ChangeState<EnSiblingLeaf>();
        ((IDisposable)Machine).Dispose();
    }
}

/// <summary>Disposes the machine from its sync enter, before its child is created.</summary>
public sealed class EnDisposingRoot : EnteredLoggingState, IEnterController
{
    public void OnEnter() => ((IDisposable)Machine!).Dispose();
}

public sealed class EnUnderDisposingRoot : EnteredLoggingState, IChildState<EnDisposingRoot>;

public sealed class EnThrowingLeaf : IState, IChildState<EnRoot>
{
    public EnThrowingLeaf() => throw new InvalidOperationException("constructor failed");
}

#endregion

// A stage that waits for an event after its enter had no way to know the transition that entered it was over:
// it polled IsTransitioning, or risked a ChangeState that threw or an async one that rolled the running change back.
public class EnteredControllerTests
{
    private readonly List<string> _log = [];
    private readonly EnRoot _root = new();
    private readonly EnLeaf _leaf = new();
    private readonly EnSiblingLeaf _sibling = new();
    private readonly EnOtherRoot _other = new();
    private readonly EnAsyncLeaf _asyncLeaf = new();
    private readonly EnRedirectingLeaf _redirecting = new();
    private readonly EnFailingRedirectLeaf _failingRedirect = new();
    private readonly EnDisposingLeaf _disposingLeaf = new();
    private readonly EnDisposingRoot _disposingRoot = new();
    private readonly TestStateFactory _factory = new();
    private readonly TestableStateMachine _sm;

    public EnteredControllerTests()
    {
        _sm = new TestableStateMachine(_factory);
        foreach (var state in new EnteredLoggingState[] { _root, _leaf, _sibling, _other, _asyncLeaf, _redirecting, _failingRedirect, _disposingLeaf, _disposingRoot })
        {
            state.Log = _log;
            state.Machine = _sm;
        }

        _factory.RegisterState(() => _root);
        _factory.RegisterState(() => _leaf);
        _factory.RegisterState(() => _sibling);
        _factory.RegisterState(() => _other);
        _factory.RegisterState(() => _asyncLeaf);
        _factory.RegisterState(() => _redirecting);
        _factory.RegisterState(() => _failingRedirect);
        _factory.RegisterState(() => _disposingLeaf);
        _factory.RegisterState(() => _disposingRoot);
        _factory.RegisterState(() => new EnUnderDisposingRoot());
        _factory.RegisterState(() => new EnThrowingLeaf());
    }

    [Fact]
    public void Runs_once_per_state_after_the_whole_chain_is_entered_from_root_to_leaf()
    {
        _sm.ChangeState<EnLeaf>();

        Assert.Equal(new[] { "enter EnRoot", "enter EnLeaf", "entered EnRoot", "entered EnLeaf" }, _log);
        Assert.Equal(1, _root.EnteredCalled);
        Assert.Equal(1, _leaf.EnteredCalled);
        Assert.False(_leaf.TransitioningInOnEntered);
    }

    [Fact]
    public void A_shared_ancestor_is_not_notified_again()
    {
        _sm.ChangeState<EnLeaf>();

        _sm.ChangeState<EnSiblingLeaf>();

        Assert.Equal(1, _root.EnteredCalled);
        Assert.Equal(1, _sibling.EnteredCalled);
    }

    [Fact]
    public async Task Async_change_notifies_once_the_transition_has_completed()
    {
        var task = _sm.ChangeStateAsync<EnAsyncLeaf>();
        Assert.Equal(0, _root.EnteredCalled);
        Assert.Equal(0, _asyncLeaf.EnteredCalled);

        _asyncLeaf.CompleteEnter();
        await task;

        Assert.Equal(1, _root.EnteredCalled);
        Assert.Equal(1, _asyncLeaf.EnteredCalled);
        Assert.False(_asyncLeaf.TransitioningInOnEntered);
    }

    [Fact]
    public async Task A_rolled_back_transition_notifies_nobody()
    {
        using var cts = new CancellationTokenSource();
        var task = _sm.ChangeStateAsync<EnAsyncLeaf>(cts.Token);

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);

        Assert.Equal(0, _root.EnteredCalled);
        Assert.Equal(0, _asyncLeaf.EnteredCalled);

        // Nothing is left over for the next transition to notify.
        _sm.ChangeState<EnOtherRoot>();
        Assert.Equal(0, _root.EnteredCalled);
        Assert.Equal(1, _other.EnteredCalled);
    }

    [Fact]
    public void A_failed_sync_change_notifies_nobody()
    {
        Assert.Throws<InvalidOperationException>(() => _sm.ChangeState<EnThrowingLeaf>());

        // The root stays entered, but the change that entered it failed.
        Assert.Same(_root, Assert.Single(_sm.CurrentStates));
        Assert.Equal(0, _root.EnteredCalled);
    }

    // The first change succeeded and queued the root; the queued one failed. The root's entry used to stay behind,
    // and the next, unrelated change notified it.
    [Fact]
    public void A_failed_queued_change_drops_the_whole_operations_notifications()
    {
        Assert.Throws<InvalidOperationException>(() => _sm.ChangeState<EnFailingRedirectLeaf>());
        Assert.Same(_root, Assert.Single(_sm.CurrentStates));

        _sm.ChangeState<EnSiblingLeaf>();

        Assert.Equal(0, _root.EnteredCalled);
        Assert.Equal(0, _failingRedirect.EnteredCalled);
        Assert.Equal(1, _sibling.EnteredCalled);
    }

    // A callback registered on the lifetime that throws must not stop the exit or Dispose that cancels it.
    [Fact]
    public void A_throwing_lifetime_callback_does_not_break_the_exit()
    {
        var published = new List<Exception>();
        void OnUnobserved(Exception exception) => published.Add(exception);
        _leaf.OnEnteredAction = () => _leaf.Lifetime.Register(() => throw new InvalidOperationException("callback failed"));
        _sm.ChangeState<EnLeaf>();

        UniTaskScheduler.UnobservedTaskException += OnUnobserved;
        try
        {
            _sm.ChangeState<EnSiblingLeaf>();
        }
        finally
        {
            UniTaskScheduler.UnobservedTaskException -= OnUnobserved;
        }

        Assert.Equal(new IState[] { _root, _sibling }, _sm.CurrentStates.ToArray());
        Assert.Equal(1, _leaf.LifetimeCancelledInOnExit == true ? 1 : 0);
        Assert.Contains(published, exception => exception.ToString().Contains("callback failed"));
    }

    [Fact]
    public void A_throwing_lifetime_callback_does_not_break_Dispose()
    {
        var published = new List<Exception>();
        void OnUnobserved(Exception exception) => published.Add(exception);
        _root.OnEnteredAction = () => _root.Lifetime.Register(() => throw new InvalidOperationException("callback failed"));
        _sm.ChangeState<EnLeaf>();

        UniTaskScheduler.UnobservedTaskException += OnUnobserved;
        try
        {
            _sm.Dispose();
        }
        finally
        {
            UniTaskScheduler.UnobservedTaskException -= OnUnobserved;
        }

        Assert.Equal(1, _sm.DisposedCallCount);
        Assert.True(_leaf.Lifetime.IsCancellationRequested);
        Assert.Contains(published, exception => exception.ToString().Contains("callback failed"));
    }

    // A lifetime callback that changes state used to modify the lifetimes mid-iteration and abort Dispose.
    [Fact]
    public void A_lifetime_callback_that_changes_state_does_not_break_Dispose()
    {
        _root.OnEnteredAction = () => _root.Lifetime.Register(() => _sm.ChangeState<EnOtherRoot>());
        _leaf.OnEnteredAction = () => _leaf.Lifetime.Register(() => _sm.ChangeState<EnOtherRoot>());
        _sm.ChangeState<EnLeaf>();

        _sm.Dispose();

        Assert.Equal(1, _sm.DisposedCallCount);
        Assert.Equal(new IState[] { _root, _leaf }, _sm.CurrentStates.ToArray());
        Assert.DoesNotContain("enter EnOtherRoot", _log);
    }

    // A request queued before Dispose used to run after it: a new scope and lifetime nobody released, an OnEntered on
    // a disposed machine.
    [Fact]
    public void A_change_queued_before_Dispose_is_dropped()
    {
        _sm.ChangeState<EnDisposingLeaf>();

        Assert.Equal(new IState[] { _root, _disposingLeaf }, _sm.CurrentStates.ToArray());
        Assert.DoesNotContain("enter EnSiblingLeaf", _log);
        Assert.Equal(0, _root.EnteredCalled);
        Assert.Equal(0, _disposingLeaf.EnteredCalled);
    }

    [Fact]
    public void Dispose_from_an_enter_stops_the_change_before_the_next_state()
    {
        _sm.ChangeState<EnUnderDisposingRoot>();

        Assert.Same(_disposingRoot, Assert.Single(_sm.CurrentStates));
        Assert.DoesNotContain(_factory.CreatedWithScope, entry => entry.Type == typeof(EnUnderDisposingRoot));
        Assert.Equal(0, _disposingRoot.EnteredCalled);
    }

    [Fact]
    public void A_change_deferred_from_a_tick_that_then_disposes_is_dropped()
    {
        var ticker = new TickingRootState(() =>
        {
            _sm.ChangeState<EnOtherRoot>();
            _sm.Dispose();
        });
        _factory.RegisterState(() => ticker);
        _sm.ChangeState<TickingRootState>();

        _sm.CallUpdate(0.016f);

        Assert.Same(ticker, Assert.Single(_sm.CurrentStates));
        Assert.DoesNotContain("enter EnOtherRoot", _log);
    }

    [Fact]
    public async Task State_changes_after_Dispose_are_ignored()
    {
        _sm.ChangeState<EnLeaf>();
        _sm.Dispose();

        _sm.ChangeState<EnOtherRoot>();
        _sm.TransitionTo<EnOtherRoot>();
        await _sm.ChangeStateAsync<EnOtherRoot>();
        await _sm.TransitionToAsync<EnOtherRoot>();

        Assert.Equal(new IState[] { _root, _leaf }, _sm.CurrentStates.ToArray());
        Assert.DoesNotContain("enter EnOtherRoot", _log);
    }

    [Fact]
    public void The_lifetime_is_cancelled_before_the_exit_controllers_run()
    {
        _sm.ChangeState<EnLeaf>();
        Assert.False(_leaf.Lifetime.IsCancellationRequested);

        _sm.ChangeState<EnSiblingLeaf>();

        Assert.True(_leaf.LifetimeCancelledInOnExit);
        Assert.True(_leaf.Lifetime.IsCancellationRequested);
        Assert.False(_root.Lifetime.IsCancellationRequested);
    }

    [Fact]
    public void The_lifetime_is_cancelled_on_Dispose()
    {
        _sm.ChangeState<EnLeaf>();

        _sm.Dispose();

        Assert.True(_root.Lifetime.IsCancellationRequested);
        Assert.True(_leaf.Lifetime.IsCancellationRequested);
    }

    [Fact]
    public void ChangeState_from_OnEntered_is_a_new_transition()
    {
        _leaf.OnEnteredAction = () => _sm.ChangeState<EnSiblingLeaf>();

        _sm.ChangeState<EnLeaf>();

        Assert.Equal(new IState[] { _root, _sibling }, _sm.CurrentStates.ToArray());
        Assert.Equal(1, _leaf.EnteredCalled);
        Assert.Equal(1, _sibling.EnteredCalled);
        Assert.True(_leaf.Lifetime.IsCancellationRequested);
    }

    [Fact]
    public async Task ChangeStateAsync_from_OnEntered_is_a_new_transition()
    {
        var follow = default(UniTask);
        _leaf.OnEnteredAction = () => follow = _sm.ChangeStateAsync<EnAsyncLeaf>();

        _sm.ChangeState<EnLeaf>();
        Assert.True(_sm.IsTransitioning);
        _asyncLeaf.CompleteEnter();
        await follow;

        Assert.Equal(new IState[] { _root, _asyncLeaf }, _sm.CurrentStates.ToArray());
        Assert.Equal(1, _asyncLeaf.EnteredCalled);
    }

    [Fact]
    public void A_state_a_queued_redirect_exited_again_is_not_notified()
    {
        _sm.ChangeState<EnRedirectingLeaf>();

        Assert.Equal(new IState[] { _root, _sibling }, _sm.CurrentStates.ToArray());
        Assert.Equal(0, _redirecting.EnteredCalled);
        Assert.Equal(1, _sibling.EnteredCalled);
        Assert.Equal(1, _root.EnteredCalled);
    }

    [Fact]
    public void A_throwing_OnEntered_reaches_the_caller_after_every_state_is_notified()
    {
        _root.OnEnteredAction = () => throw new InvalidOperationException("entered failed");

        var exception = Assert.Throws<InvalidOperationException>(() => _sm.ChangeState<EnLeaf>());

        Assert.Equal("entered failed", exception.Message);
        Assert.Equal(1, _leaf.EnteredCalled);
        Assert.Equal(new IState[] { _root, _leaf }, _sm.CurrentStates.ToArray());
    }

    [Fact]
    public void A_change_deferred_from_a_tick_notifies_too()
    {
        var ticker = new TickingRootState(() => _sm.ChangeState<EnOtherRoot>());
        _factory.RegisterState(() => ticker);
        _sm.ChangeState<TickingRootState>();

        _sm.CallUpdate(0.016f);

        Assert.Same(_other, Assert.Single(_sm.CurrentStates));
        Assert.Equal(1, _other.EnteredCalled);
    }

    private sealed class TickingRootState(Action onTick) : IState, IUpdateController
    {
        public void Update(float deltaTime) => onTick();
    }
}
