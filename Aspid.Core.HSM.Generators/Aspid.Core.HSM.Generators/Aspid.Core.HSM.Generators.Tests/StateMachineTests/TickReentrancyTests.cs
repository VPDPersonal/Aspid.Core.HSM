using System;
using System.Linq;
using System.Collections.Generic;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

#region Helpers

/// <summary>Root whose every tick kind runs <see cref="OnTick"/>; records the order states were ticked in.</summary>
public sealed class TickRootState : BaseTestState, IUpdateController, ILateUpdateController, IFixedUpdateController
{
    public Action? OnTick { get; set; }

    public List<string>? Log { get; set; }

    public void Update(float deltaTime) => Tick();

    public void LateUpdate(float deltaTime) => Tick();

    public void FixedUpdate(float deltaTime) => Tick();

    private void Tick()
    {
        Log?.Add(nameof(TickRootState));
        OnTick?.Invoke();
    }
}

public sealed class TickLeafState : BaseTestState, IChildState<TickRootState>,
    IUpdateController, ILateUpdateController, IFixedUpdateController
{
    public Action? OnTick { get; set; }

    public List<string>? Log { get; set; }

    public int TickCount { get; private set; }

    public void Update(float deltaTime) => Tick();

    public void LateUpdate(float deltaTime) => Tick();

    public void FixedUpdate(float deltaTime) => Tick();

    private void Tick()
    {
        TickCount++;
        Log?.Add(nameof(TickLeafState));
        OnTick?.Invoke();
    }
}

public sealed class TickAltLeafState : BaseTestState, IChildState<TickRootState>, IEnterController
{
    public List<string>? Log { get; set; }

    public void OnEnter() => Log?.Add(nameof(TickAltLeafState) + ".Enter");
}

public sealed class TickOtherLeafState : BaseTestState, IChildState<TickRootState> { }

/// <summary>Extension that runs <see cref="OnTick"/> from its own update.</summary>
public sealed class TickExtensionState : BaseTestState, IExtensionState, IUpdateController
{
    public Action? OnTick { get; set; }

    public int TickCount { get; private set; }

    public bool CanAttachTo(IState hostState) => true;

    public void Update(float deltaTime)
    {
        TickCount++;
        OnTick?.Invoke();
    }
}

public sealed class TickSecondExtensionState : BaseTestState, IExtensionState, IUpdateController
{
    public int TickCount { get; private set; }

    public bool CanAttachTo(IState hostState) => true;

    public void Update(float deltaTime) => TickCount++;
}

#endregion

public class TickReentrancyTests
{
    private readonly TickRootState _root = new();
    private readonly TickLeafState _leaf = new();
    private readonly TickAltLeafState _alt = new();
    private readonly TickOtherLeafState _other = new();
    private readonly TickExtensionState _extension = new();
    private readonly TickSecondExtensionState _secondExtension = new();
    private readonly TestableStateMachine _sm;

    public TickReentrancyTests()
    {
        var factory = new TestStateFactory();
        factory.RegisterState(() => _root);
        factory.RegisterState(() => _leaf);
        factory.RegisterState(() => _alt);
        factory.RegisterState(() => _other);
        factory.RegisterState(() => _extension);
        factory.RegisterState(() => _secondExtension);

        _sm = new TestableStateMachine(factory);
        _sm.ChangeState<TickLeafState>();
    }

    public static TheoryData<string> TickKinds => new() { "Update", "LateUpdate", "FixedUpdate" };

    private void Tick(string kind)
    {
        switch (kind)
        {
            case "Update": _sm.CallUpdate(0.016f); break;
            case "LateUpdate": _sm.CallLateUpdate(0.016f); break;
            case "FixedUpdate": _sm.CallFixedUpdate(0.02f); break;
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    // ChangeState from a controller used to rewrite _currentStates while the tick's foreach walked it:
    // "Collection was modified". The change is now applied once the whole chain has been ticked.
    [Theory]
    [MemberData(nameof(TickKinds))]
    public void ChangeState_from_a_tick_is_applied_after_every_state_is_ticked(string kind)
    {
        var log = new List<string>();
        _root.Log = log;
        _leaf.Log = log;
        _alt.Log = log;
        _root.OnTick = () => _sm.ChangeState<TickAltLeafState>();

        Tick(kind);

        Assert.Equal(
            new[] { nameof(TickRootState), nameof(TickLeafState), nameof(TickAltLeafState) + ".Enter" },
            log);
        Assert.Equal(new IState[] { _root, _alt }, _sm.CurrentStates.ToArray());
        Assert.Equal(1, _leaf.ExitCalled);
        Assert.Equal(1, _alt.EnterCalled);
    }

    [Fact]
    public void Several_requests_from_one_tick_are_applied_in_order()
    {
        _root.OnTick = () => _sm.ChangeState<TickAltLeafState>();
        _leaf.OnTick = () => _sm.ChangeState<TickOtherLeafState>();

        _sm.CallUpdate(0.016f);

        Assert.Equal(new IState[] { _root, _other }, _sm.CurrentStates.ToArray());
        Assert.Equal(1, _alt.EnterCalled);
        Assert.Equal(1, _alt.ExitCalled);
        Assert.Equal(1, _other.EnterCalled);
    }

    [Fact]
    public void Request_from_a_tick_is_applied_before_the_next_tick()
    {
        _root.OnTick = () =>
        {
            _root.OnTick = null;
            _sm.ChangeState<TickAltLeafState>();
        };

        _sm.CallUpdate(0.016f);
        _sm.CallLateUpdate(0.016f);

        // The leaf was exited between Update and LateUpdate, so LateUpdate did not reach it.
        Assert.Equal(1, _leaf.TickCount);
        Assert.Equal(new IState[] { _root, _alt }, _sm.CurrentStates.ToArray());
    }

    [Fact]
    public void A_throwing_controller_drops_the_requests_made_earlier_in_its_tick()
    {
        _root.OnTick = () => _sm.ChangeState<TickAltLeafState>();
        _leaf.OnTick = () => throw new InvalidOperationException("boom");

        Assert.Throws<InvalidOperationException>(() => _sm.CallUpdate(0.016f));
        Assert.Equal(new IState[] { _root, _leaf }, _sm.CurrentStates.ToArray());

        // The dropped request does not resurface in a later, unrelated change.
        _root.OnTick = null;
        _leaf.OnTick = null;
        _sm.ChangeState<TickOtherLeafState>();

        Assert.Equal(new IState[] { _root, _other }, _sm.CurrentStates.ToArray());
        Assert.Equal(0, _alt.EnterCalled);
    }

    [Fact]
    public void Extension_detaching_itself_during_a_tick_does_not_break_the_tick()
    {
        _sm.AttachExtension<TickExtensionState>();
        _sm.AttachExtension<TickSecondExtensionState>();
        _extension.OnTick = () => _sm.DetachExtension<TickExtensionState>();

        _sm.CallUpdate(0.016f);

        Assert.Equal(1, _extension.TickCount);
        Assert.Equal(1, _extension.ExitCalled);
        Assert.Equal(new IExtensionState[] { _secondExtension }, _sm.ActiveExtensions.ToArray());
    }

    [Fact]
    public void Extension_attached_from_a_state_tick_does_not_break_the_tick()
    {
        _root.OnTick = () =>
        {
            _root.OnTick = null;
            _sm.AttachExtension<TickExtensionState>();
        };

        _sm.CallUpdate(0.016f);

        Assert.Equal(1, _leaf.TickCount);
        Assert.Equal(new IExtensionState[] { _extension }, _sm.ActiveExtensions.ToArray());
    }

    [Fact]
    public void ChangeState_from_an_extension_tick_is_deferred_too()
    {
        _sm.AttachExtension<TickExtensionState>();
        _sm.AttachExtension<TickSecondExtensionState>();
        _extension.OnTick = () =>
        {
            _extension.OnTick = null;
            _sm.ChangeState<TickAltLeafState>();
        };

        _sm.CallUpdate(0.016f);

        // The second extension still got its tick before the change was applied.
        Assert.Equal(1, _secondExtension.TickCount);
        Assert.Equal(new IState[] { _root, _alt }, _sm.CurrentStates.ToArray());
    }
}
