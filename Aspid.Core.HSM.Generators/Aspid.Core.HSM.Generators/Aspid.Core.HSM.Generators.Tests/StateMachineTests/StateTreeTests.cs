using System;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

#region Tree-only states (no IChildState — hierarchy is source of truth)
public class TreeRootState : BaseTestState { }
public class TreeChildState : BaseTestState { }
public class TreeSiblingState : BaseTestState { }
public class TreeGrandchildState : BaseTestState { }
#endregion

public class TreeTestStateFactory : TestStateFactory
{
    public TreeTestStateFactory(IStateHierarchy hierarchy) : base(hierarchy) { }
}

public class StateTreeTests
{
    private static StateTree BuildSampleTree() =>
        new StateTreeBuilder()
            .Root<TreeRootState>()
                .Child<TreeChildState>(c => c
                    .Child<TreeGrandchildState>())
                .Child<TreeSiblingState>()
            .Build();

    [Fact]
    public void Builder_DuplicateDeclaration_Throws()
    {
        var builder = new StateTreeBuilder();
        builder.Root<TreeRootState>().Child<TreeChildState>();

        Assert.Throws<InvalidOperationException>(() =>
            builder.Root<TreeRootState>());
    }

    [Fact]
    public void TryGetParent_ForRoot_ReturnsFalse()
    {
        var tree = BuildSampleTree();

        Assert.False(tree.TryGetParent(typeof(TreeRootState), out _));
    }

    [Fact]
    public void TryGetParent_ForChild_ReturnsParent()
    {
        var tree = BuildSampleTree();

        Assert.True(tree.TryGetParent(typeof(TreeChildState), out var parent));
        Assert.Equal(typeof(TreeRootState), parent);

        Assert.True(tree.TryGetParent(typeof(TreeGrandchildState), out var grandParent));
        Assert.Equal(typeof(TreeChildState), grandParent);
    }

    [Fact]
    public void ChangeState_ToTreeChild_EntersParentAndChild()
    {
        var tree = BuildSampleTree();
        var factory = new TreeTestStateFactory(tree);
        var root = new TreeRootState();
        var child = new TreeChildState();
        factory.RegisterState(() => root);
        factory.RegisterState(() => child);
        var sm = new TestableStateMachine(factory);

        sm.ChangeState<TreeChildState>();

        Assert.Equal(1, root.EnterCalled);
        Assert.Equal(1, child.EnterCalled);
        Assert.Equal(2, sm.CurrentStates.Count);
        Assert.Same(root, sm.CurrentStates[0]);
        Assert.Same(child, sm.CurrentStates[1]);
    }

    [Fact]
    public void ChangeState_FromTreeChildToSibling_ReusesParent()
    {
        var tree = BuildSampleTree();
        var factory = new TreeTestStateFactory(tree);
        var root = new TreeRootState();
        var child = new TreeChildState();
        var sibling = new TreeSiblingState();
        factory.RegisterState(() => root);
        factory.RegisterState(() => child);
        factory.RegisterState(() => sibling);
        var sm = new TestableStateMachine(factory);

        sm.ChangeState<TreeChildState>();
        factory.ClearReleasedStates();
        sm.ChangeState<TreeSiblingState>();

        Assert.Equal(1, child.ExitCalled);
        Assert.Equal(0, root.ExitCalled);
        Assert.Equal(1, sibling.EnterCalled);
        Assert.Single(factory.ReleasedStates);
        Assert.Contains(child, factory.ReleasedStates);
    }

    [Fact]
    public void ChangeState_ToTreeGrandchild_EntersFullChain()
    {
        var tree = BuildSampleTree();
        var factory = new TreeTestStateFactory(tree);
        var root = new TreeRootState();
        var child = new TreeChildState();
        var grandchild = new TreeGrandchildState();
        factory.RegisterState(() => root);
        factory.RegisterState(() => child);
        factory.RegisterState(() => grandchild);
        var sm = new TestableStateMachine(factory);

        sm.ChangeState<TreeGrandchildState>();

        Assert.Equal(3, sm.CurrentStates.Count);
        Assert.Same(root, sm.CurrentStates[0]);
        Assert.Same(child, sm.CurrentStates[1]);
        Assert.Same(grandchild, sm.CurrentStates[2]);
    }
}
