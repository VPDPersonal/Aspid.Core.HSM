using System;
using System.Linq;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

/// <summary>
/// What [ExtensionFor(typeof(SelComposite), IncludeDescendants = true)] generates: the composite anywhere in the chain.
/// </summary>
public sealed class CompositePauseExtension : BaseTestState, IExtensionState
{
    public bool CanAttachTo(IState hostState) =>
        StateHierarchy.IsSelfOrDescendantOf(hostState.GetType(), typeof(SelComposite));
}

/// <summary>What [ExtensionFor(typeof(SelComposite))] generates: the composite must be the leaf.</summary>
public sealed class CompositeLeafOnlyExtension : BaseTestState, IExtensionState
{
    public bool CanAttachTo(IState hostState) => hostState is SelComposite;
}

// A pause bound to the driving stage had to list every kind-specific leaf under it.
public class ExtensionWithDescendantsTests
{
    private readonly TestStateFactory _factory = new();
    private readonly TestableStateMachine _sm;

    public ExtensionWithDescendantsTests()
    {
        _factory.RegisterState<SelRoot>();
        _factory.RegisterState<SelComposite>();
        _factory.RegisterState<SelLeafA>();
        _factory.RegisterState<SelLeafB>();
        _factory.RegisterState<SelForeign>();
        _factory.RegisterState<CompositePauseExtension>();
        _factory.RegisterState<CompositeLeafOnlyExtension>();
        _sm = new TestableStateMachine(_factory);
    }

    [Fact]
    public void Attaches_while_a_child_of_the_host_is_the_leaf()
    {
        _sm.ChangeState<SelLeafA>();

        _sm.AttachExtension<CompositePauseExtension>();

        Assert.IsType<CompositePauseExtension>(Assert.Single(_sm.ActiveExtensions));
    }

    [Fact]
    public void Stays_attached_while_the_leaf_changes_inside_the_host()
    {
        _sm.ChangeState<SelLeafA>();
        _sm.AttachExtension<CompositePauseExtension>();
        var extension = _sm.ActiveExtensions[0];

        _sm.ChangeState<SelLeafB>();

        Assert.Same(extension, Assert.Single(_sm.ActiveExtensions));
    }

    [Fact]
    public void Is_detached_once_the_host_exits()
    {
        _sm.ChangeState<SelLeafA>();
        _sm.AttachExtension<CompositePauseExtension>();

        _sm.ChangeState<SelForeign>();

        Assert.Empty(_sm.ActiveExtensions);
    }

    [Fact]
    public void Without_the_flag_the_host_must_still_be_the_leaf()
    {
        _sm.ChangeState<SelLeafA>();

        _sm.AttachExtension<CompositeLeafOnlyExtension>();

        Assert.Empty(_sm.ActiveExtensions);
    }

    [Fact]
    public void A_type_outside_the_host_is_not_its_descendant()
    {
        Assert.True(StateHierarchy.IsSelfOrDescendantOf(typeof(SelNestedLeaf), typeof(SelComposite)));
        Assert.True(StateHierarchy.IsSelfOrDescendantOf(typeof(SelComposite), typeof(SelComposite)));
        Assert.False(StateHierarchy.IsSelfOrDescendantOf(typeof(SelForeign), typeof(SelComposite)));
        Assert.False(StateHierarchy.IsSelfOrDescendantOf(typeof(SelRoot), typeof(SelComposite)));
    }
}
