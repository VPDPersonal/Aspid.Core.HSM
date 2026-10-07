using System;
using System.Linq;
using System.Collections.Immutable;
using Aspid.Core.HSM.Generators.ControllerGroup;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.GeneratorTests;

// A stage controller names its successor in a generic argument (CountdownController<ClassicDrivingState>),
// so the group must keep the closed generic type in its field, its constructor and its profiler marker.
public class GenericControllerEmitTests
{
    private const string Source = """
        using System;
        using Aspid.Core.HSM;

        namespace Unity.Profiling
        {
            public readonly struct ProfilerMarker
            {
                public ProfilerMarker(string name) { }

                public AutoScope Auto() => default;

                public readonly struct AutoScope : IDisposable
                {
                    public void Dispose() { }
                }
            }
        }

        namespace Sample
        {
            public sealed class DrivingState : IState { }

            public sealed class FinishingState : IState { }

            public sealed class CountdownController<TNext> : IEnterController
                where TNext : IState
            {
                public void OnEnter() { }
            }

            [ControllerGroup]
            public sealed partial class CountdownState : IState
            {
                public CountdownState(
                    CountdownController<DrivingState> toDriving,
                    CountdownController<FinishingState> toFinishing)
                {
                    AddControllers(toDriving, toFinishing);
                }
            }
        }
        """;

    [Fact]
    public void Closed_generic_controllers_keep_their_type_arguments()
    {
        var (generated, _) = RunGenerator();

        Assert.Contains("global::Sample.CountdownController<global::Sample.DrivingState>", generated);
        Assert.Contains("global::Sample.CountdownController<global::Sample.FinishingState>", generated);
    }

    [Fact]
    public void Each_closed_generic_controller_gets_its_own_profiler_marker()
    {
        var (generated, _) = RunGenerator();

        Assert.Contains("new(\"Sample.CountdownController<Sample.DrivingState>\")", generated);
        Assert.Contains("new(\"Sample.CountdownController<Sample.FinishingState>\")", generated);
    }

    [Fact]
    public void Generated_group_with_generic_controllers_compiles()
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

        var generatedFile = output.SyntaxTrees.FirstOrDefault(t => t.FilePath.Contains("CountdownState"));
        Assert.NotNull(generatedFile);

        return (generatedFile!.ToString(), output.GetDiagnostics());
    }
}
