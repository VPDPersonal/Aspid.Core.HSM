using System.Linq;
using Unity.Profiling;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

public sealed class ProfiledGenericState<T> : BaseTestState, IUpdateController
{
    public void Update(float deltaTime) { }
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
