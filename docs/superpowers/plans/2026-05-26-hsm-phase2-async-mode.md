# HSM Phase 2: AsyncMode Per-Interface Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `[AsyncMode]` attribute that configures async dispatch mode (Sequential or Parallel) per controller interface on a ControllerGroup class. Sequential awaits controllers one by one; Parallel uses `UniTask.WhenAll`. Default is Parallel.

**Architecture:** The async infrastructure already exists — `IAsyncEnterController`, `IAsyncExitController`, `[AsyncOf]` pairing, `ChangeStateAsync`, mixed sync/async dispatch in the generator. Phase 2 adds one new concept: the *execution mode* per interface. This requires a new attribute (`AsyncModeAttribute`), a new enum (`AsyncExecutionMode`), modifications to the generator's data model and body emitter, and new tests.

**Tech Stack:** C# (.NET 10, netstandard2.0 for generator), xUnit, Roslyn Incremental Source Generators, UniTask

---

**Path aliases:**

| Alias | Full Path |
|---|---|
| `$RUNTIME` | `Aspid.Core.HSM/Assets/Plugins/Aspid/Core/HSM` |
| `$GEN` | `Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators` |
| `$TESTS` | `Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests` |

All paths relative to repo root: `/Users/vladislavpanin/Documents/Docs/Aspid/Packages/Aspid.Core.HSM/Projects/Aspid.Core.HSM/`

---

## File Structure

### Files to create

| File | Responsibility |
|---|---|
| `$RUNTIME/Source/Generation/AsyncExecutionMode.cs` | Enum: `Sequential`, `Parallel` |
| `$RUNTIME/Source/Generation/AsyncModeAttribute.cs` | Attribute: `[AsyncMode(typeof(IAsyncEnterController), AsyncExecutionMode.Sequential)]` |
| `$TESTS/GeneratorTests/AsyncModeEmitTests.cs` | Generator tests for Sequential vs Parallel code emit |

### Files to modify

| File | Change |
|---|---|
| `$GEN/Descriptions/HsmClasses.cs` | Add `AsyncModeAttribute` type descriptor |
| `$GEN/ControllerGroup/Data/Interfaces/ControllerInterfaceData.cs` | Add `AsyncExecutionMode Mode` field |
| `$GEN/ControllerGroup/Factories/ControllerInterfaceDataFactory.cs` | Read `[AsyncMode]` from the class and set mode on interface data |
| `$GEN/ControllerGroup/Bodies/ControllerGroupBody.cs` | Emit `WhenAll` for Parallel, sequential `await` for Sequential |
| `$TESTS/Aspid.Core.HSM.Generators.Tests.csproj` | Add linked compile for new attribute/enum files |

---

### Task 1: Create AsyncExecutionMode enum and AsyncModeAttribute

**Files:**
- Create: `$RUNTIME/Source/Generation/AsyncExecutionMode.cs`
- Create: `$RUNTIME/Source/Generation/AsyncModeAttribute.cs`

- [ ] **Step 1: Create AsyncExecutionMode enum**

Create `$RUNTIME/Source/Generation/AsyncExecutionMode.cs`:

```csharp
// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public enum AsyncExecutionMode
    {
        Parallel,
        Sequential
    }
}
```

Note: `Parallel` is first (default value = 0) because the spec says default is Parallel.

- [ ] **Step 2: Create AsyncModeAttribute**

Create `$RUNTIME/Source/Generation/AsyncModeAttribute.cs`:

```csharp
using System;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class AsyncModeAttribute : Attribute
    {
        public Type AsyncInterface { get; }
        public AsyncExecutionMode Mode { get; }

        public AsyncModeAttribute(Type asyncInterface, AsyncExecutionMode mode)
        {
            AsyncInterface = asyncInterface;
            Mode = mode;
        }
    }
}
```

`AllowMultiple = true` because a class can have different modes for different interfaces:
```csharp
[AsyncMode(typeof(IAsyncEnterController), AsyncExecutionMode.Sequential)]
[AsyncMode(typeof(IAsyncExitController), AsyncExecutionMode.Parallel)]
```

- [ ] **Step 3: Build to verify**

```bash
cd Aspid.Core.HSM.Generators && dotnet build Aspid.Core.HSM.Generators.slnx
```

- [ ] **Step 4: Commit**

```bash
git add Aspid.Core.HSM/Assets/Plugins/Aspid/Core/HSM/Source/Generation/AsyncExecutionMode.cs Aspid.Core.HSM/Assets/Plugins/Aspid/Core/HSM/Source/Generation/AsyncModeAttribute.cs
git commit -m "feat: add AsyncExecutionMode enum and AsyncModeAttribute"
```

---

### Task 2: Update generator data model — add Mode to ControllerInterfaceData

**Files:**
- Modify: `$GEN/Descriptions/HsmClasses.cs`
- Modify: `$GEN/ControllerGroup/Data/Interfaces/ControllerInterfaceData.cs`
- Modify: `$GEN/ControllerGroup/Factories/ControllerInterfaceDataFactory.cs`

- [ ] **Step 1: Add AsyncModeAttribute to HsmClasses.cs**

In `$GEN/Descriptions/HsmClasses.cs`, add a new field alongside the existing attribute descriptors:

```csharp
public static readonly AttributeText AsyncModeAttribute =
    new(HsmNamespaces.Aspid_Core_HSM, "AsyncModeAttribute");
```

- [ ] **Step 2: Add Mode field to ControllerInterfaceData**

In `$GEN/ControllerGroup/Data/Interfaces/ControllerInterfaceData.cs`, add a new field to the readonly struct. The current fields are:
- `TypeSymbol` (sync interface)
- `AsyncTypeSymbol` (async interface, nullable)
- `ControllerIndexes`
- `ControllerIsAsync`
- `Methods`

Add `AsyncExecutionMode` as an int (since the generator targets netstandard2.0 and can't reference the runtime enum directly):

```csharp
public readonly int AsyncMode; // 0 = Parallel (default), 1 = Sequential
```

Update the constructor to accept this new field.

- [ ] **Step 3: Read [AsyncMode] in ControllerInterfaceDataFactory**

In `$GEN/ControllerGroup/Factories/ControllerInterfaceDataFactory.cs`, in the `Create` method, after building the interface data, read `[AsyncMode]` attributes from the class declaration:

1. Get the class symbol from the semantic model
2. Look for `AsyncModeAttribute` instances on the class
3. For each, match the first constructor argument (interface type) to the interface data's `AsyncTypeSymbol`
4. Extract the mode (second constructor argument) as an int
5. Pass the mode value into `ControllerInterfaceData`

If no `[AsyncMode]` is found for a given interface, default to `0` (Parallel).

- [ ] **Step 4: Build and run existing tests**

```bash
cd Aspid.Core.HSM.Generators && dotnet build Aspid.Core.HSM.Generators.slnx
cd Aspid.Core.HSM.Generators && dotnet test Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests/Aspid.Core.HSM.Generators.Tests.csproj -v normal
```

All 32 existing tests must still pass — this change is additive only.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add AsyncMode to generator data model"
```

---

### Task 3: Update generator body — emit Parallel (WhenAll) vs Sequential dispatch

**Files:**
- Modify: `$GEN/ControllerGroup/Bodies/ControllerGroupBody.cs`

- [ ] **Step 1: Modify AppendAsyncInterfaceMethod to use mode**

In `ControllerGroupBody.cs`, the method `AppendAsyncInterfaceMethod` currently emits sequential `await` for each controller. Modify it to:

1. Read `interfaceData.AsyncMode`
2. If **Sequential** (mode == 1): keep current behavior — `await` each controller one by one
3. If **Parallel** (mode == 0, default): emit `await UniTask.WhenAll(...)` wrapping all async controller calls

**Sequential emit (current behavior, mode == 1):**
```csharp
await ((IAsyncEnterController)__controller0).OnEnterAsync(ct);
await ((IAsyncEnterController)__controller1).OnEnterAsync(ct);
```

**Parallel emit (new, mode == 0):**
```csharp
await Cysharp.Threading.Tasks.UniTask.WhenAll(
    ((IAsyncEnterController)__controller0).OnEnterAsync(ct),
    ((IAsyncEnterController)__controller1).OnEnterAsync(ct)
);
```

For mixed sync/async controllers in Parallel mode, sync controllers should still be called synchronously before the WhenAll:
```csharp
((IEnterController)__controller0).OnEnter();  // sync, no await
await Cysharp.Threading.Tasks.UniTask.WhenAll(
    ((IAsyncEnterController)__controller1).OnEnterAsync(ct),
    ((IAsyncEnterController)__controller2).OnEnterAsync(ct)
);
```

For Sequential mode with mixed controllers, keep existing behavior: sync calls inline, async calls with `await`.

- [ ] **Step 2: Build and run existing tests**

```bash
cd Aspid.Core.HSM.Generators && dotnet test Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests/Aspid.Core.HSM.Generators.Tests.csproj -v normal
```

All 32 tests must pass. The default mode is Parallel, but existing tests that emit async code should still work because:
- If there's only 1 async controller, WhenAll with 1 argument is equivalent to a single await
- The async emit test `Group_with_async_controller_emits_async_method_using_AsyncOf_pair` may need adjustment if the generated code format changes

If existing async emit tests break due to the format change, update them to match the new Parallel output format.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: emit Parallel (WhenAll) or Sequential async dispatch based on AsyncMode"
```

---

### Task 4: Add linked compile references for new files in test project

**Files:**
- Modify: `$TESTS/Aspid.Core.HSM.Generators.Tests.csproj`

- [ ] **Step 1: Add linked compile items for AsyncExecutionMode.cs and AsyncModeAttribute.cs**

The test project links runtime `.cs` files from the Unity package so they can be used in tests outside Unity. Add `<Compile Include="...">` items for the two new files, following the existing pattern in the csproj.

- [ ] **Step 2: Build tests to verify**

```bash
cd Aspid.Core.HSM.Generators && dotnet build Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests/Aspid.Core.HSM.Generators.Tests.csproj
```

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "chore: link AsyncMode files in test project"
```

---

### Task 5: Generator emit tests — Sequential and Parallel modes

**Files:**
- Create: `$TESTS/GeneratorTests/AsyncModeEmitTests.cs`

- [ ] **Step 1: Write generator emit tests**

Create `$TESTS/GeneratorTests/AsyncModeEmitTests.cs` with these tests:

1. **`Parallel_mode_emits_WhenAll_for_async_controllers`** — Two async controllers with `[AsyncMode(typeof(IAsyncEnterController), AsyncExecutionMode.Parallel)]` → generated code contains `WhenAll`

2. **`Sequential_mode_emits_sequential_await_for_async_controllers`** — Two async controllers with `[AsyncMode(typeof(IAsyncEnterController), AsyncExecutionMode.Sequential)]` → generated code contains two separate `await` lines, no `WhenAll`

3. **`Default_mode_is_Parallel_when_no_AsyncMode_attribute`** — Two async controllers without any `[AsyncMode]` → generated code contains `WhenAll` (default is Parallel)

4. **`Mixed_sync_async_in_Parallel_mode_calls_sync_before_WhenAll`** — One sync + two async controllers with Parallel mode → sync called without await, then `WhenAll` for async ones

Use the same `RunGenerator` pattern from existing tests. The test source must include the `IController`, `AsyncOfAttribute`, `AsyncModeAttribute`, `AsyncExecutionMode`, and controller interfaces inline (since generator tests run in isolation with in-memory compilation).

- [ ] **Step 2: Run tests**

```bash
cd Aspid.Core.HSM.Generators && dotnet test Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests/Aspid.Core.HSM.Generators.Tests.csproj --filter FullyQualifiedName~AsyncModeEmitTests -v normal
```

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "test: add generator emit tests for AsyncMode Sequential/Parallel"
```

---

### Task 6: Final verification

- [ ] **Step 1: Run all tests**

```bash
cd Aspid.Core.HSM.Generators && dotnet test Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests/Aspid.Core.HSM.Generators.Tests.csproj -v normal
```

Expected: All tests pass (32 existing + 4 new = 36).

- [ ] **Step 2: Build full solution**

```bash
cd Aspid.Core.HSM.Generators && dotnet build Aspid.Core.HSM.Generators.slnx
```

Expected: Clean build.
