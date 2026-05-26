# HSM Phase 6: Integrations and Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add extension points for future configurability (IsControllerEnabled, IsStateEnabled) and compile-time diagnostics in generators (cyclic hierarchy, invalid parent refs). MVVM package deferred to integration phase.

**Architecture:** Extension points are virtual methods on StateMachineBase that gate controller dispatch and state transitions — defaulting to `true`. Diagnostics are Roslyn `DiagnosticDescriptor`s reported during generation when structural errors are detected.

**Tech Stack:** C# (.NET 10, netstandard2.0 for generator), xUnit, Roslyn

---

**Path aliases:**

| Alias | Full Path |
|---|---|
| `$RT` | `Aspid.Core.HSM/Assets/Plugins/Aspid/Core/HSM` |
| `$GEN` | `Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators` |
| `$TESTS` | `Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests` |

Root: `/Users/vladislavpanin/Documents/Docs/Aspid/Packages/Aspid.Core.HSM/Projects/Aspid.Core.HSM/`

---

### Task 1: Extension points — IsControllerEnabled, IsStateEnabled

**Files to modify:**
- `$RT/Unity/Runtime/StateMachines/StateMachineBase.cs`

Add virtual methods and integrate into dispatch:

```csharp
protected virtual bool IsControllerEnabled(IController controller, IState state) => true;
protected virtual bool IsStateEnabled(Type stateType) => true;
```

**Integration points:**
- `Update()` / `LateUpdate()` / `FixedUpdate()`: check `IsControllerEnabled` before dispatching
- `ChangeState<T>()`: check `IsStateEnabled(typeof(T))` at the start, return early if false
- Same in `TransitionTo<T>()`

These are stubs — they always return true. But subclasses can override them to implement config-driven behavior.

Build, run existing tests (must all pass), commit: `feat: add IsControllerEnabled and IsStateEnabled extension points`

---

### Task 2: Compile-time diagnostics in ChildStateGenerator

**Files to modify:**
- `$GEN/ChildState/ChildStateGenerator.cs`

**Files to create:**
- `$GEN/Diagnostics/HsmDiagnostics.cs` — shared diagnostic descriptors

Add diagnostic for cyclic parent hierarchy:
- When processing `[ParentState(typeof(Parent))]`, walk the parent chain
- If we encounter the same type twice → report `HSM001: Cyclic state hierarchy detected`

```csharp
public static class HsmDiagnostics
{
    public static readonly DiagnosticDescriptor CyclicHierarchy = new(
        id: "HSM001",
        title: "Cyclic state hierarchy",
        messageFormat: "State '{0}' creates a cyclic hierarchy through '{1}'",
        category: "Aspid.Core.HSM",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
```

Build, run tests, commit: `feat: add compile-time diagnostic for cyclic state hierarchy`

---

### Task 3: Tests for extension points and diagnostics

**Files to create:**
- `$TESTS/StateMachineTests/ExtensionPointTests.cs` — runtime tests
- `$TESTS/GeneratorTests/DiagnosticTests.cs` — generator diagnostic tests

**Extension point tests:**
1. **`IsControllerEnabled_false_skips_update_dispatch`** — override to return false, verify controller not called
2. **`IsStateEnabled_false_prevents_state_change`** — override to return false, verify state doesn't change

**Diagnostic tests:**
1. **`Cyclic_hierarchy_reports_HSM001`** — `[ParentState(typeof(B))]` on A, `[ParentState(typeof(A))]` on B → diagnostic reported

Build, run all tests, commit: `test: add tests for extension points and diagnostics`

---

### Task 4: Final verification

Run all tests + build.
