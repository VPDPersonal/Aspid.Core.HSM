using System;
using System.Linq;
using System.Collections.Immutable;
using Aspid.Core.HSM.Generators.ControllerGroup;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.GeneratorTests;

public class AsyncModeEmitTests
{
    [Fact]
    public void Parallel_mode_emits_WhenAll_for_async_controllers()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using Aspid.Core.HSM;

            namespace Sample;

            public interface IMyController : IController
            {
                void DoWork();
            }

            [AsyncOf(typeof(IMyController))]
            public interface IMyAsyncController : IController
            {
                ValueTask DoWorkAsync(CancellationToken cancellationToken);
            }

            public sealed class AsyncCtrlA : IMyAsyncController
            {
                public ValueTask DoWorkAsync(CancellationToken cancellationToken) => default;
            }

            public sealed class AsyncCtrlB : IMyAsyncController
            {
                public ValueTask DoWorkAsync(CancellationToken cancellationToken) => default;
            }

            [ControllerGroup]
            [AsyncMode(typeof(IMyAsyncController), AsyncExecutionMode.Parallel)]
            public sealed partial class ParallelGroup
            {
                public ParallelGroup()
                {
                    AddControllers(new AsyncCtrlA(), new AsyncCtrlB());
                }
            }
            """;

        var generated = RunGenerator(source, "ParallelGroup");

        Assert.Contains("Cysharp.Threading.Tasks.UniTask.WhenAll(", generated);
    }

    [Fact]
    public void Sequential_mode_emits_sequential_await_for_async_controllers()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using Aspid.Core.HSM;

            namespace Sample;

            public interface IMyController : IController
            {
                void DoWork();
            }

            [AsyncOf(typeof(IMyController))]
            public interface IMyAsyncController : IController
            {
                ValueTask DoWorkAsync(CancellationToken cancellationToken);
            }

            public sealed class AsyncCtrlA : IMyAsyncController
            {
                public ValueTask DoWorkAsync(CancellationToken cancellationToken) => default;
            }

            public sealed class AsyncCtrlB : IMyAsyncController
            {
                public ValueTask DoWorkAsync(CancellationToken cancellationToken) => default;
            }

            [ControllerGroup]
            [AsyncMode(typeof(IMyAsyncController), AsyncExecutionMode.Sequential)]
            public sealed partial class SequentialGroup
            {
                public SequentialGroup()
                {
                    AddControllers(new AsyncCtrlA(), new AsyncCtrlB());
                }
            }
            """;

        var generated = RunGenerator(source, "SequentialGroup");

        Assert.DoesNotContain("WhenAll", generated);
        Assert.Contains("await ((global::Sample.IMyAsyncController)__controller0).DoWorkAsync(cancellationToken);", generated);
        Assert.Contains("await ((global::Sample.IMyAsyncController)__controller1).DoWorkAsync(cancellationToken);", generated);
    }

    [Fact]
    public void Default_mode_is_Parallel_when_no_AsyncMode_attribute()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using Aspid.Core.HSM;

            namespace Sample;

            public interface IMyController : IController
            {
                void DoWork();
            }

            [AsyncOf(typeof(IMyController))]
            public interface IMyAsyncController : IController
            {
                ValueTask DoWorkAsync(CancellationToken cancellationToken);
            }

            public sealed class AsyncCtrlA : IMyAsyncController
            {
                public ValueTask DoWorkAsync(CancellationToken cancellationToken) => default;
            }

            public sealed class AsyncCtrlB : IMyAsyncController
            {
                public ValueTask DoWorkAsync(CancellationToken cancellationToken) => default;
            }

            [ControllerGroup]
            public sealed partial class DefaultModeGroup
            {
                public DefaultModeGroup()
                {
                    AddControllers(new AsyncCtrlA(), new AsyncCtrlB());
                }
            }
            """;

        var generated = RunGenerator(source, "DefaultModeGroup");

        Assert.Contains("Cysharp.Threading.Tasks.UniTask.WhenAll(", generated);
    }

    [Fact]
    public void Mixed_sync_async_in_Parallel_mode_calls_sync_before_WhenAll()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using Aspid.Core.HSM;

            namespace Sample;

            public interface IMyController : IController
            {
                void DoWork();
            }

            [AsyncOf(typeof(IMyController))]
            public interface IMyAsyncController : IController
            {
                ValueTask DoWorkAsync(CancellationToken cancellationToken);
            }

            public sealed class SyncCtrl : IMyController
            {
                public void DoWork() { }
            }

            public sealed class AsyncCtrlA : IMyAsyncController
            {
                public ValueTask DoWorkAsync(CancellationToken cancellationToken) => default;
            }

            public sealed class AsyncCtrlB : IMyAsyncController
            {
                public ValueTask DoWorkAsync(CancellationToken cancellationToken) => default;
            }

            [ControllerGroup]
            [AsyncMode(typeof(IMyAsyncController), AsyncExecutionMode.Parallel)]
            public sealed partial class MixedParallelGroup
            {
                public MixedParallelGroup()
                {
                    AddControllers(new SyncCtrl(), new AsyncCtrlA(), new AsyncCtrlB());
                }
            }
            """;

        var generated = RunGenerator(source, "MixedParallelGroup");

        // Sync controller called without await
        Assert.Contains("((global::Sample.IMyController)__controller0).DoWork();", generated);
        Assert.DoesNotContain("await ((global::Sample.IMyController)", generated);

        // Async controllers called via WhenAll
        Assert.Contains("Cysharp.Threading.Tasks.UniTask.WhenAll(", generated);
        Assert.Contains("((global::Sample.IMyAsyncController)__controller1).DoWorkAsync(cancellationToken)", generated);
        Assert.Contains("((global::Sample.IMyAsyncController)__controller2).DoWorkAsync(cancellationToken)", generated);
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
