---
name: gen-snapshot-test
description: Add an xUnit test for the output of ControllersGroupGenerator, TransitionGenerator or ExtensionStateGenerator. Use when adding a new emit branch, fixing generated code, or verifying that an attribute combination produces the expected partial class. Tests live in Aspid.Core.HSM.Generators.Tests/GeneratorTests/ and run the generator with CSharpGeneratorDriver.
---

# gen-snapshot-test

Write a focused test that runs one incremental generator on a sample input class and asserts on the generated source text.

## Where the test goes

`Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests/GeneratorTests/`, one file per generator or feature: `TransitionEmitTests.cs`, `ExtensionStateEmitTests.cs`, `ControllerGroup*EmitTests.cs`, `AsyncModeEmitTests.cs`. State machine runtime tests go to `StateMachineTests/`.

## Generators

| Attribute | Generator | Namespace |
|---|---|---|
| `[ControllerGroup]` | `ControllersGroupGenerator` | `Aspid.Core.HSM.Generators.ControllerGroup` |
| `[Transition(typeof(A), typeof(B))]` | `TransitionGenerator` | `Aspid.Core.HSM.Generators.Transition` |
| `[ExtensionFor(typeof(A), …)]` | `ExtensionStateGenerator` | `Aspid.Core.HSM.Generators.ExtensionState` |

There is no `ChildStateGenerator`: `IChildState<TParent>` gives the parent through a default interface member.

## Pattern

The input source declares the HSM types the generator looks up (through `HsmClasses` / `HsmNamespaces`) as small stubs in `namespace Aspid.Core.HSM`. Then the test does not depend on the runtime files that the Tests project also compiles. Copy `RunGenerator` from `TransitionEmitTests.cs` and change the generator type.

```csharp
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
                public interface ITransition { Type SourceState { get; } Type TargetState { get; } }

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
    }

    private static string RunGenerator(string source, string targetClassName)
    {
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToImmutableArray();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new TransitionGenerator().AsSourceGenerator());
        driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);

        var generatedFile = driver.GetRunResult().GeneratedTrees
            .FirstOrDefault(t => t.FilePath.Contains(targetClassName));

        Assert.NotNull(generatedFile);
        return generatedFile!.ToString();
    }
}
```

## Tips

- A generator skips a class that is not `partial`. Add a negative test that asserts no generated tree for it.
- For `ControllersGroupGenerator`, cover the `[ReverseExecute]`, `[AsyncOf]` and `[AsyncMode]` combinations on group methods: they drive different emit branches in `ControllerGroupBody`.
- Equality of the incremental records (`ControllerGroupData`, `TransitionData`, `ExtensionStateData`) matters for caching. If you add a field, add it to the record equality too.
- Run the tests with `dotnet test Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests` from the repository root.
