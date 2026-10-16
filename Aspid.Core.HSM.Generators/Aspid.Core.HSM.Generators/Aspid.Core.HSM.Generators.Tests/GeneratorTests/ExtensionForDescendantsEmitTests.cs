using System;
using System.Linq;
using System.Collections.Immutable;
using Aspid.Core.HSM.Generators.ExtensionState;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.GeneratorTests;

public class ExtensionForDescendantsEmitTests
{
    private const string Source = """
        using Aspid.Core.HSM;

        namespace Sample
        {
            public sealed class DrivingState : IState { }

            public sealed class OtherState : IState { }

            [ExtensionFor(typeof(DrivingState), typeof(OtherState), IncludeDescendants = true)]
            public sealed partial class PauseExtension : IExtensionState { }

            [ExtensionFor(typeof(DrivingState))]
            public sealed partial class LeafOnlyExtension : IExtensionState { }

            [ExtensionFor(typeof(DrivingState), IncludeDescendants = false)]
            public sealed partial class ExplicitLeafOnlyExtension : IExtensionState { }
        }
        """;

    [Fact]
    public void IncludeDescendants_checks_the_hosts_chain()
    {
        var (generated, _) = RunGenerator("PauseExtension");

        Assert.Contains(
            "global::Aspid.Core.HSM.StateHierarchy.IsSelfOrDescendantOf(hostState.GetType(), typeof(global::Sample.DrivingState)) || " +
            "global::Aspid.Core.HSM.StateHierarchy.IsSelfOrDescendantOf(hostState.GetType(), typeof(global::Sample.OtherState))",
            generated);
    }

    [Theory]
    [InlineData("LeafOnlyExtension")]
    [InlineData("ExplicitLeafOnlyExtension")]
    public void Without_the_flag_the_pattern_is_unchanged(string extension)
    {
        var (generated, _) = RunGenerator(extension);

        Assert.Contains("hostState is global::Sample.DrivingState;", generated);
        Assert.DoesNotContain("StateHierarchy", generated);
    }

    [Fact]
    public void Generated_extensions_compile()
    {
        var (_, diagnostics) = RunGenerator("PauseExtension");

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    private static (string Generated, ImmutableArray<Diagnostic> Diagnostics) RunGenerator(string extension)
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

        var driver = CSharpGeneratorDriver.Create(new ExtensionStateGenerator().AsSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);

        var generatedFile = output.SyntaxTrees.FirstOrDefault(t => t.FilePath.Contains(extension));
        Assert.NotNull(generatedFile);

        return (generatedFile!.ToString(), output.GetDiagnostics());
    }
}
