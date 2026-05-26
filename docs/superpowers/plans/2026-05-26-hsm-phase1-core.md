# HSM Phase 1: Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Verify and solidify the core HSM framework — Controller, ControllerGroup (as standalone reusable unit), State, hierarchy via `[ParentState]`, StateMachineBase, MonoStateMachine, and source generators.

**Architecture:** Most of Phase 1 already exists in the codebase. The main gaps are: (1) a generator test proving nested ControllerGroups (ControllerGroup inside ControllerGroup) emit correctly, (2) a runtime test proving nested ControllerGroup dispatch works, (3) sample demonstrating standalone ControllerGroup reuse across states. The existing 68+ tests and two generators (ChildStateGenerator, ControllersGroupGenerator) form the baseline.

**Tech Stack:** C# 12, .NET (netstandard2.0 for generator, net10.0 for tests), xUnit, Roslyn Incremental Source Generators, Unity 2022.3+

---

**Path aliases used throughout this plan:**

| Alias | Full Path |
|---|---|
| `$RUNTIME` | `Aspid.Core.HSM/Assets/Plugins/Aspid/Core/HSM` |
| `$GEN` | `Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators` |
| `$TESTS` | `Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests` |
| `$SAMPLE` | `Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Sample` |

All paths relative to repo root: `/Users/vladislavpanin/Documents/Docs/Aspid/Packages/Aspid.Core.HSM/Projects/Aspid.Core.HSM/`

---

## File Structure

### Existing files (no changes needed)

| File | Responsibility |
|---|---|
| `$RUNTIME/Source/IController.cs` | Marker interface for all controllers |
| `$RUNTIME/Source/IState.cs` | State interface with Enter/Exit |
| `$RUNTIME/Source/IChildState.cs` | Exposes ParentState type for hierarchy |
| `$RUNTIME/Source/IStateMachine.cs` | Public state machine API |
| `$RUNTIME/Source/EmptyState.cs` | Initial state placeholder |
| `$RUNTIME/Source/StateFactory.cs` | Abstract factory, chain builder |
| `$RUNTIME/Source/Extensions/StateExtensions.cs` | GetController helper |
| `$RUNTIME/Source/Extensions/StateMachineExtensions.cs` | GetParentState/GetChildState |
| `$RUNTIME/Source/Generation/ParentStateAttribute.cs` | Triggers ChildStateGenerator |
| `$RUNTIME/Source/Generation/ControllerGroupAttribute.cs` | Triggers ControllersGroupGenerator |
| `$RUNTIME/Source/Generation/ReverseExecuteAttribute.cs` | Marks reverse-order methods |
| `$RUNTIME/Unity/Runtime/Controllers/*.cs` | All 9 controller interfaces |
| `$RUNTIME/Unity/Runtime/StateMachines/StateMachineBase.cs` | Core state machine logic |
| `$RUNTIME/Unity/Runtime/StateMachines/MonoStateMachine.cs` | Unity MonoBehaviour wrapper |
| `$GEN/ChildState/**` | ChildStateGenerator + data + body |
| `$GEN/ControllerGroup/**` | ControllersGroupGenerator + data + factories + body |
| `$TESTS/StateMachineTests/TestStates.cs` | Test state hierarchy |
| `$TESTS/StateMachineTests/TestStateFactory.cs` | Test factory implementation |
| `$TESTS/StateMachineTests/TestableStateMachine.cs` | Testable StateMachineBase subclass |
| `$TESTS/StateMachineTests/StateMachineBaseTests.cs` | 30+ existing tests |

### Files to create

| File | Responsibility |
|---|---|
| `$TESTS/GeneratorTests/ControllerGroupNestingEmitTests.cs` | Verify generator emits correct code for nested ControllerGroups |
| `$TESTS/StateMachineTests/NestedControllerGroupTests.cs` | Verify runtime dispatch through nested controller groups |
| `$SAMPLE/Sample/Controllers/InputControllerGroup.cs` | Standalone ControllerGroup sample (reused across states) |

---

### Task 1: Verify baseline — all tests pass

**Files:**
- Read: `$TESTS/Aspid.Core.HSM.Generators.Tests.csproj`

- [ ] **Step 1: Build the solution**

Run from `Aspid.Core.HSM.Generators/`:
```bash
dotnet build Aspid.Core.HSM.Generators.slnx
```
Expected: Build succeeded.

- [ ] **Step 2: Run all existing tests**

```bash
dotnet test Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests/Aspid.Core.HSM.Generators.Tests.csproj -v normal
```
Expected: All tests pass. If any fail, fix them before proceeding.

- [ ] **Step 3: Commit baseline verification**

No code changes needed — this is just a checkpoint. If fixes were needed:
```bash
git add -A
git commit -m "fix: resolve baseline test failures"
```

---

### Task 2: Generator test — nested ControllerGroup emit

This test verifies that when a `[ControllerGroup]` class aggregates another `[ControllerGroup]` class, the outer group generates correct delegation code.

**Files:**
- Create: `$TESTS/GeneratorTests/ControllerGroupNestingEmitTests.cs`

- [ ] **Step 1: Write the generator emit test**

Create `$TESTS/GeneratorTests/ControllerGroupNestingEmitTests.cs`:

```csharp
using Aspid.Core.HSM.Generators.ControllerGroup;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.GeneratorTests;

public class ControllerGroupNestingEmitTests
{
    [Fact]
    public void Outer_group_delegates_to_inner_group_that_implements_interface()
    {
        var source = """
            using Aspid.Core.HSM;

            namespace Test;

            public interface IMyController : IController
            {
                void DoWork();
            }

            public class LeafController : IMyController
            {
                public void DoWork() { }
            }

            [ControllerGroup]
            public partial class InnerGroup : IMyController
            {
                public InnerGroup()
                {
                    AddControllers(new LeafController());
                }
            }

            [ControllerGroup]
            public partial class OuterGroup : IMyController
            {
                public OuterGroup()
                {
                    AddControllers(new InnerGroup());
                }
            }
            """;

        var generated = RunGenerator(source, "OuterGroup");

        Assert.Contains("void IMyController.DoWork()", generated);
        Assert.Contains("((IMyController)__controller0).DoWork()", generated);
    }

    [Fact]
    public void Outer_group_with_mixed_inner_group_and_leaf_delegates_to_both()
    {
        var source = """
            using Aspid.Core.HSM;

            namespace Test;

            public interface IMyController : IController
            {
                void DoWork();
            }

            public class LeafController : IMyController
            {
                public void DoWork() { }
            }

            [ControllerGroup]
            public partial class InnerGroup : IMyController
            {
                public InnerGroup()
                {
                    AddControllers(new LeafController());
                }
            }

            [ControllerGroup]
            public partial class OuterGroup : IMyController
            {
                public OuterGroup()
                {
                    AddControllers(new InnerGroup(), new LeafController());
                }
            }
            """;

        var generated = RunGenerator(source, "OuterGroup");

        Assert.Contains("((IMyController)__controller0).DoWork()", generated);
        Assert.Contains("((IMyController)__controller1).DoWork()", generated);
    }

    private static string RunGenerator(string source, string targetClassName)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .ToList();

        var compilation = CSharpCompilation.Create("TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new ControllersGroupGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

        var results = driver.GetRunResult();
        var targetResult = results.GeneratedTrees
            .FirstOrDefault(t => t.FilePath.Contains(targetClassName));

        Assert.NotNull(targetResult);
        return targetResult!.GetText().ToString();
    }
}
```

- [ ] **Step 2: Run test to verify it passes**

```bash
dotnet test Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests/Aspid.Core.HSM.Generators.Tests.csproj --filter FullyQualifiedName~ControllerGroupNestingEmitTests -v normal
```
Expected: PASS. The generator already handles nested groups because it treats any `[ControllerGroup]`-decorated class the same as a regular controller — it just needs to implement the interface in its type declaration.

If the test FAILS (e.g. generator doesn't see InnerGroup as implementing IMyController), the fix is in `ControllerInterfaceDataFactory.Create()` — ensure it resolves interfaces from the semantic model which includes generated partial declarations.

- [ ] **Step 3: Commit**

```bash
git add Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests/GeneratorTests/ControllerGroupNestingEmitTests.cs
git commit -m "test: add generator emit tests for nested ControllerGroups"
```

---

### Task 3: Runtime test — nested ControllerGroup dispatch

This test verifies that at runtime, dispatching Update/Enter through a state that contains a nested ControllerGroup calls all leaf controllers.

**Files:**
- Create: `$TESTS/StateMachineTests/NestedControllerGroupTests.cs`

- [ ] **Step 1: Write the runtime dispatch test**

Create `$TESTS/StateMachineTests/NestedControllerGroupTests.cs`:

```csharp
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

public class InnerGroupController : IEnterController, IUpdateController
{
    public int OnEnterCalled { get; private set; }
    public int UpdateCallCount { get; private set; }
    public float LastDeltaTime { get; private set; }

    public void OnEnter() => OnEnterCalled++;

    public void Update(float deltaTime)
    {
        UpdateCallCount++;
        LastDeltaTime = deltaTime;
    }
}

public class OuterGroupState : BaseTestState, IEnterController, IUpdateController
{
    public InnerGroupController Inner { get; } = new();

    public void OnEnter() => Inner.OnEnter();

    public void Update(float deltaTime) => Inner.Update(deltaTime);
}

public class NestedControllerGroupTests
{
    [Fact]
    public void Update_dispatches_through_nested_controller_group()
    {
        var factory = new TestStateFactory();
        var state = new OuterGroupState();
        factory.RegisterState(() => state);
        var sm = new TestableStateMachine(factory);

        sm.ChangeState<OuterGroupState>();
        sm.CallUpdate(0.016f);

        Assert.Equal(1, state.Inner.UpdateCallCount);
        Assert.Equal(0.016f, state.Inner.LastDeltaTime);
    }

    [Fact]
    public void Enter_dispatches_through_nested_controller_group()
    {
        var factory = new TestStateFactory();
        var state = new OuterGroupState();
        factory.RegisterState(() => state);
        var sm = new TestableStateMachine(factory);

        sm.ChangeState<OuterGroupState>();

        Assert.Equal(1, state.Inner.OnEnterCalled);
    }

    [Fact]
    public void Multiple_updates_dispatch_correctly_through_nesting()
    {
        var factory = new TestStateFactory();
        var state = new OuterGroupState();
        factory.RegisterState(() => state);
        var sm = new TestableStateMachine(factory);

        sm.ChangeState<OuterGroupState>();
        sm.CallUpdate(0.016f);
        sm.CallUpdate(0.033f);
        sm.CallUpdate(0.016f);

        Assert.Equal(3, state.Inner.UpdateCallCount);
        Assert.Equal(0.016f, state.Inner.LastDeltaTime);
    }
}
```

- [ ] **Step 2: Run tests to verify they pass**

```bash
dotnet test Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests/Aspid.Core.HSM.Generators.Tests.csproj --filter FullyQualifiedName~NestedControllerGroupTests -v normal
```
Expected: All 3 tests PASS.

- [ ] **Step 3: Commit**

```bash
git add Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests/StateMachineTests/NestedControllerGroupTests.cs
git commit -m "test: add runtime dispatch tests for nested ControllerGroups"
```

---

### Task 4: Sample — standalone ControllerGroup reused across states

Add a sample showing a standalone `InputControllerGroup` used in multiple states (spec section 1.3 example).

**Files:**
- Create: `$SAMPLE/Sample/Controllers/InputControllerGroup.cs`
- Modify: `$SAMPLE/Sample/States/Singleplayers/SingleplayerState.cs`
- Modify: `$SAMPLE/Sample/States/Multiplayers/MultiplayerState.cs`

- [ ] **Step 1: Create InputControllerGroup**

Create `$SAMPLE/Sample/Controllers/InputControllerGroup.cs`:

```csharp
using Aspid.Core.HSM;

namespace Aspid.Core.HSM.Generators.Sample.Sample;

[ControllerGroup]
public partial class InputControllerGroup : IEnterController, IUpdateController
{
    public InputControllerGroup()
    {
        AddControllers(new SomeUpdateController(), new SomeUpdateController());
    }
}
```

- [ ] **Step 2: Update SingleplayerState to use InputControllerGroup**

In `$SAMPLE/Sample/States/Singleplayers/SingleplayerState.cs`, update the constructor:

```csharp
using Aspid.Core.HSM;

namespace Aspid.Core.HSM.Generators.Sample.Sample;

[ControllerGroup]
public partial class SingleplayerState : IState
{
    public SingleplayerState()
    {
        AddControllers(new PlayerController(), new InputControllerGroup());
    }
}
```

- [ ] **Step 3: Update MultiplayerState to use InputControllerGroup**

In `$SAMPLE/Sample/States/Multiplayers/MultiplayerState.cs`, update the constructor:

```csharp
using Aspid.Core.HSM;

namespace Aspid.Core.HSM.Generators.Sample.Sample;

[ControllerGroup]
public partial class MultiplayerState : IState
{
    public MultiplayerState()
    {
        AddControllers(new PlayerController(), new SomeUpdateController(), new InputControllerGroup());
    }

    public void Enter()
    {
    }

    public void Exit()
    {
    }
}
```

- [ ] **Step 4: Build to verify generator handles the samples**

```bash
dotnet build Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.slnx
```
Expected: Build succeeded. The generator should produce correct code for both states that now include `InputControllerGroup`.

- [ ] **Step 5: Commit**

```bash
git add Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Sample/Sample/Controllers/InputControllerGroup.cs
git add Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Sample/Sample/States/Singleplayers/SingleplayerState.cs
git add Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Sample/Sample/States/Multiplayers/MultiplayerState.cs
git commit -m "feat: add InputControllerGroup sample showing reusable group across states"
```

---

### Task 5: Final verification — all tests pass

**Files:** None (verification only)

- [ ] **Step 1: Run full test suite**

```bash
dotnet test Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests/Aspid.Core.HSM.Generators.Tests.csproj -v normal
```
Expected: All tests pass (existing 68+ plus the new ones from Tasks 2 and 3).

- [ ] **Step 2: Build full solution**

```bash
dotnet build Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.slnx
```
Expected: Clean build, no warnings from our code.

---

## Phase 2–7 Outlines

### Phase 2: AsyncMode Per-Interface

**What exists:** `IAsyncEnterController`, `IAsyncExitController`, `[AsyncOf]`, `ChangeStateAsync`, generator handles mixed sync/async dispatch.

**What's new:**
1. `AsyncModeAttribute` — new attribute: `[AsyncMode(typeof(IAsyncEnterController), AsyncExecutionMode.Sequential)]`
2. `AsyncExecutionMode` enum — `Sequential`, `Parallel`
3. Generator update — `ControllerGroupBody.AppendAsyncInterfaceMethod()` reads `[AsyncMode]` from the class and emits either sequential `await` or `UniTask.WhenAll()`
4. Generator test — verify Sequential vs Parallel emit
5. Runtime test — verify Parallel mode dispatches concurrently

**Key files:**
- Create: `$RUNTIME/Source/Generation/AsyncModeAttribute.cs`
- Create: `$RUNTIME/Source/Generation/AsyncExecutionMode.cs`
- Modify: `$GEN/ControllerGroup/Factories/ControllerInterfaceDataFactory.cs` — read `[AsyncMode]` attribute
- Modify: `$GEN/ControllerGroup/Bodies/ControllerGroupBody.cs` — conditional Sequential/Parallel emit
- Create: `$TESTS/GeneratorTests/AsyncModeEmitTests.cs`
- Create: `$TESTS/StateMachineTests/AsyncModeRuntimeTests.cs`

---

### Phase 3: Transition Pipeline

**What exists:** Nothing — this is entirely new.

**What's new:**
1. `ITransition` / `ITransition<TSource, TTarget>` interfaces (Source/Target types, `CanTransition()` guard)
2. `TransitionAttribute` — `[Transition(typeof(Source), typeof(Target))]`
3. `TransitionGenerator` — generates `ITransition.SourceState` / `TargetState` properties
4. `TransitionRegistry` — holds registered transitions, keyed by (source, target)
5. `StateMachineBase` update — `TransitionTo<T>()` and `TransitionVia<T>()` methods, pipeline execution
6. Transition resolution — direct lookup then chain fallback with guard pre-check
7. `TransitionState` — runs in separate lightweight execution context
8. Async pipeline — `OnBeforeTransitionAsync`, exit source, enter target, `OnAfterTransitionAsync`
9. `MonoStateMachine` update — expose `TransitionTo`/`TransitionVia`
10. Backward compatibility — keep `ChangeState<T>()` as low-level API, `TransitionTo<T>()` uses pipeline

**Key files:**
- Create: `$RUNTIME/Source/ITransition.cs`
- Create: `$RUNTIME/Source/Generation/TransitionAttribute.cs`
- Create: `$GEN/Transition/TransitionGenerator.cs` (+ Data, Body, Factory)
- Modify: `$RUNTIME/Unity/Runtime/StateMachines/StateMachineBase.cs` — add transition registry, pipeline execution
- Modify: `$RUNTIME/Source/IStateMachine.cs` — add `TransitionTo`, `TransitionVia`, `IsTransitioning`
- Create: `$TESTS/StateMachineTests/TransitionPipelineTests.cs`
- Create: `$TESTS/GeneratorTests/TransitionEmitTests.cs`

---

### Phase 4: Extension States

**What exists:** Nothing — this is entirely new.

**What's new:**
1. `IExtensionState` interface — `CanAttachTo`, `OnAttached`, `OnDetached`
2. `ExtensionForAttribute` — `[ExtensionFor(typeof(State1), typeof(State2))]`
3. `ExtensionStateGenerator` — generates `CanAttachTo` implementation
4. `StateMachineBase` update — `AttachExtension<T>()`, `DetachExtension<T>()`, `ActiveExtensions` list
5. Auto-detach on incompatible transition
6. Controller dispatch order — host states first, then extensions in attachment order

**Key files:**
- Create: `$RUNTIME/Source/IExtensionState.cs`
- Create: `$RUNTIME/Source/Generation/ExtensionForAttribute.cs`
- Create: `$GEN/ExtensionState/ExtensionStateGenerator.cs` (+ Data, Body, Factory)
- Modify: `$RUNTIME/Unity/Runtime/StateMachines/StateMachineBase.cs` — extension management
- Modify: `$RUNTIME/Source/IStateMachine.cs` — add extension API
- Create: `$TESTS/StateMachineTests/ExtensionStateTests.cs`
- Create: `$TESTS/GeneratorTests/ExtensionStateEmitTests.cs`

---

### Phase 5: Scope + VContainer

**What exists:** `StateFactory` with `CreateStateInternal` / `Release` / `MarkInitialized`.

**What's new:**
1. `IStateScope` interface in core — `Parent`, `CreateChildScope()`, `IDisposable`
2. `ScopeLifetimeAttribute` — `[ScopeLifetime(ScopeLifetime.Cached)]`
3. `ScopeLifetime` enum — `Transient`, `Cached`
4. `StateFactory` update — scope creation/caching integrated into `CreateState`
5. Separate package `Aspid.Core.HSM.VContainer` — `VContainerStateFactory`, `VContainerStateScope`
6. Extension State scopes — child of host state scope

**Key files:**
- Create: `$RUNTIME/Source/IStateScope.cs`
- Create: `$RUNTIME/Source/Generation/ScopeLifetimeAttribute.cs`
- Modify: `$RUNTIME/Source/StateFactory.cs` — scope awareness
- Create new package: `Aspid.Core.HSM.VContainer/`
- Create: `$TESTS/StateMachineTests/ScopeLifecycleTests.cs`

---

### Phase 6: Integrations and Polish

1. `Aspid.Core.HSM.MVVM` package — `ViewLifecycleController<TView, TViewModel>`
2. Compile-time diagnostics in generators (cyclic hierarchy, invalid `[ParentState]` refs)
3. Extension points stubs — `IsControllerEnabled()`, `IsStateEnabled()`, `ResolveTransition()`
4. Updated samples and documentation

---

### Phase 7: Claude Tooling (Separate Spec)

Separate design spec covering:
1. Claude Code skills for HSM scaffolding
2. MCP server for HSM tree introspection
3. Agents for FSM→HSM migration
4. XML-docs and CLAUDE.md guidelines for consumer projects
