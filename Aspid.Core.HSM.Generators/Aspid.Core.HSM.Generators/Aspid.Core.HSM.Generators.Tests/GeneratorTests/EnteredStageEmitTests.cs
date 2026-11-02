using System;
using System.Linq;
using System.Collections.Immutable;
using Aspid.Core.HSM.Generators.ControllerGroup;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.GeneratorTests;

// The steps of an entered stage run in AddControllers order unless the group asks otherwise: IAsyncEnteredController
// declares Sequential as its default through [AsyncOf(..., DefaultMode = ...)], while other async pairs stay parallel.
public class EnteredStageEmitTests
{
    private const string Source = """
        using Aspid.Core.HSM;
        using Cysharp.Threading.Tasks;

        namespace Sample
        {
            public sealed class LoadStep : IAsyncEnteredController
            {
                public UniTask OnEnteredAsync(StateEntry entry) => UniTask.CompletedTask;
            }

            public sealed class HudStep : IEnteredController
            {
                public void OnEntered(StateEntry entry) { }
            }

            public sealed class GoToStep : IAsyncEnteredController
            {
                public UniTask OnEnteredAsync(StateEntry entry) => UniTask.CompletedTask;
            }

            [ControllerGroup]
            public sealed partial class LoadingState : IState
            {
                public LoadingState(LoadStep load, HudStep hud, GoToStep next) => AddControllers(load, hud, next);
            }

            [ControllerGroup]
            [AsyncMode(typeof(IAsyncEnteredController), AsyncExecutionMode.Parallel)]
            public sealed partial class ParallelState : IState
            {
                public ParallelState(LoadStep load, GoToStep next) => AddControllers(load, next);
            }

            public sealed class Hud : IEnterController, IExitController
            {
                public void OnEnter() { }

                public void OnExit() { }
            }

            public sealed class FailurePolicy : IEnteredFailedController
            {
                public void OnEnteredFailed(IState state, System.Exception exception) { }
            }

            [ControllerGroup]
            public sealed partial class GuardedState : IState
            {
                public GuardedState(Hud hud, LoadStep load, FailurePolicy policy) => AddControllers(hud, load, policy);
            }

            public sealed class LoadEnter : IAsyncEnterController
            {
                public UniTask OnEnterAsync(System.Threading.CancellationToken cancellationToken) => UniTask.CompletedTask;
            }

            [ControllerGroup]
            public sealed partial class EnterState : IState
            {
                public EnterState(LoadEnter first, LoadEnter second) => AddControllers(first, second);
            }
        }
        """;

    [Fact]
    public void Entered_steps_run_one_after_another_by_default()
    {
        var (generated, _) = RunGenerator("LoadingState");

        Assert.Contains("global::Aspid.Core.HSM.IAsyncEnteredController.OnEnteredAsync(global::Aspid.Core.HSM.StateEntry entry)", generated);
        Assert.DoesNotContain("WhenAll", generated);

        // In AddControllers order, the synchronous step between the two async ones.
        var load = generated.IndexOf("__controller0).OnEnteredAsync(entry)", StringComparison.Ordinal);
        var hud = generated.IndexOf("__controller1).OnEntered(entry)", StringComparison.Ordinal);
        var next = generated.IndexOf("__controller2).OnEnteredAsync(entry)", StringComparison.Ordinal);
        Assert.True(load >= 0 && hud > load && next > hud, generated);
    }

    [Fact]
    public void No_step_starts_once_the_state_has_exited()
    {
        var (generated, _) = RunGenerator("LoadingState");

        var checks = generated.Split("entry.Lifetime.ThrowIfCancellationRequested();").Length - 1;
        Assert.Equal(3, checks);
    }

    [Fact]
    public void The_failure_handler_of_a_group_is_not_switched_off_by_its_exit()
    {
        var (generated, _) = RunGenerator("GuardedState");

        var handler = generated.Substring(generated.IndexOf("IEnteredFailedController.OnEnteredFailed", StringComparison.Ordinal));
        handler = handler.Substring(0, handler.IndexOf("OnEnteredFailed(state,exception);", StringComparison.Ordinal));
        Assert.DoesNotContain("__enteredControllers", handler);

        // The stage of the same group stays guarded: its steps are skipped once the exit has run.
        Assert.Contains("__enteredControllers > 1", generated);
    }

    [Fact]
    public void Other_async_pairs_stay_parallel_by_default()
    {
        var (generated, _) = RunGenerator("EnterState");

        Assert.Contains("WhenAll", generated);
    }

    [Fact]
    public void AsyncMode_on_the_group_overrides_the_default()
    {
        var (generated, _) = RunGenerator("ParallelState");

        Assert.Contains("WhenAll", generated);
    }

    [Fact]
    public void Generated_groups_compile()
    {
        var (_, diagnostics) = RunGenerator("GuardedState");

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    private static (string Generated, ImmutableArray<Diagnostic> Diagnostics) RunGenerator(string state)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(Source);
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToImmutableArray();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver.Create(new ControllersGroupGenerator().AsSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);

        var generatedFile = output.SyntaxTrees.FirstOrDefault(t => t.FilePath.Contains(state));
        Assert.NotNull(generatedFile);

        return (generatedFile!.ToString(), output.GetDiagnostics());
    }
}
