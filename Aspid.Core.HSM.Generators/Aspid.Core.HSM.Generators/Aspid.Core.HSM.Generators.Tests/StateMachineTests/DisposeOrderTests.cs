using System.Collections.Generic;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

#region Helpers

public abstract class DisposeOrderState : BaseTestState, IExitController, IDisposableController
{
    public List<string>? Log { get; set; }

    public void OnExit() => Log?.Add($"exit:{GetType().Name}");

    public void Dispose() => Log?.Add($"dispose:{GetType().Name}");
}

public sealed class DisposeOrderRoot : DisposeOrderState { }

public sealed class DisposeOrderMiddle : DisposeOrderState, IChildState<DisposeOrderRoot> { }

public sealed class DisposeOrderLeaf : DisposeOrderState, IChildState<DisposeOrderMiddle> { }

public sealed class DisposeOrderOther : BaseTestState { }

#endregion

/// <summary>
/// Dispose used to walk the chain from the root, so a parent was disposed while its children still relied on it.
/// </summary>
public class DisposeOrderTests
{
    private readonly List<string> _log = new();
    private readonly TestableStateMachine _sm;

    public DisposeOrderTests()
    {
        var factory = new TestStateFactory();
        factory.RegisterState(() => new DisposeOrderRoot { Log = _log });
        factory.RegisterState(() => new DisposeOrderMiddle { Log = _log });
        factory.RegisterState(() => new DisposeOrderLeaf { Log = _log });
        factory.RegisterState(() => new DisposeOrderOther());
        _sm = new TestableStateMachine(factory);
    }

    [Fact]
    public void Dispose_releases_the_chain_from_leaf_to_root()
    {
        _sm.ChangeState<DisposeOrderLeaf>();

        _sm.Dispose();

        Assert.Equal(
            new[] { "dispose:DisposeOrderLeaf", "dispose:DisposeOrderMiddle", "dispose:DisposeOrderRoot" },
            _log);
    }

    [Fact]
    public void Dispose_follows_the_order_of_an_exit()
    {
        _sm.ChangeState<DisposeOrderLeaf>();
        _sm.ChangeState<DisposeOrderOther>();
        var exitOrder = _log.ConvertAll(entry => entry.Replace("exit:", ""));
        _log.Clear();

        _sm.ChangeState<DisposeOrderLeaf>();
        _sm.Dispose();
        var disposeOrder = _log.ConvertAll(entry => entry.Replace("dispose:", ""));

        Assert.Equal(exitOrder, disposeOrder);
    }
}
