using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

#region Helpers

/// <summary>Enter is async only, as in a [ControllerGroup] that holds one async enter controller.</summary>
public sealed class AsyncOnlyEnterState : BaseTestState, IAsyncEnterController
{
    public int OnEnterAsyncCalled { get; private set; }

    public UniTask OnEnterAsync(CancellationToken cancellationToken)
    {
        OnEnterAsyncCalled++;
        return UniTask.CompletedTask;
    }
}

public sealed class AsyncOnlyEnterChildState : BaseTestState, IChildState<ParentTestState>, IAsyncEnterController
{
    public UniTask OnEnterAsync(CancellationToken cancellationToken) => UniTask.CompletedTask;
}

public sealed class AsyncOnlyExitState : BaseTestState, IAsyncExitController
{
    public int OnExitAsyncCalled { get; private set; }

    public UniTask OnExitAsync(CancellationToken cancellationToken)
    {
        OnExitAsyncCalled++;
        return UniTask.CompletedTask;
    }
}

public sealed class SyncAndAsyncEnterState : BaseTestState, IEnterController, IAsyncEnterController
{
    public int OnEnterCalled { get; private set; }

    public void OnEnter() => OnEnterCalled++;

    public UniTask OnEnterAsync(CancellationToken cancellationToken) => UniTask.CompletedTask;
}

public sealed class SimpleToAsyncOnlyTransition : ITransition<SimpleTestState, AsyncOnlyEnterState>
{
    public int BeforeCount { get; private set; }

    public int AfterCount { get; private set; }

    public void OnBeforeTransition() => BeforeCount++;

    public void OnAfterTransition() => AfterCount++;
}

#endregion

// A synchronous change used to pass through a state whose enter or exit is async only, without calling a single
// one of its controllers. A [ControllerGroup] becomes such a state as soon as one of its controllers is async.
public class SyncChangeIntoAsyncStateTests
{
    private readonly SimpleTestState _simple = new();
    private readonly AsyncOnlyEnterState _asyncEnter = new();
    private readonly AsyncOnlyExitState _asyncExit = new();
    private readonly TestStateFactory _factory = new();
    private readonly TestableStateMachine _sm;

    public SyncChangeIntoAsyncStateTests()
    {
        _factory.RegisterState(() => _simple);
        _factory.RegisterState(() => _asyncEnter);
        _factory.RegisterState(() => _asyncExit);
        _factory.RegisterState<ParentTestState>();
        _factory.RegisterState<AsyncOnlyEnterChildState>();
        _factory.RegisterState<SyncAndAsyncEnterState>();
        _sm = new TestableStateMachine(_factory);
    }

    [Fact]
    public void ChangeState_into_an_async_only_enter_throws_and_changes_nothing()
    {
        _sm.ChangeState<SimpleTestState>();

        var exception = Assert.Throws<InvalidOperationException>(() => _sm.ChangeState<AsyncOnlyEnterState>());

        Assert.Contains(nameof(AsyncOnlyEnterState), exception.Message);
        Assert.Contains("ChangeStateAsync", exception.Message);
        Assert.Same(_simple, Assert.Single(_sm.CurrentStates));
        Assert.Equal(0, _simple.ExitCalled);
        Assert.DoesNotContain(_factory.CreatedWithScope, entry => entry.Type == typeof(AsyncOnlyEnterState));
    }

    [Fact]
    public void The_whole_path_is_checked_before_the_first_exit()
    {
        _sm.ChangeState<SimpleTestState>();

        // The async-only state sits below a synchronous parent; nothing may be exited or entered on the way.
        Assert.Throws<InvalidOperationException>(() => _sm.ChangeState<AsyncOnlyEnterChildState>());

        Assert.Same(_simple, Assert.Single(_sm.CurrentStates));
        Assert.Equal(0, _simple.ExitCalled);
        Assert.Empty(_factory.CreatedWithScope.Where(entry => entry.Type == typeof(ParentTestState)));
    }

    [Fact]
    public void ChangeState_out_of_an_async_only_exit_throws()
    {
        _sm.ChangeStateAsync<AsyncOnlyExitState>().GetAwaiter().GetResult();

        var exception = Assert.Throws<InvalidOperationException>(() => _sm.ChangeState<SimpleTestState>());

        Assert.Contains(nameof(AsyncOnlyExitState), exception.Message);
        Assert.Same(_asyncExit, Assert.Single(_sm.CurrentStates));
        Assert.Equal(0, _asyncExit.OnExitAsyncCalled);
        Assert.Equal(0, _asyncExit.ExitCalled);
    }

    [Fact]
    public void TransitionTo_is_checked_the_same_way()
    {
        _sm.ChangeState<SimpleTestState>();

        Assert.Throws<InvalidOperationException>(() => _sm.TransitionTo<AsyncOnlyEnterState>());

        Assert.Same(_simple, Assert.Single(_sm.CurrentStates));
    }

    // The check used to run inside the change, after OnBeforeTransition: the hook ran and its OnAfterTransition never did.
    [Fact]
    public void TransitionTo_throws_before_the_transition_hooks()
    {
        var transition = new SimpleToAsyncOnlyTransition();
        _sm.RegisterTransition(transition);
        _sm.ChangeState<SimpleTestState>();
        var changing = _sm.ChangingStateCallCount;

        Assert.Throws<InvalidOperationException>(() => _sm.TransitionTo<AsyncOnlyEnterState>());

        Assert.Equal(0, transition.BeforeCount);
        Assert.Equal(0, transition.AfterCount);
        Assert.Equal(changing, _sm.ChangingStateCallCount);
    }

    [Fact]
    public void TransitionVia_throws_before_the_transition_hooks()
    {
        var transition = new SimpleToAsyncOnlyTransition();
        _sm.RegisterTransition(transition);
        _sm.ChangeState<SimpleTestState>();

        Assert.Throws<InvalidOperationException>(() => _sm.TransitionVia<SimpleToAsyncOnlyTransition>());

        Assert.Equal(0, transition.BeforeCount);
        Assert.Same(_simple, Assert.Single(_sm.CurrentStates));
    }

    [Fact]
    public void A_state_with_both_enters_is_still_entered_synchronously()
    {
        _sm.ChangeState<SyncAndAsyncEnterState>();

        var state = Assert.IsType<SyncAndAsyncEnterState>(Assert.Single(_sm.CurrentStates));
        Assert.Equal(1, state.OnEnterCalled);
    }

    [Fact]
    public async Task ChangeStateAsync_runs_the_async_only_states()
    {
        await _sm.ChangeStateAsync<AsyncOnlyEnterState>();
        Assert.Equal(1, _asyncEnter.OnEnterAsyncCalled);

        await _sm.ChangeStateAsync<AsyncOnlyExitState>();
        await _sm.ChangeStateAsync<SimpleTestState>();

        Assert.Equal(1, _asyncExit.OnExitAsyncCalled);
        Assert.Same(_simple, Assert.Single(_sm.CurrentStates));
    }
}
