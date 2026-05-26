using System;
using System.Linq;
using System.Collections.Immutable;
using Aspid.Core.HSM.Generators.ExtensionState;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.GeneratorTests;

public class ExtensionStateEmitTests
{
    [Fact]
    public void ExtensionFor_generates_CanAttachTo_with_pattern_matching()
    {
        const string source = """
            using System;

            namespace Aspid.Core.HSM
            {
                public interface IState { void Enter() { } void Exit() { } }

                public interface IExtensionState : IState
                {
                    bool CanAttachTo(IState hostState);
                }

                [AttributeUsage(AttributeTargets.Class)]
                public sealed class ExtensionForAttribute : Attribute
                {
                    public ExtensionForAttribute(params Type[] compatibleStates) { }
                }
            }

            namespace Test
            {
                using Aspid.Core.HSM;

                public class StateA : IState { }
                public class StateB : IState { }

                [ExtensionFor(typeof(StateA), typeof(StateB))]
                public partial class MyExtension : IExtensionState { }
            }
            """;

        var generated = RunGenerator(source, "MyExtension");

        Assert.Contains("hostState is global::Test.StateA or global::Test.StateB", generated);
    }

    [Fact]
    public void ExtensionFor_with_single_state_generates_simple_is_check()
    {
        const string source = """
            using System;

            namespace Aspid.Core.HSM
            {
                public interface IState { void Enter() { } void Exit() { } }

                public interface IExtensionState : IState
                {
                    bool CanAttachTo(IState hostState);
                }

                [AttributeUsage(AttributeTargets.Class)]
                public sealed class ExtensionForAttribute : Attribute
                {
                    public ExtensionForAttribute(params Type[] compatibleStates) { }
                }
            }

            namespace Test
            {
                using Aspid.Core.HSM;

                public class StateA : IState { }

                [ExtensionFor(typeof(StateA))]
                public partial class MyExtension : IExtensionState { }
            }
            """;

        var generated = RunGenerator(source, "MyExtension");

        Assert.Contains("hostState is global::Test.StateA", generated);
        Assert.DoesNotContain(" or ", generated);
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

        var generator = new ExtensionStateGenerator().AsSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);
        var runResult = driver.GetRunResult();

        var generatedFile = runResult.GeneratedTrees
            .FirstOrDefault(t => t.FilePath.Contains(targetClassName));

        Assert.NotNull(generatedFile);
        return generatedFile!.ToString();
    }
}
