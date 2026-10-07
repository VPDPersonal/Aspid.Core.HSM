using System;
using System.Linq;
using Unity.Profiling;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

public sealed class ProfiledGenericState<T> : BaseTestState, IUpdateController
{
    public void Update(float deltaTime) { }
}

public static class ProfiledOuter<T>
{
    public sealed class Inner : BaseTestState, IUpdateController
    {
        public void Update(float deltaTime) { }
    }

    public sealed class Generic<TInner> : BaseTestState, IUpdateController
    {
        public void Update(float deltaTime) { }
    }
}

public class ProfilerMarkerTests
{
    private const string Parent = "Aspid.Core.HSM.Generators.Tests.StateMachineTests.ParentTestState";
    private const string Child = "Aspid.Core.HSM.Generators.Tests.StateMachineTests.ChildTestState";

    private readonly TestStateFactory _factory = new();
    private readonly TestableStateMachine _sm;

    public ProfilerMarkerTests()
    {
        ProfilerMarker.Samples.Clear();

        _factory.RegisterState<ParentTestState>();
        _factory.RegisterState<ChildTestState>();
        _factory.RegisterState<SimpleTestState>();
        _factory.RegisterState<UpdateableTestState>();
        _factory.RegisterState<ProfiledGenericState<int>>();
        _factory.RegisterState<ProfiledOuter<int>.Inner>();
        _factory.RegisterState<ProfiledOuter<int>.Generic<string>>();
        _sm = new TestableStateMachine(_factory);
    }

    [Fact]
    public void Enter_is_sampled_per_state_with_its_full_type_name()
    {
        _sm.ChangeState<ChildTestState>();

        Assert.Equal(
            new[] { "begin HSM.Enter " + Parent, "end HSM.Enter " + Parent, "begin HSM.Enter " + Child, "end HSM.Enter " + Child },
            ProfilerMarker.Samples.Where(s => s.Contains("HSM.Enter")));
    }

    [Fact]
    public void Exit_is_sampled_per_state()
    {
        _sm.ChangeState<ChildTestState>();
        ProfilerMarker.Samples.Clear();

        _sm.ChangeState<SimpleTestState>();

        Assert.Equal(
            new[] { "begin HSM.Exit " + Child, "end HSM.Exit " + Child, "begin HSM.Exit " + Parent, "end HSM.Exit " + Parent },
            ProfilerMarker.Samples.Where(s => s.Contains("HSM.Exit")));
    }

    [Fact]
    public void Tick_is_sampled_per_state_and_tick_kind()
    {
        _sm.ChangeState<UpdateableTestState>();
        ProfilerMarker.Samples.Clear();

        _sm.CallUpdate(0.016f);

        const string name = "Aspid.Core.HSM.Generators.Tests.StateMachineTests.UpdateableTestState";
        Assert.Equal(new[] { "begin HSM.Update " + name, "end HSM.Update " + name }, ProfilerMarker.Samples);
    }

    [Fact]
    public void Generic_state_marker_spells_out_its_type_arguments()
    {
        _sm.ChangeState<ProfiledGenericState<int>>();
        ProfilerMarker.Samples.Clear();

        _sm.CallUpdate(0.016f);

        Assert.Contains(
            "begin HSM.Update Aspid.Core.HSM.Generators.Tests.StateMachineTests.ProfiledGenericState<System.Int32>",
            ProfilerMarker.Samples);
    }

    [Theory]
    [InlineData(typeof(ProfiledOuter<int>.Inner), "ProfiledOuter<System.Int32>.Inner")]
    [InlineData(typeof(ProfiledOuter<int>.Generic<string>), "ProfiledOuter<System.Int32>.Generic<System.String>")]
    public void Nested_generic_state_marker_keeps_each_segment(Type stateType, string expectedName)
    {
        _sm.ChangeState(stateType);
        ProfilerMarker.Samples.Clear();

        _sm.CallUpdate(0.016f);

        // The nested name must not collapse into the outer type's name, and arguments stay on their segment.
        Assert.Equal(
            new[] { "begin HSM.Update Aspid.Core.HSM.Generators.Tests.StateMachineTests." + expectedName,
                    "end HSM.Update Aspid.Core.HSM.Generators.Tests.StateMachineTests." + expectedName },
            ProfilerMarker.Samples);
    }

    [Fact]
    public void Async_change_without_async_controllers_samples_like_a_sync_change()
    {
        _sm.ChangeState<ChildTestState>();
        var syncEnter = ProfilerMarker.Samples.ToArray();
        ProfilerMarker.Samples.Clear();
        _sm.ChangeState<SimpleTestState>();
        var syncExit = ProfilerMarker.Samples.Where(s => s.Contains("HSM.Exit")).ToArray();

        var asyncSm = new TestableStateMachine(_factory);
        ProfilerMarker.Samples.Clear();
        asyncSm.ChangeStateAsync<ChildTestState>().GetAwaiter().GetResult();
        var asyncEnter = ProfilerMarker.Samples.ToArray();
        ProfilerMarker.Samples.Clear();
        asyncSm.ChangeStateAsync<SimpleTestState>().GetAwaiter().GetResult();
        var asyncExit = ProfilerMarker.Samples.Where(s => s.Contains("HSM.Exit")).ToArray();

        // One sample per state either way, so the Profiler call count does not depend on the entry point.
        Assert.Equal(syncEnter, asyncEnter);
        Assert.Equal(syncExit, asyncExit);
    }

    [Fact]
    public void Async_enter_samples_only_synchronous_segments()
    {
        _sm.ChangeStateAsync<ChildTestState>().GetAwaiter().GetResult();

        // Every sample is closed: none spans an await.
        Assert.Equal(
            ProfilerMarker.Samples.Count(s => s.StartsWith("begin ")),
            ProfilerMarker.Samples.Count(s => s.StartsWith("end ")));
        Assert.Contains("begin HSM.Enter " + Child, ProfilerMarker.Samples);
    }
}
