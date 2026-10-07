# Changelog

All notable changes to **Aspid.Core.HSM** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Breaking

- **A state is created inside its own scope.** The whole new chain used to be constructed before the first `Enter`, so a state's scope — and its new ancestors' scopes — did not exist yet when its constructor ran, and it could not depend on anything its ancestors registered. States are now created one at a time, right before each is entered: the state's scope is activated first, as a child of the nearest active ancestor's scope (or reused for `[ScopeLifetime(Cached)]`), and the state is resolved from it. A cached scope is disposed together with the parent scope it was created under, so a cached state under a transient parent gets a new scope under the parent's new one on re-entry. `StateFactory.CreateStateInternal(Type)` is replaced by `CreateStateInternal(Type type, IStateScope? scope)`; resolve from `scope` to see the ancestors' registrations. If creation throws, the scope it was given is released. A synchronous change is therefore no longer atomic when a constructor throws: the states below the diverge point have already exited and stay exited, and the chain ends at the last state entered, or at `EmptyState` when none is left. `OnChangedState` and the extension auto-detach now run on that path too, for `ChangeState`, `TransitionTo` and `TransitionVia`. If that cleanup throws as well, the call throws an `AggregateException` with the original exception first, instead of the original exception.
- `StateFactory.CreateState<TState>(activeStates)`, `CreateState(Type, activeStates[, destination])` and `CreateInstance(Type)` are replaced by `BuildTypeChain(Type, List<Type>)`, which resolves the chain of types without creating states, and `CreateState(Type)`, which creates one state in its own scope. `GetParentType(Type)` exposes the declared parent. `MarkInitialized` no longer activates the scope — `CreateState` does.
- **The parent must be declared with `IChildState<TParent>`.** A state implementing only the non-generic `IChildState` used to be instantiated to read its parent; since a state cannot exist before its scope, it now throws `InvalidOperationException` naming the type.
- **An extension declaring `IChildState<TParent>` is scoped under that parent.** Its scope is a child of `TParent`'s scope, so it sees the parent's registrations; it attaches only while `TParent` is in the active chain and is detached before `TParent` exits, while its parent's scope is still alive. Leaf changes inside `TParent` leave it attached. An extension without `IChildState<>` is scoped under the root, as before.

### Added

- **MIT license.** `LICENSE` at the repository root, `LICENSE.md` inside the package, and a `license` field in `package.json`. The first preview shipped without any of these, which by default reserves all rights and blocks shipping a game built on the package.
- `StateMachineBase.IsTransitionEnabled(Type sourceType, Type targetType)` — an edge-level guard receiving both endpoints, complementing the node-level `IsStateEnabled(Type)`. Every state change funnels through it, `ChangeState` included, so it is a usable single point for enforcing edge legality.
- `StateMachineBase.StrictTransitions` — opt-in strict mode. When enabled, `TransitionTo` / `TransitionToAsync` require a registered `ITransition` covering **every** step of the path and throw `InvalidOperationException` naming the missing edge instead of transitioning anyway. `ChangeState` stays outside the check as the deliberate escape hatch.
- `MonoStateMachine` now exposes `IsStateEnabled`, `IsControllerEnabled`, `IsTransitionEnabled` and `StrictTransitions` as `protected virtual` members and forwards them to the internal core. Previously these existed only on `StateMachineBase`, which `MonoStateMachine` composes rather than inherits, so a subclass could not override them at all (CS0115).
- Non-generic `ChangeState(Type)`, `ChangeStateAsync(Type, …)`, `TransitionTo(Type)`, `TransitionVia(Type)`, `TransitionToAsync(Type, …)` and `TransitionViaAsync(Type, …)` overloads, plus `StateFactory.CreateState(Type, …)`.
- **Per-state profiler markers.** Under `ENABLE_PROFILER` (Editor and development builds) the machine samples each state's enter, exit and update / late update / fixed update as `HSM.Enter <State>`, `HSM.Exit <State>`, `HSM.Update <State>` and so on, with the state's full type name and readable generic arguments (`NS.Outer<System.Int32>.Inner`: CLR type names, nested types joined with `.`, each segment with its own arguments). Async enter and exit sample only their synchronous segments, since a profiler sample cannot span an `await`; a state without async controllers gets one sample, the same as a synchronous change.

### Fixed

- **Changing to a leaf at a different depth re-entered the whole chain.** `StateFactory.CreateState` compared the new chain with the active one by index counted from the current leaf, not from the root. When the old and new leaves sat at different depths the indexes never lined up, so every state — including the root and shared ancestors — was exited and entered again. `ChangeState` to an ancestor of the current leaf did the same. The factory now resolves the chain of types first (from `IChildState<T>`, without instantiating), reuses the longest prefix whose types match from the root and creates only the states below it. A change to an ancestor now exits only its descendants. Existing tests missed it because their factories return one shared instance per type, which the reference-based chain diff treats as reused.
- **A `[ControllerGroup]` whose enter threw or was cancelled halfway exited and ticked controllers it never entered.** A state stays in the chain when its enter fails, so it kept calling `Update` on those controllers, and the next transition called their `OnExit` — often on controllers that assume their enter ran. A group with an `IEnterController` / `IAsyncEnterController` path now remembers how far its enter got in `AddControllers` order: exit, update and dispose reach only those controllers (a controller whose own enter threw still gets its exit), the async sequential enter checks the cancellation token before each controller, and a completed exit resets the progress. In parallel async mode every controller is started at once, so the whole group counts as entered. Groups without an enter path are emitted as before.
- **`StateFactory.CreateState` handed out its own chain buffer.** The returned `IReadOnlyList<IState>` was a live reference to a factory field that the next call cleared and rewrote. A `ChangeState` issued from a state's `Enter` / `IEnterController.OnEnter` therefore rewrote the chain the outer `ChangeState` was still iterating, entering states twice and leaving duplicates in `CurrentStates`. Affected the async path as well, where the chain is held across `await`. `CreateState` now returns a list the caller owns, and the machine rents a separate buffer per in-flight transition.
- **Re-entrant `ChangeState` now runs to completion.** A state change requested while another one is still applying is queued and performed once the running change finishes, rather than mutating the chain underneath it. Guards are re-resolved at apply time, so each queued request sees the chain the previous one left behind.
- **A partially registered transition path was treated as a fully registered one.** `TransitionTo` collected whatever segment transitions happened to exist and proceeded; under `StrictTransitions` a partial path is now rejected. Permissive (default) behaviour is unchanged.
- **`ChangeStateAsync` awaited from an async enter/exit callback deadlocked.** The new call waited for the running transition to unwind, and that transition waited for the callback. A cancelled transition now stops waiting for its async callback at once, so the new call supersedes it. An async enter callback can redirect the machine with `await ChangeStateAsync<T>()`.
- **Overlapping `ChangeStateAsync` calls could run two transitions at once.** Calls made while a transition was running all waited for the same unwind, and each then started its own transition. A call made from the first synchronous part of an enter/exit callback did not wait at all. Every call now supersedes the transition that is running when it resumes.
- **An async state change started from a synchronous `Enter` / `OnEnter` corrupted the chain.** It ran inside the running `ChangeState`, released the parent and entered its own target, and the outer loop then added the child under the new root. `ChangeStateAsync`, `TransitionToAsync` and `TransitionViaAsync` now fail with `InvalidOperationException` while a synchronous change is in progress. Use the synchronous methods there: they queue the request.
- **`TransitionTo` and `TransitionVia` checked the guards twice**, the second time after `OnBeforeTransition` had run. A guard that changed its answer there skipped the state change but still called `OnAfterTransition`. The guards now run once, before `OnBeforeTransition`. `TransitionToAsync` and `TransitionViaAsync` check the edge guard and resolve the registered transition after the superseded transition has unwound, against the leaf that it left.
- **`ChangeState` from an update controller corrupted the tick.** `Update`, `LateUpdate` and `FixedUpdate` walked the active chain with `foreach`, so a controller that changed state threw "Collection was modified" and left the rest of the chain unticked; attaching or detaching an extension from a tick did the same to the extension list. A synchronous `ChangeState` / `TransitionTo` / `TransitionVia` requested during a tick is now queued and applied once every state and extension has been ticked, in the order the requests were made, and before the next tick runs. The tick walks both lists by index, so a state or extension removed mid-tick is skipped instead of throwing. Requests made before a controller throws are dropped with the tick.
- **A cancelled or failed `ChangeStateAsync` left the chain half-changed.** Some old states were exited, the state being entered was left half-entered with its scope active, and nothing was released. The transition now rolls back to the point where the old and new chains diverge: every state below it — old states not yet exited and new ones entered so far, including the half-entered one — is exited from the tail with an uncancellable exit and released, leaving only the shared, fully entered ancestors (or `EmptyState` when nothing is shared). `OnChangedState` and the extension auto-detach then run, as after a failed synchronous change, so `OnChangingState`/`OnChangedState` stay paired. The original exception or cancellation is rethrown; if a rollback exit or that cleanup throws as well, all are reported in an `AggregateException`, the original first.
- **An async exit, once started, always completes.** When the detach of its bound extensions, `OnExitingState` or an exit controller is cancelled or throws, `IState.Exit` and `Release` still run and the state leaves the chain; the callbacks that did not get to run are not called again, and `OnExitedState` runs only if `OnExitingState` did. Likewise, an extension whose `OnDetached` or exit throws is still exited, removed and released, and the other extensions being detached with it — bound to the same parent or incompatible with the new leaf — are still detached.
- **The `Game Loop` sample never reached the published package.** `package.json` advertised `Samples~/GameLoop`, but a `*~` pattern in a contributor's global gitignore matched the `Samples~` directory itself, so it was absent from every commit and from the `git subtree split` the release workflow publishes. The repository `.gitignore` now re-includes `~`-suffixed directories.

### Changed

- `TransitionVia` and the async transition entry points no longer dispatch through `MethodInfo.Invoke`; the reflection-based generic dispatch was replaced by the new `Type`-based overloads.
- `ChangeState` now rejects a call made during an async transition before consulting `IsStateEnabled`, so an in-flight async transition throws regardless of the target. Previously a target that `IsStateEnabled` refused returned silently instead.
- README no longer claims UniTask is "pulled in automatically as a package dependency" — UPM does not resolve git dependencies transitively, so it never was. Installation now documents UniTask as an explicit first step with a pinned git URL.
- README no longer documents the `upm` branch and stable install URL as if they existed; they appear when the first non-prerelease version ships.
- A superseded async transition no longer waits for an async enter/exit callback that ignores its `CancellationToken`. The callback keeps running on its own, and the superseding transition starts at once.
- `[ControllerGroup]` names each controller's profiler marker with the controller's full type name, generic arguments included (`Sample.CountdownController<Sample.DrivingState>`), in the same format as the state markers. It used the simple name, so every closing of a generic controller fell under a single marker.

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

[Unreleased]: https://github.com/VPDPersonal/Aspid.Core.HSM/compare/v0.0.1-alpha.1...HEAD
[0.0.1-alpha.1]: https://github.com/VPDPersonal/Aspid.Core.HSM/releases/tag/v0.0.1-alpha.1
