---
name: gen-snapshot-test
description: Add an xUnit snapshot-style test for ChildStateGenerator or ControllersGroupGenerator output. Use when adding a new emit branch, fixing generated code, or verifying that an attribute combination produces the expected partial class. Tests live in Aspid.Core.HSM.Generators.Tests/ and use Microsoft.CodeAnalysis.CSharp.SourceGenerators.Testing.XUnit (already referenced).
---

# gen-snapshot-test

Generate a focused test that drives one of the incremental generators with a sample input class and asserts on the produced source text.

## Where the test goes

`Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests/` — keep state-machine runtime tests under `StateMachineTests/` and put generator tests under a new `GeneratorTests/` subfolder.

## Pattern

Use `CSharpGeneratorTest<TGenerator, XUnitVerifier>` (from `Microsoft.CodeAnalysis.CSharp.SourceGenerators.Testing.XUnit`). Required references for the test compilation must include the runtime types the generator looks up via `HsmClasses`/`HsmNamespaces` — `IState`, `IChildState`, `IController`, `ParentStateAttribute`, `ControllerGroupAttribute`, `ReverseExecuteAttribute`. The test csproj already links those via `<Compile Include="..\..\..\Aspid.Core.HSM\Assets\..." />`, so they are available as in-test source rather than a packaged reference.

## Skeleton

```csharp
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Xunit;
using Aspid.Core.HSM.Generators.ChildState;

public class ChildStateGeneratorTests
{
    [Fact]
    public async Task ParentState_attribute_emits_IChildState_implementation()
    {
        const string input = """
            using Aspid.Core.HSM;
            namespace Sample;

            public partial class Root : IState { }

            [ParentState(typeof(Root))]
            public partial class Leaf : IState { }
            """;

        const string expected = /* paste generated source here */;

        var test = new CSharpSourceGeneratorTest<ChildStateGenerator, XUnitVerifier>
        {
            TestState =
            {
                Sources = { input },
                GeneratedSources =
                {
                    (typeof(ChildStateGenerator), "Leaf.g.cs", expected),
                },
            },
        };

        await test.RunAsync();
    }
}
```

## Tips

- The generator skips classes that are not `partial` or that are `static`. Add a negative test for each.
- `[ParentState(null)]` is the root-state path; cover it separately from the `typeof(Parent)` branch.
- For `ControllersGroupGenerator`, exercise `[ReverseExecuteAttribute]` and `[AsyncAttribute]` combinations on group methods — those drive different emit branches in `ControllerGroupBody`.
- Equality of incremental pipeline records (`ChildStateData`, `ControllerGroupData`) matters for caching; if you add fields, add a test that two equal inputs produce a single `Transform` invocation.
