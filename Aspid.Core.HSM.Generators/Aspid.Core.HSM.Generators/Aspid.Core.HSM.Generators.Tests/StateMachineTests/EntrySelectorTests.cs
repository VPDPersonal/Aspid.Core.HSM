using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

#region Helpers

public abstract class SelLogged : IState
{
    public List<string>? Log { get; set; }

    public int EnterCalled { get; private set; }

    public void Enter()
    {
        EnterCalled++;
        Log?.Add("enter " + GetType().Name);
    }

    public void Exit() => Log?.Add("exit " + GetType().Name);
}

public sealed class SelRoot : SelLogged;

/// <summary>Composite whose entry child is chosen by <see cref="Entry"/>.</summary>
/// <summary>Marker a host can be named by in [ExtensionFor], instead of its class.</summary>
public interface ISelStage : IState;

public sealed class SelComposite : SelLogged, IChildState<SelRoot>, IEntrySelector, IEnteredController, ISelStage
{
    public Func<Type> Entry { get; set; } = () => typeof(SelLeafA);

    public int SelectCalled { get; private set; }

    public int EnteredCalled { get; private set; }

    public Type SelectEntry()
    {
        SelectCalled++;
        Log?.Add("select " + nameof(SelComposite));
        return Entry();
    }

    public void OnEntered(CancellationToken stateLifetime) => EnteredCalled++;
}

public sealed class SelLeafA : SelLogged, IChildState<SelComposite>, IEnteredController
{
    public int EnteredCalled { get; private set; }

    public void OnEntered(CancellationToken stateLifetime) => EnteredCalled++;
}

public sealed class SelLeafB : SelLogged, IChildState<SelComposite>;

public sealed class SelNested : SelLogged, IChildState<SelComposite>, IEntrySelector
{
    public Type SelectEntry() => typeof(SelNestedLeaf);
}

public sealed class SelNestedLeaf : SelLogged, IChildState<SelNested>;

public sealed class SelForeign : SelLogged, IChildState<SelRoot>;

public sealed class SelAsyncOnlyLeaf : SelLogged, IChildState<SelComposite>, IAsyncEnterController
{
    public UniTask OnEnterAsync(CancellationToken cancellationToken) => UniTask.CompletedTask;
}

/// <summary>Composite whose async enter blocks until completed or cancelled.</summary>
public sealed class SelAsyncComposite : SelLogged, IChildState<SelRoot>, IAsyncEnterController, IEntrySelector
{
    private readonly UniTaskCompletionSource _enter = new();

    public int SelectCalled { get; private set; }

    public void CompleteEnter() => _enter.TrySetResult();

    public async UniTask OnEnterAsync(CancellationToken cancellationToken)
    {
        using var registration = cancellationToken.Register(() => _enter.TrySetCanceled(cancellationToken));
        await _enter.Task;
        Log?.Add("entered async " + nameof(SelAsyncComposite));
    }

    public Type SelectEntry()
    {
        SelectCalled++;
        Log?.Add("select " + nameof(SelAsyncComposite));
        return typeof(SelAsyncLeaf);
    }
}

public sealed class SelAsyncLeaf : SelLogged, IChildState<SelAsyncComposite>;

/// <summary>Declares itself as its own parent.</summary>
public sealed class SelSelfParented : SelLogged, IChildState<SelSelfParented>;

/// <summary>Records the leaf every OnChangedState sees.</summary>
public sealed class LeafRecordingMachine(StateFactory factory) : StateMachineBase(factory)
{
    public List<Type> ChangedLeaves { get; } = [];

    public int Changing { get; private set; }

    protected override void OnChangingState() => Changing++;

    protected override void OnChangedState() => ChangedLeaves.Add(CurrentStates[^1].GetType());
}

#endregion

// ChangeState<Composite>() made the composite the leaf. A composite that implements IEntrySelector now picks its leaf, so a
// controller can move to a stage without knowing which kind-specific leaf runs under it.
public class EntrySelectorTests
{
    private readonly List<string> _log = [];
    private readonly SelComposite _composite = new();
    private readonly SelLeafA _leafA = new();
    private readonly SelAsyncComposite _asyncComposite = new();
    private readonly TestStateFactory _factory = new();
    private readonly LeafRecordingMachine _sm;

    public EntrySelectorTests()
    {
        _factory.RegisterState(() => Logged(new SelRoot()));
        _factory.RegisterState(() => Logged(_composite));
        _factory.RegisterState(() => Logged(_leafA));
        _factory.RegisterState(() => Logged(new SelLeafB()));
        _factory.RegisterState(() => Logged(new SelNested()));
        _factory.RegisterState(() => Logged(new SelNestedLeaf()));
        _factory.RegisterState(() => Logged(new SelForeign()));
        _factory.RegisterState(() => Logged(new SelAsyncOnlyLeaf()));
        _factory.RegisterState(() => Logged(_asyncComposite));
        _factory.RegisterState(() => Logged(new SelAsyncLeaf()));
        _factory.RegisterState(() => Logged(new SelSelfParented()));
        _sm = new LeafRecordingMachine(_factory);
    }

    private T Logged<T>(T state) where T : SelLogged
    {
        state.Log = _log;
        return state;
    }

    private Type[] Chain => _sm.CurrentStates.Select(state => state.GetType()).ToArray();

    [Fact]
    public void A_composite_target_enters_the_child_it_selects_in_the_same_change()
    {
        _sm.ChangeState<SelComposite>();

        Assert.Equal(new[] { typeof(SelRoot), typeof(SelComposite), typeof(SelLeafA) }, Chain);
        Assert.Equal(new[] { "enter SelRoot", "enter SelComposite", "select SelComposite", "enter SelLeafA" }, _log);

        // One change: one OnChangingState / OnChangedState pair, which already sees the final leaf.
        Assert.Equal(1, _sm.Changing);
        Assert.Equal(new[] { typeof(SelLeafA) }, _sm.ChangedLeaves);
    }

    [Fact]
    public void OnEntered_runs_once_the_selected_leaf_is_entered()
    {
        _sm.ChangeState<SelComposite>();

        Assert.Equal(1, _composite.EnteredCalled);
        Assert.Equal(1, _leafA.EnteredCalled);
    }

    [Fact]
    public void Nested_selectors_descend_until_a_plain_leaf()
    {
        _composite.Entry = () => typeof(SelNested);

        _sm.ChangeState<SelComposite>();

        Assert.Equal(new[] { typeof(SelRoot), typeof(SelComposite), typeof(SelNested), typeof(SelNestedLeaf) }, Chain);
        Assert.Equal(new[] { typeof(SelNestedLeaf) }, _sm.ChangedLeaves);
    }

    [Fact]
    public void A_change_straight_to_a_leaf_passes_the_selector_by()
    {
        _sm.ChangeState<SelLeafB>();

        Assert.Equal(new[] { typeof(SelRoot), typeof(SelComposite), typeof(SelLeafB) }, Chain);
        Assert.Equal(0, _composite.SelectCalled);
    }

    [Fact]
    public void A_change_between_siblings_does_not_enter_the_selector_again()
    {
        _sm.ChangeState<SelComposite>();

        _sm.ChangeState<SelLeafB>();

        Assert.Equal(1, _composite.EnterCalled);
        Assert.Equal(1, _composite.SelectCalled);
        Assert.Equal(typeof(SelLeafB), Chain[^1]);
    }

    // Targeting the composite while one of its children is active: the child exits, the composite stays, and it is
    // asked again, even when it picks the child that just exited.
    [Fact]
    public void Targeting_the_active_composite_again_asks_it_again_without_entering_it()
    {
        _sm.ChangeState<SelComposite>();

        _sm.ChangeState<SelComposite>();

        Assert.Equal(new[] { typeof(SelRoot), typeof(SelComposite), typeof(SelLeafA) }, Chain);
        Assert.Equal(1, _composite.EnterCalled);
        Assert.Equal(2, _composite.SelectCalled);
        Assert.Equal(2, _leafA.EnterCalled);
        Assert.Equal(2, _leafA.EnteredCalled);
    }

    [Fact]
    public async Task Targeting_the_active_composite_again_async_asks_it_again_without_entering_it()
    {
        var task = _sm.ChangeStateAsync<SelAsyncComposite>();
        _asyncComposite.CompleteEnter();
        await task;

        await _sm.ChangeStateAsync<SelAsyncComposite>();

        Assert.Equal(new[] { typeof(SelRoot), typeof(SelAsyncComposite), typeof(SelAsyncLeaf) }, Chain);
        Assert.Equal(1, _asyncComposite.EnterCalled);
        Assert.Equal(2, _asyncComposite.SelectCalled);
    }

    [Fact]
    public void TransitionTo_a_composite_descends_like_ChangeState()
    {
        _sm.TransitionTo<SelComposite>();

        Assert.Equal(typeof(SelLeafA), Chain[^1]);
    }

    [Fact]
    public void A_null_entry_throws_with_the_selectors_name()
    {
        _composite.Entry = () => null!;

        var exception = Assert.Throws<InvalidOperationException>(() => _sm.ChangeState<SelComposite>());

        Assert.Contains(nameof(SelComposite), exception.Message);
        Assert.Contains("null", exception.Message);
    }

    [Fact]
    public void An_entry_that_is_not_a_direct_child_throws_with_both_names()
    {
        _composite.Entry = () => typeof(SelForeign);

        var exception = Assert.Throws<InvalidOperationException>(() => _sm.ChangeState<SelComposite>());

        Assert.Contains(nameof(SelComposite), exception.Message);
        Assert.Contains(nameof(SelForeign), exception.Message);
        // The change stops at the selector, closed like any change that threw.
        Assert.Equal(typeof(SelComposite), Chain[^1]);
        Assert.Equal(_sm.Changing, _sm.ChangedLeaves.Count);
    }

    [Fact]
    public void A_grandchild_entry_throws_too()
    {
        _composite.Entry = () => typeof(SelNestedLeaf);

        var exception = Assert.Throws<InvalidOperationException>(() => _sm.ChangeState<SelComposite>());

        Assert.Contains(nameof(SelNestedLeaf), exception.Message);
    }

    // The only way an entry can already be in the chain is a cycle in the declared parents; such a chain never reached a
    // root and used to hang the change.
    [Fact]
    public void A_cycle_in_the_declared_parents_throws_instead_of_hanging()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => _sm.ChangeState<SelSelfParented>());

        Assert.Contains("cycle", exception.Message);
        Assert.Contains(nameof(SelSelfParented), exception.Message);
    }

    [Fact]
    public void A_sync_change_into_an_async_only_entry_throws()
    {
        _composite.Entry = () => typeof(SelAsyncOnlyLeaf);

        var exception = Assert.Throws<InvalidOperationException>(() => _sm.ChangeState<SelComposite>());

        Assert.Contains(nameof(SelAsyncOnlyLeaf), exception.Message);
        Assert.Contains("ChangeStateAsync", exception.Message);

        // Known only once the selector has been entered: the chain ends at the selector.
        Assert.Equal(typeof(SelComposite), Chain[^1]);
    }

    [Fact]
    public async Task ChangeStateAsync_asks_the_selector_only_after_the_async_enter_completed()
    {
        var task = _sm.ChangeStateAsync<SelAsyncComposite>();
        Assert.Equal(0, _asyncComposite.SelectCalled);
        Assert.True(_sm.IsTransitioning);

        _asyncComposite.CompleteEnter();
        await task;

        Assert.Equal(new[] { typeof(SelRoot), typeof(SelAsyncComposite), typeof(SelAsyncLeaf) }, Chain);
        Assert.Equal(
            new[] { "enter SelRoot", "enter SelAsyncComposite", "entered async SelAsyncComposite", "select SelAsyncComposite", "enter SelAsyncLeaf" },
            _log);
        Assert.Equal(new[] { typeof(SelAsyncLeaf) }, _sm.ChangedLeaves);
    }

    [Fact]
    public async Task A_cancelled_enter_never_asks_the_selector()
    {
        using var cts = new CancellationTokenSource();
        var task = _sm.ChangeStateAsync<SelAsyncComposite>(cts.Token);

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);

        Assert.Equal(0, _asyncComposite.SelectCalled);
        Assert.DoesNotContain(typeof(SelAsyncComposite), Chain);
    }

    [Fact]
    public async Task A_superseded_enter_never_asks_the_selector()
    {
        var first = _sm.ChangeStateAsync<SelAsyncComposite>();
        var second = _sm.ChangeStateAsync<SelLeafB>();

        await Assert.ThrowsAsync<TransitionSupersededException>(async () => await first);
        await second;

        Assert.Equal(0, _asyncComposite.SelectCalled);
        Assert.Equal(typeof(SelLeafB), Chain[^1]);
    }
}
