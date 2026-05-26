# HSM Phase 3: Transition Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a full Transition Pipeline to the HSM: `ITransition` with guards, transition registry, `TransitionTo<T>()`/`TransitionVia<T>()` API, sync and async pipeline execution, chain fallback resolution, and `[Transition]` source generator.

**Architecture:** Transitions wrap the existing `ChangeState<T>()` low-level API with a pipeline: Guard → OnBeforeTransition → Exit Source → Enter Target → OnAfterTransition. Transitions are registered in the StateMachine and looked up by source/target type pair. When no direct transition exists, the SM composes a chain along the state tree path. `ChangeState<T>()` remains as internal low-level API; the public API becomes `TransitionTo<T>()`/`TransitionVia<T>()`.

**Tech Stack:** C# (.NET 10, netstandard2.0 for generator), xUnit, Roslyn Incremental Source Generators, UniTask

---

**Path aliases:**

| Alias | Full Path |
|---|---|
| `$RT` | `Aspid.Core.HSM/Assets/Plugins/Aspid/Core/HSM` |
| `$GEN` | `Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators` |
| `$TESTS` | `Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests` |

All paths relative to: `/Users/vladislavpanin/Documents/Docs/Aspid/Packages/Aspid.Core.HSM/Projects/Aspid.Core.HSM/`

---

## File Structure

### Files to create

| File | Responsibility |
|---|---|
| `$RT/Source/ITransition.cs` | `ITransition`, `ITransition<TSource,TTarget>` interfaces |
| `$RT/Source/Generation/TransitionAttribute.cs` | `[Transition(typeof(Source), typeof(Target))]` |
| `$RT/Unity/Runtime/StateMachines/StateMachineBase.Transitions.cs` | Transition registry + pipeline execution (partial class) |
| `$GEN/Transition/TransitionGenerator.cs` | Source generator for `[Transition]` |
| `$GEN/Transition/Data/TransitionData.cs` | Generator data record |
| `$GEN/Transition/Bodies/TransitionBody.cs` | Generator emit logic |
| `$GEN/Transition/Factories/TransitionDataFactory.cs` | Generator data extraction |
| `$TESTS/StateMachineTests/TransitionPipelineTests.cs` | Runtime tests for transition pipeline |
| `$TESTS/StateMachineTests/TransitionChainTests.cs` | Runtime tests for chain fallback |
| `$TESTS/GeneratorTests/TransitionEmitTests.cs` | Generator emit tests |

### Files to modify

| File | Change |
|---|---|
| `$RT/Source/IStateMachine.cs` | Add `TransitionTo`, `TransitionVia`, `IsTransitioning` |
| `$RT/Unity/Runtime/StateMachines/MonoStateMachine.cs` | Forward new transition API |
| `$RT/Unity/Runtime/StateMachines/MonoStateMachine.Async.cs` | Forward async transition API |
| `$RT/Unity/Runtime/StateMachines/MonoStateMachineCore.cs` | Forward transition hooks |
| `$GEN/Descriptions/HsmClasses.cs` | Add `TransitionAttribute` descriptor |
| `$TESTS/Aspid.Core.HSM.Generators.Tests.csproj` | Link new runtime files |
| `$TESTS/StateMachineTests/TestableStateMachine.cs` | Expose transition methods + track hooks |

---

### Task 1: Core types — ITransition + TransitionAttribute

**Files:**
- Create: `$RT/Source/ITransition.cs`
- Create: `$RT/Source/Generation/TransitionAttribute.cs`

- [ ] **Step 1: Create ITransition.cs**

```csharp
using System;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public interface ITransition
    {
        Type SourceState { get; }
        Type TargetState { get; }
        bool CanTransition() => true;
        void OnBeforeTransition() { }
        void OnAfterTransition() { }
    }

    public interface ITransition<TSource, TTarget> : ITransition
        where TSource : IState
        where TTarget : IState
    {
        Type ITransition.SourceState => typeof(TSource);
        Type ITransition.TargetState => typeof(TTarget);
    }
}
```

- [ ] **Step 2: Create TransitionAttribute.cs**

```csharp
using System;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class TransitionAttribute : Attribute
    {
        public Type SourceState { get; }
        public Type TargetState { get; }

        public TransitionAttribute(Type sourceState, Type targetState)
        {
            SourceState = sourceState;
            TargetState = targetState;
        }
    }
}
```

- [ ] **Step 3: Build, commit**

```bash
git commit -m "feat: add ITransition interfaces and TransitionAttribute"
```

---

### Task 2: TransitionGenerator (source generator for [Transition])

**Files:**
- Create: `$GEN/Transition/TransitionGenerator.cs`
- Create: `$GEN/Transition/Data/TransitionData.cs`
- Create: `$GEN/Transition/Bodies/TransitionBody.cs`
- Create: `$GEN/Transition/Factories/TransitionDataFactory.cs`
- Modify: `$GEN/Descriptions/HsmClasses.cs`

The generator processes classes with `[Transition(typeof(Source), typeof(Target))]` and generates `ITransition.SourceState` and `ITransition.TargetState` property implementations. Follow the exact same pattern as `ChildStateGenerator`.

- [ ] **Step 1: Add TransitionAttribute to HsmClasses.cs**

```csharp
public static readonly AttributeText TransitionAttribute =
    new(nameof(TransitionAttribute), namespaceText: HsmNamespaces.Aspid_Core_HSM);
```

- [ ] **Step 2: Create TransitionData.cs**

```csharp
public readonly struct TransitionData
{
    public readonly ClassDeclarationSyntax ClassDeclaration;
    public readonly ITypeSymbol SourceStateType;
    public readonly ITypeSymbol TargetStateType;
}
```

- [ ] **Step 3: Create TransitionDataFactory.cs**

Extracts source and target types from `[Transition]` attribute constructor arguments. Same pattern as `ChildStateDataFactory`.

- [ ] **Step 4: Create TransitionBody.cs**

Generates:
```csharp
[GeneratedCode("Aspid.Core.HSM.Generators.TransitionGenerator", "0.0.1")]
global::System.Type ITransition.SourceState => typeof(SourceType);

[GeneratedCode("Aspid.Core.HSM.Generators.TransitionGenerator", "0.0.1")]
global::System.Type ITransition.TargetState => typeof(TargetType);
```

Same pattern as `ChildStateBody`.

- [ ] **Step 5: Create TransitionGenerator.cs**

Incremental generator triggered by `[Transition]` on partial non-static classes. Same pattern as `ChildStateGenerator`.

- [ ] **Step 6: Build, run existing tests (must all pass), commit**

```bash
git commit -m "feat: add TransitionGenerator source generator"
```

---

### Task 3: StateMachineBase transition pipeline (sync + async)

This is the core task. Add transition registry and pipeline execution.

**Files:**
- Modify: `$RT/Source/IStateMachine.cs`
- Create: `$RT/Unity/Runtime/StateMachines/StateMachineBase.Transitions.cs`

- [ ] **Step 1: Update IStateMachine.cs**

Add transition API:

```csharp
public interface IStateMachine
{
    IReadOnlyList<IState> CurrentStates { get; }

    void ChangeState<T>() where T : IState;

    void TransitionTo<TTarget>() where TTarget : IState;
    void TransitionVia<TTransition>() where TTransition : ITransition;
    UniTask TransitionToAsync<TTarget>(CancellationToken ct = default) where TTarget : IState;
    UniTask TransitionViaAsync<TTransition>(CancellationToken ct = default) where TTransition : ITransition;

    bool IsTransitioning { get; }
}
```

- [ ] **Step 2: Create StateMachineBase.Transitions.cs**

New partial class file for transition logic:

```csharp
public partial class StateMachineBase
{
    private readonly Dictionary<(Type source, Type target), ITransition> _transitions = new();

    public bool IsTransitioning => _activeTransitionCts is not null;

    // Registration
    public void RegisterTransition(ITransition transition)
    {
        _transitions[(transition.SourceState, transition.TargetState)] = transition;
    }

    public void RegisterTransition<TSource, TTarget>(ITransition<TSource, TTarget> transition)
        where TSource : IState where TTarget : IState
    {
        RegisterTransition((ITransition)transition);
    }

    // Sync pipeline
    public void TransitionTo<TTarget>() where TTarget : IState
    {
        var leafState = _currentStates[^1].GetType();
        var transition = ResolveTransition(leafState, typeof(TTarget));

        if (transition is not null && !transition.CanTransition())
            return;

        transition?.OnBeforeTransition();
        ChangeState<TTarget>();
        transition?.OnAfterTransition();
    }

    public void TransitionVia<TTransition>() where TTransition : ITransition
    {
        var transition = _transitions.Values.OfType<TTransition>().FirstOrDefault()
            ?? throw new InvalidOperationException($"Transition {typeof(TTransition)} not registered.");

        if (!transition.CanTransition()) return;

        transition.OnBeforeTransition();
        ChangeStateByType(transition.TargetState);
        transition.OnAfterTransition();
    }

    // Resolution
    protected virtual ITransition? ResolveTransition(Type source, Type target)
    {
        return _transitions.GetValueOrDefault((source, target));
    }

    // Helper to change state by Type (runtime type, not generic)
    private void ChangeStateByType(Type targetStateType)
    {
        // Use reflection to call ChangeState<T>() with runtime type
        var method = typeof(StateMachineBase).GetMethod(nameof(ChangeState))!
            .MakeGenericMethod(targetStateType);
        method.Invoke(this, null);
    }
}
```

- [ ] **Step 3: Add async pipeline in same file**

```csharp
    public async UniTask TransitionToAsync<TTarget>(CancellationToken ct = default)
        where TTarget : IState
    {
        var leafState = _currentStates[^1].GetType();
        var transition = ResolveTransition(leafState, typeof(TTarget));

        if (transition is not null && !transition.CanTransition())
            return;

        transition?.OnBeforeTransition();
        await ChangeStateAsync<TTarget>(ct);
        transition?.OnAfterTransition();
    }

    public async UniTask TransitionViaAsync<TTransition>(CancellationToken ct = default)
        where TTransition : ITransition
    {
        var transition = _transitions.Values.OfType<TTransition>().FirstOrDefault()
            ?? throw new InvalidOperationException($"Transition {typeof(TTransition)} not registered.");

        if (!transition.CanTransition()) return;

        transition.OnBeforeTransition();
        await ChangeStateAsyncByType(transition.TargetState, ct);
        transition.OnAfterTransition();
    }

    private async UniTask ChangeStateAsyncByType(Type targetStateType, CancellationToken ct)
    {
        var method = typeof(StateMachineBase).GetMethod(nameof(ChangeStateAsync))!
            .MakeGenericMethod(targetStateType);
        await (UniTask)method.Invoke(this, new object[] { ct })!;
    }
```

- [ ] **Step 4: Build, run existing tests, commit**

```bash
git commit -m "feat: add transition registry and pipeline to StateMachineBase"
```

---

### Task 4: Update MonoStateMachine + link test csproj

**Files:**
- Modify: `$RT/Unity/Runtime/StateMachines/MonoStateMachine.cs`
- Modify: `$RT/Unity/Runtime/StateMachines/MonoStateMachine.Async.cs`
- Modify: `$RT/Unity/Runtime/StateMachines/MonoStateMachineCore.cs`
- Modify: `$TESTS/Aspid.Core.HSM.Generators.Tests.csproj`
- Modify: `$TESTS/StateMachineTests/TestableStateMachine.cs`

- [ ] **Step 1: MonoStateMachine — forward TransitionTo/TransitionVia**

Add to `MonoStateMachine.cs`:
```csharp
public void TransitionTo<TTarget>() where TTarget : IState =>
    _stateMachine!.TransitionTo<TTarget>();

public void TransitionVia<TTransition>() where TTransition : ITransition =>
    _stateMachine!.TransitionVia<TTransition>();

public bool IsTransitioning => _stateMachine?.IsTransitioning ?? false;
```

Add to `MonoStateMachine.Async.cs`:
```csharp
public UniTask TransitionToAsync<TTarget>(CancellationToken ct = default)
    where TTarget : IState =>
    _stateMachine!.TransitionToAsync<TTarget>(ct);

public UniTask TransitionViaAsync<TTransition>(CancellationToken ct = default)
    where TTransition : ITransition =>
    _stateMachine!.TransitionViaAsync<TTransition>(ct);
```

- [ ] **Step 2: Link new files in test csproj**

Add `<Compile Include>` for:
- `$RT/Source/ITransition.cs`
- `$RT/Source/Generation/TransitionAttribute.cs`
- `$RT/Unity/Runtime/StateMachines/StateMachineBase.Transitions.cs`

- [ ] **Step 3: Update TestableStateMachine**

Add transition forwarding methods:
```csharp
public void CallTransitionTo<TTarget>() where TTarget : IState => TransitionTo<TTarget>();
public void CallTransitionVia<TTransition>() where TTransition : ITransition => TransitionVia<TTransition>();
public void CallRegisterTransition(ITransition transition) => RegisterTransition(transition);
```

- [ ] **Step 4: Build, run all tests, commit**

```bash
git commit -m "feat: forward transition API in MonoStateMachine and test infrastructure"
```

---

### Task 5: Runtime tests — transition pipeline

**Files:**
- Create: `$TESTS/StateMachineTests/TransitionPipelineTests.cs`

- [ ] **Step 1: Write tests**

Test cases:

1. **`TransitionTo_executes_state_change`** — basic TransitionTo changes state
2. **`TransitionTo_with_registered_transition_calls_guard`** — registered transition's `CanTransition()` is checked
3. **`TransitionTo_aborts_when_guard_returns_false`** — state doesn't change if guard fails
4. **`TransitionTo_calls_OnBeforeTransition_and_OnAfterTransition`** — pipeline hooks fire in order
5. **`TransitionTo_without_registered_transition_still_works`** — no transition registered = default behavior (no guard, just change state)
6. **`TransitionVia_uses_specific_transition`** — TransitionVia finds the right transition by type
7. **`TransitionVia_throws_if_not_registered`** — TransitionVia with unregistered type throws
8. **`RegisterTransition_replaces_existing`** — re-registering same source/target replaces previous

Test helpers needed:
```csharp
public class TestTransition : ITransition<SimpleTestState, AnotherTestState>
{
    public bool GuardResult { get; set; } = true;
    public int BeforeCount { get; private set; }
    public int AfterCount { get; private set; }
    public bool CanTransition() => GuardResult;
    public void OnBeforeTransition() => BeforeCount++;
    public void OnAfterTransition() => AfterCount++;
}
```

- [ ] **Step 2: Run tests, commit**

```bash
git commit -m "test: add runtime tests for transition pipeline"
```

---

### Task 6: Chain fallback resolution + tests

**Files:**
- Modify: `$RT/Unity/Runtime/StateMachines/StateMachineBase.Transitions.cs`
- Create: `$TESTS/StateMachineTests/TransitionChainTests.cs`

- [ ] **Step 1: Implement chain fallback in ResolveTransition or TransitionTo**

When `TransitionTo<TTarget>()` finds no direct transition, compose a chain:
1. Determine the path from current leaf to target (via common ancestor)
2. For each segment, look up registered transition
3. Check ALL guards before any exit (if any guard fails → abort entire transition)
4. If all guards pass, execute the state change (ChangeState<TTarget> handles the exit/enter sequence)

The chain fallback logic lives in `TransitionTo`:
```csharp
// If direct transition exists, use it (already handled)
// If not, collect all segment transitions along the path
// Check all guards
// Execute
```

- [ ] **Step 2: Write chain tests**

Test cases:
1. **`Chain_fallback_composes_transitions_along_path`** — Grandchild→Simple: if no direct transition, individual segment transitions fire in order
2. **`Chain_fallback_aborts_if_any_guard_fails`** — one segment's guard returns false, entire transition aborts
3. **`Direct_transition_takes_priority_over_chain`** — when both direct and chain exist, direct is used

- [ ] **Step 3: Run tests, commit**

```bash
git commit -m "feat: add chain fallback resolution for transitions"
```

---

### Task 7: Generator emit tests for [Transition]

**Files:**
- Create: `$TESTS/GeneratorTests/TransitionEmitTests.cs`

- [ ] **Step 1: Write generator tests**

Tests:
1. **`Transition_attribute_generates_SourceState_and_TargetState_properties`** — verify generated code
2. **`Transition_without_attribute_generates_nothing`** — no [Transition] = no generation

Follow RunGenerator pattern. Test source must define ITransition, TransitionAttribute, IState inline.

- [ ] **Step 2: Run, commit**

```bash
git commit -m "test: add generator emit tests for TransitionGenerator"
```

---

### Task 8: Final verification

- [ ] **Step 1: Run all tests**

```bash
cd Aspid.Core.HSM.Generators && dotnet test Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests/Aspid.Core.HSM.Generators.Tests.csproj -v normal
```

- [ ] **Step 2: Build full solution**

```bash
cd Aspid.Core.HSM.Generators && dotnet build Aspid.Core.HSM.Generators.slnx
```
