using System;
using System.Linq;
using System.Collections.Immutable;
using Aspid.Core.HSM.Generators.Transition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.GeneratorTests;

public class TransitionEmitTests
{
    [Fact]
    public void Transition_attribute_generates_SourceState_and_TargetState_properties()
    {
        const string source = """
            using System;

            namespace Aspid.Core.HSM
            {
                public interface IState { void Enter() { } void Exit() { } }

                public interface ITransition
                {
                    Type SourceState { get; }
                    Type TargetState { get; }
                }

                [AttributeUsage(AttributeTargets.Class)]
                public sealed class TransitionAttribute : Attribute
                {
                    public TransitionAttribute(Type sourceState, Type targetState) { }
                }
            }

            namespace Test
            {
                using Aspid.Core.HSM;

                public class StateA : IState { }
                public class StateB : IState { }

                [Transition(typeof(StateA), typeof(StateB))]
                public partial class MyTransition : ITransition { }
            }
            """;

        var generated = RunGenerator(source, "MyTransition");

        Assert.Contains("Aspid.Core.HSM.ITransition.SourceState => typeof(global::Test.StateA)", generated);
        Assert.Contains("Aspid.Core.HSM.ITransition.TargetState => typeof(global::Test.StateB)", generated);
    }

    [Fact]
    public void Transition_without_attribute_generates_nothing()
    {
        const string source = """
            using System;

            namespace Aspid.Core.HSM
            {
                public interface IState { void Enter() { } void Exit() { } }

                public interface ITransition
                {
                    Type SourceState { get; }
                    Type TargetState { get; }
                }

                [AttributeUsage(AttributeTargets.Class)]
                public sealed class TransitionAttribute : Attribute
                {
                    public TransitionAttribute(Type sourceState, Type targetState) { }
                }
            }

            namespace Test
            {
                using Aspid.Core.HSM;

                public class StateA : IState { }
                public class StateB : IState { }

                public partial class PlainClass : ITransition
                {
                    public Type SourceState => typeof(StateA);
                    public Type TargetState => typeof(StateB);
                }
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

        var generator = new TransitionGenerator().AsSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);
        var runResult = driver.GetRunResult();

        var generatedFile = runResult.GeneratedTrees
            .FirstOrDefault(t => t.FilePath.Contains("PlainClass"));

        Assert.Null(generatedFile);
    }

    private static string RunGenerator(string source, string targetClassName)
    {
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

        var generator = new TransitionGenerator().AsSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);
        var runResult = driver.GetRunResult();

        var generatedFile = runResult.GeneratedTrees
            .FirstOrDefault(t => t.FilePath.Contains(targetClassName));

        Assert.NotNull(generatedFile);
        return generatedFile!.ToString();
    }
}
