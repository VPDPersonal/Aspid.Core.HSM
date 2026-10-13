using System;
using System.Linq;
using System.Collections.Immutable;
using Aspid.Core.HSM.Generators.ControllerGroup;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.GeneratorTests;

// IEnteredController is dispatched by a [ControllerGroup] like any other controller interface: the group implements
// it once and passes the state lifetime to each of its controllers.
public class EnteredControllerEmitTests
{
    private const string Source = """
        using System.Threading;
        using Aspid.Core.HSM;
        using Cysharp.Threading.Tasks;

        namespace Sample
        {
            public sealed class LoadController : IAsyncEnterController
            {
                public UniTask OnEnterAsync(CancellationToken cancellationToken) => UniTask.CompletedTask;
            }

            public sealed class FinishWatcher : IEnteredController
            {
                public void OnEntered(CancellationToken stateLifetime) { }
            }

            public sealed class HudController : IEnterController, IEnteredController
            {
                public void OnEnter() { }
                public void OnEntered(CancellationToken stateLifetime) { }
            }

            [ControllerGroup]
            public sealed partial class DrivingState : IState
            {
                public DrivingState(LoadController load, FinishWatcher finish, HudController hud)
                {
                    AddControllers(load, finish, hud);
                }
            }
        }
        """;

    [Fact]
    public void Group_implements_IEnteredController_and_passes_the_lifetime_on()
    {
        var (generated, _) = RunGenerator();

        Assert.Contains("void global::Aspid.Core.HSM.IEnteredController.OnEntered(global::System.Threading.CancellationToken stateLifetime)", generated);
        Assert.Contains("((global::Aspid.Core.HSM.IEnteredController)__controller1).OnEntered(stateLifetime);", generated);
        Assert.Contains("((global::Aspid.Core.HSM.IEnteredController)__controller2).OnEntered(stateLifetime);", generated);
    }

    [Fact]
    public void Generated_group_compiles()
    {
        var (_, diagnostics) = RunGenerator();

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    private static (string Generated, ImmutableArray<Diagnostic> Diagnostics) RunGenerator()
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

        var generator = new ControllersGroupGenerator().AsSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);

        var generatedFile = output.SyntaxTrees.FirstOrDefault(t => t.FilePath.Contains("DrivingState"));
        Assert.NotNull(generatedFile);

        return (generatedFile!.ToString(), output.GetDiagnostics());
    }
}
