using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

#region Test Helpers

public class TestExtensionState : BaseTestState, IExtensionState, IUpdateController
{
    public bool AttachResult { get; set; } = true;
    public IState? AttachedHost { get; private set; }
    public IState? DetachedHost { get; private set; }
    public int UpdateCount { get; private set; }

    public bool CanAttachTo(IState hostState) => AttachResult;
    public void OnAttached(IState hostState) => AttachedHost = hostState;
    public void OnDetached(IState hostState) => DetachedHost = hostState;
    public void Update(float deltaTime) => UpdateCount++;
}

/// <summary>
/// Extension that only attaches to <see cref="SimpleTestState"/>.
/// </summary>
public class SelectiveExtensionState : BaseTestState, IExtensionState, IUpdateController
{
    public int UpdateCount { get; private set; }
    public bool CanAttachTo(IState hostState) => hostState is SimpleTestState;
    public void OnAttached(IState hostState) { }
    public void OnDetached(IState hostState) { }
    public void Update(float deltaTime) => UpdateCount++;
}

#endregion

public class ExtensionStateTests
{
    #region Helpers

    private static (TestableStateMachine sm, TestStateFactory factory) CreateStateMachine()
    {
        var factory = new TestStateFactory();
        var sm = new TestableStateMachine(factory);
        return (sm, factory);
    }

    #endregion

    [Fact]
    public void AttachExtension_adds_extension_to_active_list()
    {
        var (sm, factory) = CreateStateMachine();
        var hostState = new SimpleTestState();
        var extension = new TestExtensionState();
        factory.RegisterState(() => hostState);
        factory.RegisterState(() => extension);

        sm.ChangeState<SimpleTestState>();
        sm.AttachExtension<TestExtensionState>();

        Assert.Single(sm.ActiveExtensions);
        Assert.Same(extension, sm.ActiveExtensions[0]);
    }

    [Fact]
    public void AttachExtension_calls_Enter_and_OnAttached()
    {
        var (sm, factory) = CreateStateMachine();
        var hostState = new SimpleTestState();
        var extension = new TestExtensionState();
        factory.RegisterState(() => hostState);
        factory.RegisterState(() => extension);

        sm.ChangeState<SimpleTestState>();
        sm.AttachExtension<TestExtensionState>();

        Assert.Equal(1, extension.EnterCalled);
        Assert.Same(hostState, extension.AttachedHost);
    }

    [Fact]
    public void AttachExtension_rejected_when_CanAttachTo_returns_false()
    {
        var (sm, factory) = CreateStateMachine();
        var hostState = new SimpleTestState();
        var extension = new TestExtensionState { AttachResult = false };
        factory.RegisterState(() => hostState);
        factory.RegisterState(() => extension);

        sm.ChangeState<SimpleTestState>();
        sm.AttachExtension<TestExtensionState>();

        Assert.Empty(sm.ActiveExtensions);
    }

    [Fact]
    public void DetachExtension_calls_OnDetached_and_Exit()
    {
        var (sm, factory) = CreateStateMachine();
        var hostState = new SimpleTestState();
        var extension = new TestExtensionState();
        factory.RegisterState(() => hostState);
        factory.RegisterState(() => extension);

        sm.ChangeState<SimpleTestState>();
        sm.AttachExtension<TestExtensionState>();
        sm.DetachExtension<TestExtensionState>();

        Assert.Same(hostState, extension.DetachedHost);
        Assert.Equal(1, extension.ExitCalled);
        Assert.Empty(sm.ActiveExtensions);
    }

    [Fact]
    public void Update_dispatches_to_extension_controllers_after_host()
    {
        var (sm, factory) = CreateStateMachine();
        var hostState = new UpdateableTestState();
        var extension = new TestExtensionState();
        factory.RegisterState(() => hostState);
        factory.RegisterState(() => extension);

        sm.ChangeState<UpdateableTestState>();
        sm.AttachExtension<TestExtensionState>();
        sm.CallUpdate(0.016f);

        Assert.Equal(1, hostState.UpdateCallCount);
        Assert.Equal(1, extension.UpdateCount);
    }

    [Fact]
    public void Auto_detach_on_incompatible_transition()
    {
        var (sm, factory) = CreateStateMachine();
        var simpleState = new SimpleTestState();
        var anotherState = new AnotherTestState();
        var extension = new SelectiveExtensionState();
        factory.RegisterState(() => simpleState);
        factory.RegisterState(() => anotherState);
        factory.RegisterState(() => extension);

        sm.ChangeState<SimpleTestState>();
        sm.AttachExtension<SelectiveExtensionState>();
        Assert.Single(sm.ActiveExtensions);

        // Transition to AnotherTestState — extension is incompatible (only attaches to SimpleTestState)
        sm.ChangeState<AnotherTestState>();

        Assert.Empty(sm.ActiveExtensions);
    }

    [Fact]
    public void Extension_survives_compatible_transition()
    {
        var (sm, factory) = CreateStateMachine();
        var firstState = new SimpleTestState();
        var secondState = new SimpleTestState();
        var extension = new TestExtensionState();
        // First call creates firstState, second call creates secondState
        var callCount = 0;
        factory.RegisterState<SimpleTestState>(() => callCount++ == 0 ? firstState : secondState);
        factory.RegisterState(() => extension);

        sm.ChangeState<SimpleTestState>();
        sm.AttachExtension<TestExtensionState>();
        Assert.Single(sm.ActiveExtensions);

        // Transition to another SimpleTestState instance — extension's CanAttachTo always returns true
        sm.ChangeState<SimpleTestState>();

        Assert.Single(sm.ActiveExtensions);
        Assert.Same(extension, sm.ActiveExtensions[0]);
    }

    [Fact]
    public void Multiple_extensions_dispatch_in_attachment_order()
    {
        var (sm, factory) = CreateStateMachine();
        var hostState = new SimpleTestState();
        var ext1 = new TestExtensionState();
        var ext2 = new SelectiveExtensionState();
        factory.RegisterState(() => hostState);
        factory.RegisterState(() => ext1);
        factory.RegisterState(() => ext2);

        sm.ChangeState<SimpleTestState>();
        sm.AttachExtension<TestExtensionState>();
        sm.AttachExtension<SelectiveExtensionState>();

        Assert.Equal(2, sm.ActiveExtensions.Count);
        Assert.Same(ext1, sm.ActiveExtensions[0]);
        Assert.Same(ext2, sm.ActiveExtensions[1]);

        sm.CallUpdate(0.016f);

        Assert.Equal(1, ext1.UpdateCount);
        Assert.Equal(1, ext2.UpdateCount);
    }
}
