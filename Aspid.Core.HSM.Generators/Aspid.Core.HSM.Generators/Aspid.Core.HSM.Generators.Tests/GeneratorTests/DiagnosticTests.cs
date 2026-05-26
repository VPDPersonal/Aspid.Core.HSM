using System;
using System.Linq;
using System.Collections.Immutable;
using Aspid.Core.HSM.Generators.ChildState;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.GeneratorTests;

public class DiagnosticTests
{
    [Fact]
    public void Cyclic_hierarchy_reports_HSM001()
    {
        const string source = """
            using System;
            namespace Aspid.Core.HSM
            {
                public interface IState { void Enter() { } void Exit() { } }
                public interface IChildState { Type ParentState { get; } }
                [AttributeUsage(AttributeTargets.Class)]
                public sealed class ParentStateAttribute : Attribute
                {
                    public ParentStateAttribute(Type parentState) { }
                }
            }
            namespace Test
            {
                using Aspid.Core.HSM;

                [ParentState(typeof(StateB))]
                public partial class StateA : IState { }

                [ParentState(typeof(StateA))]
                public partial class StateB : IState { }
            }
            """;

        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToImmutableArray();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new ChildStateGenerator().AsSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);
        var runResult = driver.GetRunResult();

        var diagnostics = runResult.Diagnostics;

        Assert.Contains(diagnostics, d => d.Id == "HSM001");
    }
}
