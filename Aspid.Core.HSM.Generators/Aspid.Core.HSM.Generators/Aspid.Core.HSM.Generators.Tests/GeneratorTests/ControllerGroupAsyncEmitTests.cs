using System;
using System.Linq;
using System.Collections.Immutable;
using Aspid.Core.HSM.Generators.ControllerGroup;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.GeneratorTests;

public class ControllerGroupAsyncEmitTests
{
    private const string Source = """
        using System.Threading;
        using System.Threading.Tasks;
        using Aspid.Core.HSM;

        namespace Sample;

        public interface IMyController : IController
        {
            void DoWork();
        }

        [AsyncOf(typeof(IMyController))]
        public interface IMyControllerAsync : IController
        {
            ValueTask DoWorkAsync(CancellationToken cancellationToken);
        }

        public sealed class SyncCtrl : IMyController
        {
            public void DoWork() { }
        }

        public sealed class AsyncCtrl : IMyControllerAsync
        {
            public ValueTask DoWorkAsync(CancellationToken cancellationToken) => default;
        }

        [ControllerGroup]
        public sealed partial class MixedGroup
        {
            public MixedGroup()
            {
                AddControllers(new SyncCtrl(), new AsyncCtrl());
            }
        }

        [ControllerGroup]
        public sealed partial class SyncOnlyGroup
        {
            public SyncOnlyGroup()
            {
                AddControllers(new SyncCtrl());
            }
        }
        """;

    [Fact]
    public void Group_with_async_controller_emits_async_method_using_AsyncOf_pair()
    {
        var generated = RunGenerator(Source, "MixedGroup");

        Assert.Contains("global::Sample.IMyControllerAsync", generated);
        Assert.Contains("async global::System.Threading.Tasks.ValueTask", generated);
        Assert.Contains(".DoWorkAsync(cancellationToken)", generated);
        Assert.Contains("await ((global::Sample.IMyControllerAsync)__controller", generated);
        Assert.Contains("((global::Sample.IMyController)__controller", generated);
    }

    [Fact]
    public void Group_with_only_sync_controller_emits_sync_method()
    {
        var generated = RunGenerator(Source, "SyncOnlyGroup");

        Assert.DoesNotContain("async ", generated);
        Assert.DoesNotContain("await ", generated);
        Assert.Contains("void global::Sample.IMyController.DoWork()", generated);
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
