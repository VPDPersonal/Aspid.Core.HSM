# Changelog

All notable changes to **Aspid.Core.HSM** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.0.1] — 2026-07-08

Initial preview release of **Aspid.Core.HSM** — a Roslyn-powered Hierarchical State Machine for Unity 2022.3+, distributed as the UPM package `com.aspid.core.hsm`. The public API and generated boilerplate may still change before the first stable release.

### Added

#### Core state model
- `IState` with `Enter` / `Exit` hooks (default no-op) and `EmptyState` as the machine's initial state.
- `IChildState` / `IChildState<TParent>` expressing the parent→child hierarchy: a child state implements `IChildState<TParent>`, whose default interface member returns `typeof(TParent)` as `ParentState` — no attribute and no generator involved. A root state implements `IState` only.
- `IExtensionState` / `[ExtensionFor]` for composing extra behaviour onto an existing state without subclassing.
- `IStateScope` and `ScopeLifetime` / `[ScopeLifetime]` for scoping resources to a state's active lifetime.

#### Controllers
- Marker `IController` plus concrete controllers dispatched per active state: `IEnterController`, `IExitController`, `IUpdateController`, `IFixedUpdateController`, `ILateUpdateController`, `IDisposableController`, and the async `IAsyncEnterController` / `IAsyncExitController`.
- `[ControllerGroup]` aggregation so a single `partial` class can dispatch to multiple inner controllers, with `[ReverseExecute]`, `[Async]`, `[AsyncMode]` / `AsyncExecutionMode` and `[AsyncOf]` controlling execution order and async behaviour of group methods.
- `state.GetController<T>()` lookup (`StateExtensions`) resolving a controller from the state itself or its aggregated group.

#### State machine
- `IStateMachine`, `StateFactory` / `StateFactory<TState>` — materialize the full root→leaf state path from `IChildState.ParentState` chains, reusing already-active states at matching depth and tracking first-time initialization.
- `StateMachineBase` — holds the active root→leaf chain, `ChangeState<T>()` diffs the new chain against the active one (exit/release the tail, enter added states), and `Update` / `FixedUpdate` / `LateUpdate` dispatch through the active chain.
- `ITransition` / `[Transition]` and `StateMachineBase` transition support for declarative, guarded state transitions.
- `MonoStateMachine` wiring the machine (including its async enter/exit path) to the Unity `MonoBehaviour` lifecycle.

#### Source generators
Three Roslyn incremental generators, each triggered via an attribute on a `partial` class (a non-`partial` target is skipped and implements the interface by hand instead):
- `ControllersGroupGenerator` (`[ControllerGroup]`) — emits the controller-aggregation plumbing, honouring `[ReverseExecute]`, `[AsyncOf]` and `[AsyncMode]` (`AsyncExecutionMode`) on group methods.
- `TransitionGenerator` (`[Transition(typeof(Source), typeof(Target))]`) — emits `ITransition.SourceState` / `TargetState`; the attribute-based alternative to implementing `ITransition<TSource, TTarget>` by hand.
- `ExtensionStateGenerator` (`[ExtensionFor(typeof(A), typeof(B), …)]`) — emits `IExtensionState.CanAttachTo` as `hostState is A or B`.
- All ship precompiled in the package as `Aspid.Core.HSM.Generators.dll` so Unity picks them up without a separate build.

#### Package
- `Aspid.Core.HSM` (framework) and `Aspid.Core.HSM.Unity` (Unity runtime) assemblies.
- Dependency on [UniTask](https://github.com/Cysharp/UniTask) for the async enter/exit controllers.
- **Game Loop** sample: a full state hierarchy with guarded transitions, async loading, extensions, scopes and extension points.

[Unreleased]: https://github.com/VPDPersonal/Aspid.Core.HSM/compare/v0.0.1...HEAD
[0.0.1]: https://github.com/VPDPersonal/Aspid.Core.HSM/releases/tag/v0.0.1
