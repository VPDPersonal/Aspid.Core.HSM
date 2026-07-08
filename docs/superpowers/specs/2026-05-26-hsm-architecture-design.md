# Aspid.Core.HSM — Hierarchical State Machine Architecture Design

## Overview

Aspid.Core.HSM is a hierarchical state machine framework for game architecture. It provides a composable, type-safe system for managing game states through controllers, states, transitions, and scopes. The framework is platform-agnostic at its core with a Unity integration layer.

### Goals

- **Composability**: Controllers as smallest units, grouped into ControllerGroups, composed into States
- **Hierarchical states**: Parent-child relationships where parent behavior persists during child execution
- **Dynamic extension**: Extension States that overlay behavior on host states at runtime
- **Safe transitions**: Full pipeline with guards, async support, and optional visual transition states
- **DI-agnostic scoping**: Each state owns a scope; integration via adapters (VContainer)
- **Configurability**: Extension points for disabling controllers/states, replacing transitions (implementation deferred)
- **Claude-first DX**: Designed for AI-assisted development with Claude tooling (separate spec)

### Non-Goals (This Spec)

- Detailed Claude tooling design (separate spec after Phase 6)
- Configuration system implementation (extension points only)
- Editor tooling (inspector, debug visualizer)

---

## 1. Type Hierarchy: Controller → ControllerGroup → State

### 1.1 Controller

The smallest unit of logic. A controller is a class that implements one or more controller interfaces. Controllers know nothing about the state machine or states — they contain only their domain logic.

```csharp
public interface IController { }

public class PlayerMovementController : IUpdateController, IEnterController
{
    void IEnterController.OnEnter() { /* init input */ }
    void IUpdateController.Update(float dt) { /* move player */ }
}
```

### 1.2 Controller Interfaces

Controllers implement lifecycle interfaces dispatched by the State Machine:

| Interface | Method | When Called | Order |
|---|---|---|---|
| `IEnterController` | `OnEnter()` | State enter | Forward |
| `IExitController` | `OnExit()` | State exit | Reverse |
| `IUpdateController` | `Update(float)` | Unity Update() | Forward |
| `ILateUpdateController` | `LateUpdate(float)` | Unity LateUpdate() | Forward |
| `IFixedUpdateController` | `FixedUpdate(float)` | Unity FixedUpdate() | Forward |
| `IDisposableController` | `Dispose()` | State Machine disposal | Reverse |
| `IAsyncEnterController` | `OnEnterAsync(CancellationToken)` | Async state enter | Forward |
| `IAsyncExitController` | `OnExitAsync(CancellationToken)` | Async state exit | Reverse |

Async interfaces are paired to sync ones via `[AsyncOf(typeof(ISyncInterface))]`. Core uses `Task`/`ValueTask`; Unity layer uses `UniTask`.

### 1.3 ControllerGroup

A composite controller (Composite pattern). It is itself an `IController` and aggregates child controllers and/or other ControllerGroups. The `[ControllerGroup]` source generator implements all controller interfaces from its children, delegating calls.

```csharp
[ControllerGroup]
public partial class InputControllerGroup : IController
{
    public InputControllerGroup()
    {
        AddControllers(
            new InputReadController(),
            new InputMappingController()
        );
    }
}
```

ControllerGroups can be nested. A ControllerGroup inside another ControllerGroup or State is treated as a single controller from the parent's perspective.

### 1.4 State

A State is a ControllerGroup with additional semantics:

- `Enter()` / `Exit()` called by the State Machine during transitions
- Parent state declared via `[ParentState(typeof(ParentType))]`
- Unit of Scope (for DI, config)
- Can contain individual controllers and ControllerGroups

```csharp
[ControllerGroup]
[ParentState(typeof(GameplayState))]
public partial class FreerideState : IState
{
    public FreerideState()
    {
        AddControllers(
            new PlayerMovementController(),
            new InputControllerGroup(),   // reusable group
            new FreerideHudController()
        );
    }
}
```

### 1.5 State Tree

The tree is formed statically at startup via `[ParentState]` attributes. When transitioning from parent to child, the parent does NOT exit — its controllers continue running.

```
RootState
├── MainMenuState
├── GameplayState
│   ├── FreerideState
│   │   └── FreerideInventoryState
│   └── RaceState
│       └── RaceResultState
└── SettingsState
```

---

## 2. Transition Pipeline

### 2.1 Transition Entity

A Transition describes how to move between states. Each Transition contains:

- **Source/Target** — origin and destination state types
- **Guards** — conditions that must be met (`CanTransition()`)
- **Pipeline** — sequence of actions during transition
- **Optional TransitionState** — for visually heavy transitions (loading, fade)

```csharp
public interface ITransition
{
    Type SourceState { get; }
    Type TargetState { get; }
    bool CanTransition();
}

public interface ITransition<TSource, TTarget> : ITransition
    where TSource : IState
    where TTarget : IState
{ }
```

### 2.2 Pipeline Execution Order

```
1. CanTransition() → false? → abort
2. OnBeforeTransition()
3. [If TransitionState exists] → Enter TransitionState
4. Exit Source states (reverse order, from leaf to common ancestor)
5. [Async point — scene loading, resource loading, etc.]
6. Enter Target states (forward order, from common ancestor to leaf)
7. [If TransitionState exists] → Exit TransitionState
8. OnAfterTransition()
```

### 2.3 TransitionState

An optional State that lives between exiting Source and entering Target. Used for loading screens, fade effects, intermediate animations.

**TransitionState runs in a separate execution context** — not in the main State Machine. The main machine is mid-transition and cannot "enter" another state via its normal mechanism. Instead, the Transition owns a lightweight state machine (or execution context) that manages the TransitionState lifecycle independently. This separate machine handles the TransitionState's controller dispatch (Update, etc.) while the main machine's transition pipeline is suspended awaiting async work.

```csharp
[ControllerGroup]
public partial class LoadingTransitionState : IState
{
    public LoadingTransitionState()
    {
        AddControllers(
            new LoadingScreenController(),
            new ProgressBarController()
        );
    }
}
```

### 2.4 Transition Resolution (Direct vs Chain)

When a transition is requested, the State Machine resolves it with priority:

1. **Direct Transition**: If a specific `ITransition<Source, Target>` is registered, use it. One Transition, one pipeline, one TransitionState (e.g., one loading screen instead of three).

2. **Chain Fallback**: If no direct Transition is registered, compose a chain of Transitions along the state tree path. Each segment uses its own registered Transition (or a default no-op Transition). All guards in the chain must pass before any Exit begins — if any guard fails, the entire transition is aborted.

This allows defining custom "big jump" transitions with unified loading screens while keeping automatic resolution for simple transitions.

### 2.5 Registration

```csharp
public class GameStateMachine : StateMachineBase
{
    protected override void RegisterTransitions()
    {
        Register<MainMenuState, GameplayState>(
            new MainMenuToGameplayTransition(
                transitionState: new LoadingTransitionState()
            )
        );
        Register<FreerideState, RaceState>(new FreerideToRaceTransition());
    }
}
```

### 2.6 Invocation

```csharp
// State Machine finds registered Transition from current state to RaceState
stateMachine.TransitionTo<RaceState>();

// Or specify a particular Transition (when multiple exist)
stateMachine.TransitionVia<FreerideToRaceTransition>();
```

---

## 3. Extension States

### 3.1 Concept

An Extension State dynamically attaches to a host state, extending its behavior. The host state continues running; the Extension State adds its own controllers.

Key properties:
- **Not standalone** — can only exist when attached to a host state
- **Dynamic** — attached/detached at runtime, not part of the static tree
- **Constrained** — declares which states it can attach to
- **Stackable** — multiple Extension States can be attached to the same host simultaneously

### 3.2 Interface

```csharp
public interface IExtensionState : IState
{
    bool CanAttachTo(IState hostState);
    void OnAttached(IState hostState);
    void OnDetached(IState hostState);
}
```

### 3.3 Compatibility Constraints

Via attribute + generated `CanAttachTo`:

```csharp
[ExtensionFor(typeof(FreerideState), typeof(RaceState))]
[ControllerGroup]
public partial class MiniGameExtensionState : IExtensionState
{
    public MiniGameExtensionState()
    {
        AddControllers(
            new MiniGameTimerController(),
            new MiniGameHudController()
        );
    }
    
    // Generated by [ExtensionFor]:
    // bool IExtensionState.CanAttachTo(IState host) =>
    //     host is FreerideState or RaceState;
}
```

Custom logic can be added by implementing `CanAttachTo` manually (generator skips if already implemented).

### 3.4 Lifecycle

```
Host State active
    ↓
stateMachine.AttachExtension<MiniGameExtensionState>()
    ↓
CanAttachTo(currentState) → true?
    ↓
MiniGameExtensionState.Enter()
Controllers start receiving Update/LateUpdate/etc.
    ↓
... time passes ...
    ↓
stateMachine.DetachExtension<MiniGameExtensionState>()
    ↓
MiniGameExtensionState.Exit()
Controllers stop
```

### 3.5 Behavior During Host State Change

When the host state changes via transition, Extension States are checked:

- If new state is compatible (`CanAttachTo` → true) → Extension stays attached
- If incompatible → automatic detach (`Exit()` called before host `Exit()`)

```
Freeride (+ MiniGameExtension) → Race
MiniGameExtension.CanAttachTo(RaceState) → true → stays attached

Freeride (+ MiniGameExtension) → MainMenu
MiniGameExtension.CanAttachTo(MainMenuState) → false → auto-detach
```

### 3.6 Controller Dispatch Order

```
Update():
  1. Host State controllers (in AddControllers order)
  2. Extension State 1 controllers
  3. Extension State 2 controllers
  ... (in attachment order)
```

---

## 4. Async Support

### 4.1 Two Modes

Async controller dispatch supports two execution modes:

- **Sequential** — controllers execute one after another, each awaiting the previous
- **Parallel** — all controllers start simultaneously, await `WhenAll`

### 4.2 AsyncMode Per-Interface

The mode is configured per controller interface, not per State:

```csharp
[ControllerGroup]
[AsyncMode(typeof(IAsyncEnterController), AsyncExecutionMode.Sequential)]
[AsyncMode(typeof(IAsyncExitController), AsyncExecutionMode.Parallel)]
public partial class GameplayState : IState { ... }
```

If not specified, default is **Parallel** (controllers should not depend on each other's initialization order).

### 4.3 Generated Code

```csharp
// Sequential Enter:
async UniTask IAsyncEnterController.OnEnterAsync(CancellationToken ct)
{
    await __controller0.OnEnterAsync(ct);
    await __controller1.OnEnterAsync(ct);
}

// Parallel Exit:
async UniTask IAsyncExitController.OnExitAsync(CancellationToken ct)
{
    await UniTask.WhenAll(
        __controller0.OnExitAsync(ct),
        __controller1.OnExitAsync(ct)
    );
}
```

### 4.4 Mixed Sync/Async Controllers

A State can contain both sync and async controllers. During async dispatch:
- If a controller implements the async interface → await it
- If it only implements the sync interface → call sync method (no await)

This fallback behavior is already implemented in the current generator.

### 4.5 Async in Transition Pipeline

The Transition pipeline is async-aware:

```
1. CanTransition() → sync guard
2. await OnBeforeTransitionAsync(ct)
3. [If TransitionState] → Enter TransitionState
4. await Exit Source (async controllers in configured mode)
5. await [Custom async logic — scene loading, resources]
6. await Enter Target (async controllers in configured mode)
7. [If TransitionState] → Exit TransitionState
8. await OnAfterTransitionAsync(ct)
```

### 4.6 CancellationToken

- Reentrant async transitions cancel the previous one
- State Machine disposal cancels all pending operations
- TransitionState can expose cancellation UI via the token

### 4.7 Core vs Unity

Core (no Unity dependencies): async interfaces use `Task`/`ValueTask`.
Unity layer: provides `UniTask` variants via `[AsyncOf]` attribute pairing.

---

## 5. State Machine

### 5.1 Responsibilities

1. **State tree management** — maintains current hierarchy of active states
2. **Controller dispatch** — calls Update/LateUpdate/FixedUpdate on active controllers
3. **Transition management** — holds transition registry, executes pipeline
4. **Extension State management** — manages attached extensions
5. **Extension points** — virtual hooks for future config system

### 5.2 Interface

```csharp
public interface IStateMachine
{
    IReadOnlyList<IState> CurrentStates { get; }
    IReadOnlyList<IExtensionState> ActiveExtensions { get; }
    
    // Transition by target state — looks up registered transition from current state
    void TransitionTo<TTarget>() where TTarget : IState;
    
    // Transition by explicit transition type — uses a specific registered transition
    void TransitionVia<TTransition>() where TTransition : ITransition;
    
    void AttachExtension<T>() where T : IExtensionState;
    void DetachExtension<T>() where T : IExtensionState;
    
    bool IsTransitioning { get; }
}
```

### 5.3 Controller Dispatch

| Interface | Unity Lifecycle |
|---|---|
| `IEnterController` | On state enter |
| `IExitController` | On state exit |
| `IUpdateController` | `MonoBehaviour.Update()` |
| `ILateUpdateController` | `MonoBehaviour.LateUpdate()` |
| `IFixedUpdateController` | `MonoBehaviour.FixedUpdate()` |
| `IDisposableController` | On State Machine disposal |
| Async variants | Analogous, with await |

Order: all states in hierarchy (root → leaf), then Extension States. Within each state — in AddControllers order.

### 5.4 Extension Points (Deferred Implementation)

```csharp
protected virtual bool IsControllerEnabled(IController controller, IState state) => true;
protected virtual bool IsStateEnabled(Type stateType) => true;
protected virtual ITransition? ResolveTransition(Type source, Type target) => /* registry lookup */;
```

These return defaults now but allow plugging in a config system without changing the core.

---

## 6. Scope and DI Integration

### 6.1 Concept

Each state creates its own scope. Child states inherit parent scope dependencies. On state exit, the scope is disposed.

```
RootState scope
    ├── GameplayState scope (inherits Root)
    │   ├── FreerideState scope (inherits Gameplay)
    │   └── RaceState scope (inherits Gameplay)
    └── MainMenuState scope (inherits Root)
```

### 6.2 Core Abstraction (DI-Agnostic)

```csharp
public interface IStateScope : IDisposable
{
    IStateScope? Parent { get; }
    IStateScope CreateChildScope();
}

public abstract class StateFactory
{
    protected abstract IStateScope CreateScope(IStateScope? parentScope, Type stateType);
    protected abstract IState CreateStateInstance(IStateScope scope, Type stateType);
}
```

### 6.3 Scope Lifetime Policy

States can configure scope caching to avoid allocations for frequently visited states:

```csharp
public enum ScopeLifetime
{
    Transient,   // Default — scope created on Enter, disposed on Exit
    Cached       // Scope created once, reused on subsequent Enter
}

[ScopeLifetime(ScopeLifetime.Cached)]
[ControllerGroup]
public partial class GlobalMapState : IState { ... }
```

**Cached scope**: Created on first Enter, not disposed on Exit. Reused on subsequent Enter. Controllers still receive OnEnter/OnExit. Disposed only on State Machine disposal.

**Transient scope** (default): Standard behavior — create and dispose each transition.

### 6.4 VContainer Integration

Separate package: `Aspid.Core.HSM.VContainer`

```csharp
public class VContainerStateFactory : StateFactory
{
    private readonly LifetimeScope _rootScope;
    
    protected override IStateScope CreateScope(IStateScope? parentScope, Type stateType)
    {
        var parent = parentScope as VContainerStateScope;
        var childScope = parent?.LifetimeScope.CreateChild(builder =>
        {
            RegisterStateDependencies(builder, stateType);
        });
        return new VContainerStateScope(childScope);
    }
}
```

### 6.5 Extension State Scopes

Extension States receive a child scope from the host state. They register their own dependencies but don't create a separate hierarchy level.

```
FreerideState scope
    ├── MiniGameExtension scope (child of Freeride)
    └── ChatExtension scope (child of Freeride)
```

---

## 7. Source Generators

### 7.1 Existing Generators (Extended)

**ChildStateGenerator**: Generates `IChildState.ParentState` from `[ParentState]` attribute. No changes needed.

**ControllersGroupGenerator**: Extended for:
- Nested ControllerGroups (ControllerGroup inside ControllerGroup)
- `[AsyncMode]` per-interface (Sequential/Parallel dispatch)
- Mixed sync/async controller dispatch (already implemented)
- Profiler markers (already implemented)

### 7.2 New Generators

**ExtensionStateGenerator**: For `[ExtensionFor]` attribute:
```csharp
[ExtensionFor(typeof(FreerideState), typeof(RaceState))]
public partial class MiniGameExtensionState : IExtensionState { ... }

// Generates:
bool IExtensionState.CanAttachTo(IState host) =>
    host is FreerideState or RaceState;
```

**TransitionGenerator**: For declarative transitions:
```csharp
[Transition(typeof(FreerideState), typeof(RaceState))]
public partial class FreerideToRaceTransition : ITransition { ... }

// Generates:
Type ITransition.SourceState => typeof(FreerideState);
Type ITransition.TargetState => typeof(RaceState);
```

### 7.3 Compile-Time Diagnostics

Generators emit warnings/errors for:
- `[ParentState]` referencing non-existent state
- `[ExtensionFor]` referencing non-existent state
- Cyclic dependencies in state hierarchy
- Controller implementing async interface but State missing `[AsyncMode]` for it

---

## 8. Integrations

### 8.1 Aspid.MVVM

HSM drives game state → MVVM drives presentation within that state.

**Integration points:**

1. **ViewModel as controller** — ViewModel implements controller interfaces, gets lifecycle:
```csharp
[ViewModel]
public partial class FreerideHudViewModel : IEnterController, IUpdateController
{
    [Bind] private int _score;
    void IEnterController.OnEnter() { /* init */ }
    void IUpdateController.Update(float dt) { Score = ...; }
}
```

2. **View lifecycle via State** — `IView.Initialize()` on Enter, `Deinitialize()` on Exit. Managed by a dedicated `ViewLifecycleController<TView, TViewModel>`.

3. **Commands as Transition triggers** — `IRelayCommand.Execute()` calls `stateMachine.Transition<T>()`, `CanExecute` checks `transition.CanTransition()`.

Separate package: `Aspid.Core.HSM.MVVM`

### 8.2 Aspid.FastTools

- **ProfilerMarkers**: Already used in generator. `.Marker()` for runtime profiling.
- **IdRegistry**: Stable IDs for states and transitions (config, analytics, serialization).
- **SerializableType**: Inspector selection of state/controller types.
- **EnumValues**: Mapping states to config data (timeouts, parameters).

FastTools is used directly (existing dependency via ProfilerMarkers).

---

## 9. Project Structure

### 9.1 Package Layout

```
Aspid.Core.HSM/
├── Source/                          # Core (no Unity dependencies)
│   ├── IController.cs
│   ├── IState.cs
│   ├── IChildState.cs
│   ├── IStateMachine.cs
│   ├── ITransition.cs
│   ├── IExtensionState.cs
│   ├── IStateScope.cs
│   ├── StateFactory.cs
│   ├── EmptyState.cs
│   ├── Generation/                  # Attributes
│   │   ├── ParentStateAttribute.cs
│   │   ├── ControllerGroupAttribute.cs
│   │   ├── AsyncOfAttribute.cs
│   │   ├── AsyncModeAttribute.cs
│   │   ├── ExtensionForAttribute.cs
│   │   ├── TransitionAttribute.cs
│   │   ├── ScopeLifetimeAttribute.cs
│   │   ├── ReverseExecuteAttribute.cs
│   │   └── AsyncAttribute.cs
│   └── Extensions/
│       ├── StateExtensions.cs
│       └── StateMachineExtensions.cs
├── Unity/
│   └── Runtime/
│       ├── Controllers/             # Unity-specific controller interfaces
│       │   ├── IEnterController.cs
│       │   ├── IExitController.cs
│       │   ├── IUpdateController.cs
│       │   ├── ILateUpdateController.cs
│       │   ├── IFixedUpdateController.cs
│       │   ├── IDisposableController.cs
│       │   ├── IAsyncEnterController.cs
│       │   └── IAsyncExitController.cs
│       └── StateMachines/
│           ├── StateMachineBase.cs
│           ├── StateMachineBase.Async.cs
│           ├── MonoStateMachine.cs
│           └── MonoStateMachine.Async.cs
```

### 9.2 Separate Packages

- `Aspid.Core.HSM.VContainer` — VContainer DI scope integration
- `Aspid.Core.HSM.MVVM` — Aspid.MVVM view/viewmodel lifecycle integration

---

## 10. Implementation Phases

### Phase 1: Core (Controller → ControllerGroup → State)
- Controller, ControllerGroup as standalone reusable unit, State
- State hierarchy via `[ParentState]`
- StateMachineBase, MonoStateMachine
- Generators: `[ControllerGroup]` with nesting, `[ParentState]`
- Unit tests

### Phase 2: Async Support
- Async controller interfaces with UniTask
- `[AsyncOf]` pairing
- `[AsyncMode]` per-interface (Sequential/Parallel)
- `ChangeStateAsync`, CancellationToken
- Generator updates
- Tests

### Phase 3: Transition Pipeline
- `ITransition` interface and base implementation
- Guard logic, TransitionState
- Registration and chain fallback
- Async-aware pipeline
- Tests

### Phase 4: Extension States
- `IExtensionState`, `[ExtensionFor]` attribute and generator
- Attach/detach lifecycle, auto-detach on incompatible transition
- Dispatch integration
- Tests

### Phase 5: Scope + VContainer
- `IStateScope` abstraction in core
- `ScopeLifetime` (Transient/Cached)
- StateFactory updates for scope management
- `Aspid.Core.HSM.VContainer` package
- Tests

### Phase 6: Integrations and Polish
- `Aspid.Core.HSM.MVVM` package
- Compile-time diagnostics in generators
- Extension points for config (stubs, not full implementation)
- Samples, documentation

### Phase 7: Claude Tooling (Separate Spec)
- Skills for scaffolding (state/controller/transition creation)
- MCP server for HSM tree introspection
- Agents for refactoring and migration
- Guidelines for XML-docs and CLAUDE.md organization in consumer projects

---

## 11. Claude-First DX Vision

The framework is designed for AI-assisted development. Consumer projects (e.g., CarX Street) will use Claude as the primary development tool for creating and modifying HSM-based architecture.

### Principles

- **XML documentation on concrete controllers** — so Claude understands each controller's purpose
- **CLAUDE.md per feature** — in consumer project feature folders, describing how the feature is organized via HSM
- **Predictable file structure** — one concept per file, naming matches type, so Claude can navigate by filename
- **Scaffolding patterns** — documented patterns for "how to add a new state", "how to add a controller", "how to add a transition"

### Planned Tooling (Separate Spec)

- Claude Code skills for HSM scaffolding
- MCP server exposing the HSM tree, available transitions, registered extensions
- Agents for large-scale migration of existing FSM systems to HSM

---

## 12. Testing Strategy

### Unit Tests
- Controller dispatch (order, reverse, mixed sync/async)
- State transitions (hierarchy navigation, common ancestor detection)
- Transition pipeline (guards, TransitionState lifecycle)
- Extension States (attach, detach, auto-detach, compatibility)
- Scope lifecycle (create, cache, dispose)
- Async modes (Sequential, Parallel, cancellation)

### Generator Tests
- `[ControllerGroup]` emit correctness (nested groups, async modes)
- `[ParentState]` emit correctness
- `[ExtensionFor]` emit correctness
- `[Transition]` emit correctness
- Diagnostic emission (errors, warnings)

### Integration Tests
- Full state machine lifecycle with VContainer scopes
- MVVM lifecycle integration
- CarX Street pilot (DriftFsm migration)
