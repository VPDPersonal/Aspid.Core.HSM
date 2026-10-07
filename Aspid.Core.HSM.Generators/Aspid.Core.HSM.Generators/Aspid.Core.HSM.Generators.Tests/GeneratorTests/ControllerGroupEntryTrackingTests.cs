using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Immutable;
using Aspid.Core.HSM.Generators.ControllerGroup;
using Cysharp.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.GeneratorTests;

// Compiles the generated groups together with a ProfilerMarker stub and runs them, so the tests check what a
// group actually calls after an enter that threw or was cancelled halfway, not only the emitted text.
public class ControllerGroupEntryTrackingTests
{
    private const string Source = """
        using System;
        using System.Threading;
        using Aspid.Core.HSM;
        using Cysharp.Threading.Tasks;
        using System.Collections.Generic;

        namespace Unity.Profiling
        {
            public readonly struct ProfilerMarker
            {
                public ProfilerMarker(string name) { }

                public Scope Auto() => default;

                public readonly struct Scope : IDisposable
                {
                    public void Dispose() { }
                }
            }
        }

        namespace Sample
        {
            public static class Log
            {
                public static readonly List<string> Lines = new();
                public static bool ThrowOnEnter;
                public static CancellationTokenSource? CancelOnEnter;
            }

            public sealed class Before : IEnterController, IExitController, IUpdateController
            {
                public void OnEnter() => Log.Lines.Add("before.enter");
                public void OnExit() => Log.Lines.Add("before.exit");
                public void Update(float deltaTime) => Log.Lines.Add("before.tick");
            }

            public sealed class Failing : IAsyncEnterController, IAsyncExitController
            {
                public UniTask OnEnterAsync(CancellationToken cancellationToken)
                {
                    Log.Lines.Add("failing.enter");
                    Log.CancelOnEnter?.Cancel();

                    if (Log.ThrowOnEnter)
                        throw new InvalidOperationException("enter failed");

                    return UniTask.CompletedTask;
                }

                public UniTask OnExitAsync(CancellationToken cancellationToken)
                {
                    Log.Lines.Add("failing.exit");
                    return UniTask.CompletedTask;
                }
            }

            public sealed class After : IEnterController, IExitController, IUpdateController
            {
                public void OnEnter() => Log.Lines.Add("after.enter");
                public void OnExit() => Log.Lines.Add("after.exit");
                public void Update(float deltaTime) => Log.Lines.Add("after.tick");
            }

            public sealed class ExitOnly : IExitController
            {
                public void OnExit() => Log.Lines.Add("exitOnly.exit");
            }

            public sealed class SyncFailing : IEnterController, IExitController
            {
                public void OnEnter()
                {
                    Log.Lines.Add("syncFailing.enter");
                    throw new InvalidOperationException("enter failed");
                }

                public void OnExit() => Log.Lines.Add("syncFailing.exit");
            }

            public sealed class Ticker : IUpdateController
            {
                public void Update(float deltaTime) => Log.Lines.Add("ticker.tick");
            }

            [ControllerGroup]
            [AsyncMode(typeof(IAsyncEnterController), AsyncExecutionMode.Sequential)]
            [AsyncMode(typeof(IAsyncExitController), AsyncExecutionMode.Sequential)]
            public sealed partial class SequentialGroup
            {
                public SequentialGroup()
                {
                    AddControllers(new Before(), new Failing(), new After(), new ExitOnly());
                }
            }

            [ControllerGroup]
            public sealed partial class SyncGroup
            {
                public SyncGroup()
                {
                    AddControllers(new Before(), new SyncFailing(), new After());
                }
            }

            [ControllerGroup]
            public sealed partial class TickOnlyGroup
            {
                public TickOnlyGroup()
                {
                    AddControllers(new Ticker());
                }
            }
        }
        """;

    private static readonly Lazy<Assembly> s_assembly = new(CompileSource);

    [Fact]
    public void Async_enter_that_throws_exits_only_the_controllers_it_reached()
    {
        var group = Create("SequentialGroup");
        Reset(throwOnEnter: true);

        Assert.Throws<InvalidOperationException>(() => Enter(group));
        Exit(group);

        Assert.Equal(new[] { "before.enter", "failing.enter", "failing.exit", "before.exit" }, Lines());
    }

    [Fact]
    public void Async_enter_that_throws_ticks_only_the_controllers_it_reached()
    {
        var group = Create("SequentialGroup");
        Reset(throwOnEnter: true);

        Assert.Throws<InvalidOperationException>(() => Enter(group));
        Lines().Clear();
        ((IUpdateController)group).Update(0f);

        Assert.Equal(new[] { "before.tick" }, Lines());
    }

    [Fact]
    public void Cancelled_async_enter_stops_before_the_next_controller()
    {
        var group = Create("SequentialGroup");
        Reset(throwOnEnter: false);
        using var cancellation = new CancellationTokenSource();
        SetStatic("CancelOnEnter", cancellation);

        Assert.ThrowsAny<OperationCanceledException>(() => Enter(group, cancellation.Token));
        Exit(group);

        Assert.DoesNotContain("after.enter", Lines());
        Assert.DoesNotContain("after.exit", Lines());
        Assert.DoesNotContain("exitOnly.exit", Lines());
        Assert.Contains("failing.exit", Lines());
    }

    [Fact]
    public void Completed_enter_exits_every_controller_including_exit_only_ones()
    {
        var group = Create("SequentialGroup");
        Reset(throwOnEnter: false);

        Enter(group);
        Lines().Clear();
        Exit(group);

        Assert.Equal(new[] { "exitOnly.exit", "after.exit", "failing.exit", "before.exit" }, Lines());
    }

    [Fact]
    public void Exit_resets_progress_so_a_later_tick_reaches_nothing()
    {
        var group = Create("SequentialGroup");
        Reset(throwOnEnter: false);

        Enter(group);
        Exit(group);
        Lines().Clear();
        ((IUpdateController)group).Update(0f);

        Assert.Empty(Lines());
    }

    [Fact]
    public void Sync_enter_that_throws_exits_only_the_controllers_it_reached()
    {
        var group = Create("SyncGroup");
        Reset(throwOnEnter: false);

        Assert.Throws<InvalidOperationException>(() => ((IEnterController)group).OnEnter());
        ((IExitController)group).OnExit();

        Assert.Equal(new[] { "before.enter", "syncFailing.enter", "syncFailing.exit", "before.exit" }, Lines());
    }

    [Fact]
    public void Group_without_enter_ticks_without_entering()
    {
        var group = Create("TickOnlyGroup");
        Reset(throwOnEnter: false);

        ((IUpdateController)group).Update(0f);

        Assert.Equal(new[] { "ticker.tick" }, Lines());
    }

    private static object Create(string typeName) =>
        Activator.CreateInstance(s_assembly.Value.GetType($"Sample.{typeName}", throwOnError: true)!)!;

    private static void Enter(object group, CancellationToken cancellationToken = default) =>
        ((IAsyncEnterController)group).OnEnterAsync(cancellationToken).GetAwaiter().GetResult();

    private static void Exit(object group) =>
        ((IAsyncExitController)group).OnExitAsync(CancellationToken.None).GetAwaiter().GetResult();

    private static List<string> Lines() =>
        (List<string>)GetLogType().GetField("Lines")!.GetValue(null)!;

    private static void Reset(bool throwOnEnter)
    {
        Lines().Clear();
        SetStatic("ThrowOnEnter", throwOnEnter);
        SetStatic("CancelOnEnter", null);
    }

    private static void SetStatic(string field, object? value) =>
        GetLogType().GetField(field)!.SetValue(null, value);

    private static Type GetLogType() =>
        s_assembly.Value.GetType("Sample.Log", throwOnError: true)!;

    private static Assembly CompileSource()
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(Source);
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Append(typeof(UniTask).Assembly)
            .Append(typeof(IEnterController).Assembly)
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Distinct()
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToImmutableArray();

        var compilation = CSharpCompilation.Create(
            "EntryTrackingSample",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var generator = new ControllersGroupGenerator().AsSourceGenerator();
        CSharpGeneratorDriver.Create(generator)
            .RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

        using var stream = new MemoryStream();
        var result = outputCompilation.Emit(stream);
        var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();

        Assert.True(result.Success, string.Join(Environment.NewLine, errors.Select(e => e.ToString())));

        return Assembly.Load(stream.ToArray());
    }
}
