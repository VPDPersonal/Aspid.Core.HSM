# HSM Phase 4: Extension States Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Extension States — dynamic mixin states that attach/detach at runtime, extending host state behavior. They have compatibility constraints via `[ExtensionFor]`, auto-detach on incompatible transitions, and participate in controller dispatch after host states.

**Architecture:** Extension States are managed by `StateMachineBase` in a separate list (`_activeExtensions`). On each Update/LateUpdate/FixedUpdate, host state controllers dispatch first, then extension state controllers in attachment order. During transitions, extensions check `CanAttachTo` against the new leaf state — incompatible ones auto-detach. A new source generator emits `CanAttachTo` from `[ExtensionFor]`.

**Tech Stack:** C# (.NET 10, netstandard2.0 for generator), xUnit, Roslyn, UniTask

---

**Path aliases:**

| Alias | Full Path |
|---|---|
| `$RT` | `Aspid.Core.HSM/Assets/Plugins/Aspid/Core/HSM` |
| `$GEN` | `Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators` |
| `$TESTS` | `Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests` |

Root: `/Users/vladislavpanin/Documents/Docs/Aspid/Packages/Aspid.Core.HSM/Projects/Aspid.Core.HSM/`

---

## File Structure

### Files to create

| File | Responsibility |
|---|---|
| `$RT/Source/IExtensionState.cs` | Interface: CanAttachTo, OnAttached, OnDetached |
| `$RT/Source/Generation/ExtensionForAttribute.cs` | `[ExtensionFor(typeof(State1), typeof(State2))]` |
| `$RT/Unity/Runtime/StateMachines/StateMachineBase.Extensions.cs` | Extension state management (partial) |
| `$GEN/ExtensionState/ExtensionStateGenerator.cs` | Generator for [ExtensionFor] |
| `$GEN/ExtensionState/Data/ExtensionStateData.cs` | Generator data |
| `$GEN/ExtensionState/Bodies/ExtensionStateBody.cs` | Generator emit |
| `$GEN/ExtensionState/Factories/ExtensionStateDataFactory.cs` | Generator factory |
| `$TESTS/StateMachineTests/ExtensionStateTests.cs` | Runtime tests |
| `$TESTS/GeneratorTests/ExtensionStateEmitTests.cs` | Generator tests |

### Files to modify

| File | Change |
|---|---|
| `$RT/Source/IStateMachine.cs` | Add ActiveExtensions, AttachExtension, DetachExtension |
| `$RT/Unity/Runtime/StateMachines/StateMachineBase.cs` | Dispatch extensions in Update/LateUpdate/FixedUpdate |
| `$RT/Unity/Runtime/StateMachines/MonoStateMachine.cs` | Forward extension API |
| `$GEN/Descriptions/HsmClasses.cs` | Add ExtensionForAttribute descriptor |
| `$TESTS/Aspid.Core.HSM.Generators.Tests.csproj` | Link new files |

---

### Task 1: Core types — IExtensionState + ExtensionForAttribute

**Files:**
- Create: `$RT/Source/IExtensionState.cs`
- Create: `$RT/Source/Generation/ExtensionForAttribute.cs`

- [ ] **Step 1: Create IExtensionState.cs**

```csharp
// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public interface IExtensionState : IState
    {
        bool CanAttachTo(IState hostState);
        void OnAttached(IState hostState) { }
        void OnDetached(IState hostState) { }
    }
}
```

- [ ] **Step 2: Create ExtensionForAttribute.cs**

```csharp
using System;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ExtensionForAttribute : Attribute
    {
        public Type[] CompatibleStates { get; }

        public ExtensionForAttribute(params Type[] compatibleStates)
        {
            CompatibleStates = compatibleStates;
        }
    }
}
```

- [ ] **Step 3: Build, commit**

```bash
git commit -m "feat: add IExtensionState interface and ExtensionForAttribute"
```

---

### Task 2: ExtensionStateGenerator

**Files:**
- Create: `$GEN/ExtensionState/ExtensionStateGenerator.cs`
- Create: `$GEN/ExtensionState/Data/ExtensionStateData.cs`
- Create: `$GEN/ExtensionState/Bodies/ExtensionStateBody.cs`
- Create: `$GEN/ExtensionState/Factories/ExtensionStateDataFactory.cs`
- Modify: `$GEN/Descriptions/HsmClasses.cs`

Follow ChildStateGenerator/TransitionGenerator pattern exactly.

The generator processes `[ExtensionFor(typeof(State1), typeof(State2))]` and emits:
```csharp
bool IExtensionState.CanAttachTo(IState hostState) =>
    hostState is global::NS.State1 or global::NS.State2;
```

- [ ] **Step 1-5: Create generator files + update HsmClasses**
- [ ] **Step 6: Build, run tests (49 must pass), commit**

```bash
git commit -m "feat: add ExtensionStateGenerator source generator"
```

---

### Task 3: StateMachineBase extension management + dispatch

**Files:**
- Modify: `$RT/Source/IStateMachine.cs` — add extension API
- Create: `$RT/Unity/Runtime/StateMachines/StateMachineBase.Extensions.cs`
- Modify: `$RT/Unity/Runtime/StateMachines/StateMachineBase.cs` — dispatch extensions in Update loops
- Modify: `$RT/Unity/Runtime/StateMachines/MonoStateMachine.cs` — forward API

This is the core work:

**IStateMachine additions:**
```csharp
IReadOnlyList<IExtensionState> ActiveExtensions { get; }
void AttachExtension<T>() where T : IExtensionState;
void DetachExtension<T>() where T : IExtensionState;
```

**StateMachineBase.Extensions.cs** (new partial):
- `List<IExtensionState> _activeExtensions`
- `AttachExtension<T>()`: check CanAttachTo(current leaf), Enter(), OnAttached(), add to list
- `DetachExtension<T>()`: OnDetached(), Exit(), remove from list
- `AutoDetachIncompatibleExtensions(IState newLeafState)`: called during state change, detaches extensions where CanAttachTo returns false for new state
- Extension state creation via `StateFactory.CreateStateInternal(typeof(T))`

**StateMachineBase.cs modifications:**
- In `Update()`, `LateUpdate()`, `FixedUpdate()`: after iterating `_currentStates`, also iterate `_activeExtensions` and dispatch controllers
- In `ChangeState<T>()` (and async variant): after entering new states, call `AutoDetachIncompatibleExtensions`
- In `Dispose()`: dispose extension states too

**MonoStateMachine.cs:**
- Forward `AttachExtension<T>()`, `DetachExtension<T>()`, `ActiveExtensions`

- [ ] **Steps 1-4: Implement, build, run tests, commit**

```bash
git commit -m "feat: add extension state management and dispatch to StateMachineBase"
```

---

### Task 4: Link files in test csproj + runtime tests

**Files:**
- Modify: `$TESTS/Aspid.Core.HSM.Generators.Tests.csproj`
- Create: `$TESTS/StateMachineTests/ExtensionStateTests.cs`

**Linked files to add:**
- `$RT/Source/IExtensionState.cs`
- `$RT/Source/Generation/ExtensionForAttribute.cs`
- `$RT/Unity/Runtime/StateMachines/StateMachineBase.Extensions.cs`

**Tests:**

1. **`AttachExtension_adds_extension_to_active_list`**
2. **`AttachExtension_calls_Enter_and_OnAttached`**
3. **`AttachExtension_rejected_when_CanAttachTo_returns_false`**
4. **`DetachExtension_calls_OnDetached_and_Exit`**
5. **`Update_dispatches_to_extension_controllers_after_host`**
6. **`Auto_detach_on_incompatible_transition`** — extension detaches when transitioning to incompatible state
7. **`Extension_survives_compatible_transition`** — extension stays when transitioning to compatible state
8. **`Multiple_extensions_dispatch_in_attachment_order`**

Test helpers:
```csharp
public class TestExtensionState : BaseTestState, IExtensionState, IUpdateController
{
    public bool AttachResult { get; set; } = true;
    public IState? AttachedHost { get; private set; }
    public IState? DetachedHost { get; private set; }
    public int UpdateCount { get; private set; }

    public bool CanAttachTo(IState hostState) => AttachResult;
    public void OnAttached(IState hostState) => AttachedHost = hostState;
    public void OnDetached(IState hostState) => DetachedHost = hostState;
    public void Update(float deltaTime) => UpdateCount++;
}
```

- [ ] **Steps 1-3: Link files, write tests, run, commit**

```bash
git commit -m "test: add runtime tests for extension states"
```

---

### Task 5: Generator emit tests

**Files:**
- Create: `$TESTS/GeneratorTests/ExtensionStateEmitTests.cs`

**Tests:**
1. **`ExtensionFor_generates_CanAttachTo_with_pattern_matching`** — verify generated `is State1 or State2`
2. **`ExtensionFor_with_single_state_generates_simple_is_check`**

- [ ] **Steps 1-2: Write tests, run, commit**

```bash
git commit -m "test: add generator emit tests for ExtensionStateGenerator"
```

---

### Task 6: Final verification

- [ ] **Run all tests + build**
