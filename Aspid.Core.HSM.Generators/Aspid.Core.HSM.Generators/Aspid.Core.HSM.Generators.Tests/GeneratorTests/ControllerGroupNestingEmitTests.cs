using System;
using System.Linq;
using System.Collections.Immutable;
using Aspid.Core.HSM.Generators.ControllerGroup;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.GeneratorTests;

public class ControllerGroupNestingEmitTests
{
    private const string Source = """
        using Aspid.Core.HSM;

        namespace Sample;

        public interface IMyController : IController
        {
            void DoWork();
        }

        public sealed class LeafController : IMyController
        {
            public void DoWork() { }
        }

        [ControllerGroup]
        public sealed partial class InnerGroup : IMyController
        {
            public InnerGroup()
            {
                AddControllers(new LeafController());
            }
        }

        [ControllerGroup]
        public sealed partial class OuterGroup : IMyController
        {
            public OuterGroup()
            {
                AddControllers(new InnerGroup());
            }
        }

        [ControllerGroup]
        public sealed partial class MixedOuterGroup : IMyController
        {
            public MixedOuterGroup()
            {
                AddControllers(new InnerGroup(), new LeafController());
            }
        }
        """;

    [Fact]
    public void Outer_group_delegates_to_inner_group_that_implements_interface()
    {
        var generated = RunGenerator(Source, "OuterGroup");

        Assert.Contains("void global::Sample.IMyController.DoWork()", generated);
        Assert.Contains("((global::Sample.IMyController)__controller0).DoWork();", generated);
    }

    [Fact]
    public void Outer_group_with_mixed_inner_group_and_leaf_delegates_to_both()
    {
        var generated = RunGenerator(Source, "MixedOuterGroup");

        Assert.Contains("void global::Sample.IMyController.DoWork()", generated);
        Assert.Contains("((global::Sample.IMyController)__controller0).DoWork();", generated);
        Assert.Contains("((global::Sample.IMyController)__controller1).DoWork();", generated);
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

        var generator = new ControllersGroupGenerator().AsSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);
        var runResult = driver.GetRunResult();

        var generatedFile = runResult.GeneratedTrees
            .FirstOrDefault(t => t.FilePath.Contains(targetClassName));

        Assert.NotNull(generatedFile);
        return generatedFile!.ToString();
    }
}
