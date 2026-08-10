# Changelog

All notable changes to **Aspid.Core.HSM** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.0.1-alpha.2] — 2026-08-10

Consumer-reported fixes from the first preview: the package is now legally installable, honest about its
dependency, and no longer corrupts its state chain when a state redirects from its own `Enter`.

### Added

- **MIT license.** `LICENSE` at the repository root, `LICENSE.md` inside the package, and a `license` field in `package.json`. The first preview shipped without any of these, which by default reserves all rights and blocks shipping a game built on the package.
- `StateMachineBase.IsTransitionEnabled(Type sourceType, Type targetType)` — an edge-level guard receiving both endpoints, complementing the node-level `IsStateEnabled(Type)`. Every state change funnels through it, `ChangeState` included, so it is a usable single point for enforcing edge legality.
- `StateMachineBase.StrictTransitions` — opt-in strict mode. When enabled, `TransitionTo` / `TransitionToAsync` require a registered `ITransition` covering **every** step of the path and throw `InvalidOperationException` naming the missing edge instead of transitioning anyway. `ChangeState` stays outside the check as the deliberate escape hatch.
- `MonoStateMachine` now exposes `IsStateEnabled`, `IsControllerEnabled`, `IsTransitionEnabled` and `StrictTransitions` as `protected virtual` members and forwards them to the internal core. Previously these existed only on `StateMachineBase`, which `MonoStateMachine` composes rather than inherits, so a subclass could not override them at all (CS0115).
- Non-generic `ChangeState(Type)`, `ChangeStateAsync(Type, …)`, `TransitionTo(Type)`, `TransitionVia(Type)`, `TransitionToAsync(Type, …)` and `TransitionViaAsync(Type, …)` overloads, plus `StateFactory.CreateState(Type, …)`.

### Fixed

- **`StateFactory.CreateState` handed out its own chain buffer.** The returned `IReadOnlyList<IState>` was a live reference to a factory field that the next call cleared and rewrote. A `ChangeState` issued from a state's `Enter` / `IEnterController.OnEnter` therefore rewrote the chain the outer `ChangeState` was still iterating, entering states twice and leaving duplicates in `CurrentStates`. Affected the async path as well, where the chain is held across `await`. `CreateState` now returns a list the caller owns, and the machine rents a separate buffer per in-flight transition.
- **Re-entrant `ChangeState` now runs to completion.** A state change requested while another one is still applying is queued and performed once the running change finishes, rather than mutating the chain underneath it. Guards are re-resolved at apply time, so each queued request sees the chain the previous one left behind.
- **A partially registered transition path was treated as a fully registered one.** `TransitionTo` collected whatever segment transitions happened to exist and proceeded; under `StrictTransitions` a partial path is now rejected. Permissive (default) behaviour is unchanged.
- **`ChangeStateAsync` called from an async enter/exit callback deadlocked**, waiting on the very transition it was running inside. It now throws `InvalidOperationException` explaining the situation.
- **The `Game Loop` sample never reached the published package.** `package.json` advertised `Samples~/GameLoop`, but a `*~` pattern in a contributor's global gitignore matched the `Samples~` directory itself, so it was absent from every commit and from the `git subtree split` the release workflow publishes. The repository `.gitignore` now re-includes `~`-suffixed directories.

### Changed

- `TransitionVia` and the async transition entry points no longer dispatch through `MethodInfo.Invoke`; the reflection-based generic dispatch was replaced by the new `Type`-based overloads.
- `ChangeState` now rejects a call made during an async transition before consulting `IsStateEnabled`, so an in-flight async transition throws regardless of the target. Previously a target that `IsStateEnabled` refused returned silently instead.
- README no longer claims UniTask is "pulled in automatically as a package dependency" — UPM does not resolve git dependencies transitively, so it never was. Installation now documents UniTask as an explicit first step with a pinned git URL.
- README no longer documents the `upm` branch and stable install URL as if they existed; they appear when the first non-prerelease version ships.

## [0.0.1-alpha.1] — 2026-07-08

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

[Unreleased]: https://github.com/VPDPersonal/Aspid.Core.HSM/compare/v0.0.1-alpha.2...HEAD
[0.0.1-alpha.2]: https://github.com/VPDPersonal/Aspid.Core.HSM/compare/v0.0.1-alpha.1...v0.0.1-alpha.2
[0.0.1-alpha.1]: https://github.com/VPDPersonal/Aspid.Core.HSM/releases/tag/v0.0.1-alpha.1
