# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository layout

This repo contains two independent .NET projects that share source files:

- `Aspid.Core.HSM/` — A Unity 2022.3 project that hosts the HSM runtime as a Unity package at `Assets/Plugins/Aspid/Core/HSM/` (package id `com.aspid.core.hsm`). The package contains both the framework source (`Source/`, asmdef `Aspid.Core.HSM`) and Unity-specific runtime types (`Unity/Runtime/`, asmdef `Aspid.Core.HSM.Unity`). The compiled source generator DLL is dropped into the package as `Aspid.Core.HSM.Generators.dll` so Unity picks it up.
- `Aspid.Core.HSM.Generators/` — A .NET solution (`.slnx`) with the source generator project (`netstandard2.0`, Roslyn incremental generator), a Sample project, and an xUnit test project. The Tests csproj re-includes the runtime `.cs` files from the Unity package via `<Compile Include="..\..\..\Aspid.Core.HSM\Assets\...\*.cs" />` linking, so the runtime can be tested outside Unity.

The generator project depends on `SourceGenerator.Foundations`, `Aspid.Generators.Helper`, and `Aspid.Generators.Helper.Unity`.

## Common commands

Run from `Aspid.Core.HSM.Generators/`:

```bash
dotnet build Aspid.Core.HSM.Generators.slnx
dotnet test Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests/Aspid.Core.HSM.Generators.Tests.csproj
dotnet test Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests/Aspid.Core.HSM.Generators.Tests.csproj --filter FullyQualifiedName~StateMachineBaseTests
```

After modifying the generator, just run `dotnet build` — the `CopyGeneratorToUnityPackage` target in `Directory.Build.targets` drops the DLL into the Unity package. Do not copy by hand.

## HSM architecture

### Type hierarchy

```
IController (marker)
  ├── IEnterController        → OnEnter()
  ├── IExitController         → [ReverseExecute] OnExit()
  ├── IUpdateController       → Update(float deltaTime)
  ├── ILateUpdateController   → LateUpdate(float deltaTime)
  ├── IFixedUpdateController  → FixedUpdate(float deltaTime)
  ├── IDisposableController   → [ReverseExecute] Dispose()
  ├── IAsyncEnterController   → [AsyncOf(IEnterController)] OnEnterAsync(CancellationToken)
  └── IAsyncExitController    → [AsyncOf(IExitController)] [ReverseExecute] OnExitAsync(CancellationToken)

Controller → ControllerGroup → State
```

**Controller** — smallest unit of logic. Implements one or more controller interfaces. Knows nothing about state machine or states.

**ControllerGroup** — Composite pattern. Itself an `IController`, aggregates child controllers and/or other ControllerGroups. `[ControllerGroup]` generator implements all controller interfaces from children, delegating calls. Can be reused across states.

**State** — ControllerGroup with `Enter()`/`Exit()`, parent hierarchy via `IChildState<TParent>`, unit of DI Scope. When parent state has children, parent does NOT exit — its controllers keep running.

### Transition pipeline

`ITransition` wraps state changes with a pipeline: Guard → Before → Exit → Enter → After.

```csharp
// Define a transition
[Transition(typeof(FreerideState), typeof(RaceState))]
public partial class FreerideToRace : ITransition
{
    public bool CanTransition() => /* guard logic */;
    public void OnBeforeTransition() { }
    public void OnAfterTransition() { }
}

// Register and use
stateMachine.RegisterTransition(new FreerideToRace());
stateMachine.TransitionTo<RaceState>();      // lookup by target
stateMachine.TransitionVia<FreerideToRace>(); // lookup by type
```

**Resolution:** Direct transition first → chain fallback (compose segment transitions along state tree path, all guards must pass before any exit).

`ResolveTransition(Type source, Type target)` is virtual — override for config-driven transition replacement.

### Extension States

Dynamic mixin states that attach/detach at runtime:

```csharp
[ExtensionFor(typeof(FreerideState), typeof(RaceState))]
[ControllerGroup]
public partial class MiniGameExtension : IExtensionState
{
    public MiniGameExtension() { AddControllers(new TimerController()); }
}

stateMachine.AttachExtension<MiniGameExtension>();  // checks CanAttachTo
stateMachine.DetachExtension<MiniGameExtension>();
```

Auto-detach on incompatible transition. Dispatch order: host states first → extensions in attachment order.

### Async support

Two execution modes configured per-interface via `[AsyncMode]`:

```csharp
[ControllerGroup]
[AsyncMode(typeof(IAsyncEnterController), AsyncExecutionMode.Sequential)]
[AsyncMode(typeof(IAsyncExitController), AsyncExecutionMode.Parallel)]
public partial class GameplayState : IState { ... }
```

- **Parallel** (default): `UniTask.WhenAll(...)` for 2+ async controllers
- **Sequential**: individual `await` per controller

Mixed sync/async: generator detects per-controller, sync called inline, async awaited.

### Scope management

Each state gets an `IStateScope`. Child states inherit parent scopes.

```csharp
factory.SetRootScope(rootScope);  // enables scope creation
var scope = factory.GetScope<GameplayState>();  // get active scope

[ScopeLifetime(ScopeLifetime.Cached)]  // survives Exit, reused on re-enter
public partial class GlobalMapState : IState { ... }
```

`CreateScopeForState(Type, IStateScope?)` is virtual — override in VContainer/Zenject integration.

### Extension points

```csharp
protected virtual bool IsControllerEnabled(IController controller, IState state) => true;
protected virtual bool IsStateEnabled(Type stateType) => true;
protected virtual ITransition? ResolveTransition(Type source, Type target) => /* registry lookup */;
```

Override to implement config-driven controller/state disabling or transition replacement.

## Source generators

Three incremental generators live in `Aspid.Core.HSM.Generators/`, each triggered via `ForAttributeWithMetadataName` and requiring the target class to be `partial`:

- `ControllersGroupGenerator` (triggered by `[ControllerGroup]`) — emits the controller-aggregation plumbing (e.g. `AddControllers(...)` used in samples) so a single class can dispatch to multiple inner controllers. Interacts with `[ReverseExecute]`, `[AsyncOf]`, and `[AsyncMode]` (`AsyncExecutionMode` Sequential/Parallel) on group methods.
- `TransitionGenerator` (triggered by `[Transition(typeof(Source), typeof(Target))]`) — emits `ITransition.SourceState`/`TargetState`. This is the attribute-based alternative to implementing `ITransition<TSource, TTarget>` by hand (that generic interface supplies the same members via DIM). A non-`partial` transition class is simply skipped, so it must use the generic interface instead.
- `ExtensionStateGenerator` (triggered by `[ExtensionFor(typeof(A), typeof(B), …)]`) — emits `IExtensionState.CanAttachTo` as `hostState is A or B`. A non-`partial` extension implements `CanAttachTo` manually instead.

There is **no** `ChildStateGenerator` and no `[ParentState]` attribute — the parent relationship is expressed purely by the `IChildState<TParent>` interface (see HSM architecture above).

Each follows the pattern: `Data/` (record) → `Factories/` (extract from SemanticModel) → `Bodies/` (emit source). `Descriptions/HsmClasses.cs` centralizes type names — update when renaming.

## File layout

```
Aspid.Core.HSM/Assets/Plugins/Aspid/Core/HSM/
├── Source/                              # Core (no Unity deps)
│   ├── Controllers/IController.cs       # Marker interface
│   ├── States/
│   │   ├── IState.cs                    # Enter/Exit
│   │   ├── IChildState.cs               # IChildState<TParent> → ParentState
│   │   ├── IExtensionState.cs           # Dynamic mixin states
│   │   ├── IStateScope.cs               # DI scope abstraction
│   │   └── EmptyState.cs                # Initial state
│   ├── IStateMachine.cs                 # Public API
│   ├── ITransition.cs                   # Transition pipeline
│   ├── ScopeLifetime.cs                 # Transient/Cached enum
│   ├── StateFactory.cs                  # State + scope lifecycle
│   ├── Generation/                      # Attributes for generators
│   │   ├── ControllerGroupAttribute.cs
│   │   ├── TransitionAttribute.cs
│   │   ├── ExtensionForAttribute.cs
│   │   ├── AsyncOfAttribute.cs
│   │   ├── AsyncModeAttribute.cs
│   │   ├── AsyncExecutionMode.cs
│   │   ├── ScopeLifetimeAttribute.cs
│   │   ├── ReverseExecuteAttribute.cs
│   │   └── AsyncAttribute.cs
│   └── Extensions/
│       ├── StateExtensions.cs           # GetController<T>()
│       └── StateMachineExtensions.cs    # GetParentState/GetChildState
└── Unity/Runtime/
    ├── Controllers/                     # Controller interfaces (8 files)
    └── StateMachines/
        ├── StateMachineBase.cs          # Core: ChangeState, Update, Enter/Exit
        ├── StateMachineBase.Async.cs    # ChangeStateAsync
        ├── StateMachineBase.Transitions.cs  # TransitionTo/Via, registry, chain
        ├── StateMachineBase.Extensions.cs   # Attach/Detach extensions
        ├── MonoStateMachine.cs          # Unity MonoBehaviour wrapper
        ├── MonoStateMachine.Async.cs
        └── MonoStateMachineCore.cs      # Internal bridge
```

## Common patterns

### Adding a new state

```csharp
[ControllerGroup]
public partial class NewState : IState, IChildState<GameplayState>  // root state: IState only
{
    public NewState()
    {
        AddControllers(new SomeController(), new AnotherController());
    }
}
```

When adding a state, implement `IState`, add `IChildState<TParent>` if it has a parent (root states implement `IState` only), and add controller interfaces (`IUpdateController`, `IEnterController`, …) or a `[ControllerGroup] partial class` for multi-controller dispatch. No generator or `partial` is needed for the parent relationship itself. See `Aspid.Core.HSM/Assets/_Scripts/States/RootState.cs` and `SinglePlayerState.cs` for the canonical pattern, `Aspid.Core.HSM/Assets/_Scripts/Transitions/` for `[Transition]`/`ITransition<,>`, `Aspid.Core.HSM/Assets/_Scripts/Extensions/` for `[ExtensionFor]`, and `Aspid.Core.HSM.Generators.Sample/Sample/` for `[ControllerGroup]` usage.

### Adding a new controller

```csharp
public class PlayerMovementController : IUpdateController, IEnterController
{
    void IEnterController.OnEnter() { /* init */ }
    void IUpdateController.Update(float dt) { /* move */ }
}
```

### Adding a new transition

```csharp
[Transition(typeof(SourceState), typeof(TargetState))]
public partial class SourceToTarget : ITransition
{
    public bool CanTransition() => true;
    public void OnBeforeTransition() { }
    public void OnAfterTransition() { }
}
```

Register: `stateMachine.RegisterTransition(new SourceToTarget());`

### Adding an extension state

```csharp
[ExtensionFor(typeof(FreerideState), typeof(RaceState))]
[ControllerGroup]
public partial class MiniGameExtension : IExtensionState
{
    public MiniGameExtension()
    {
        AddControllers(new MiniGameController());
    }
}
```

## Claude Code setup

- `.claude/settings.json` blocks `Edit`/`Write` on `*.meta` (Unity-managed) and `Aspid.Core.HSM.Generators.dll` (build artifact) via `PreToolUse`. Don't try to bypass — fix the source instead. The hook reads `file_path` with `sed`, not a JSON parser: it matches only the path suffix, and `$p` is not the decoded path.
- `.claude/skills/rebuild-generator` — user-invoked rebuild; copy is automatic via `Directory.Build.targets`.
- `.claude/skills/gen-snapshot-test` — template for `CSharpSourceGeneratorTest<TGenerator, XUnitVerifier>` tests under `Aspid.Core.HSM.Generators.Tests/`.
- `.claude/skills/asp-branch`, `asp-commit`, `asp-pr` — branch names, commits and pull requests. Use them for every commit and PR, so that all contributors follow one format. `asp-pr` reads repo-specific rules (scopes, labels, review loop) from `.claude/asp-pr.md` when that file exists. `asp-commit` and `asp-pr` run `sh` scripts and need `git` and an authenticated `gh`. On Windows, install Git for Windows: Claude Code then runs them in Git Bash.
- `.claude/skills/asp-xmldoc` — XML docs (`///`) conventions for public C# API. It loads before you write a `///` comment.
- `.claude/skills/create-state` — scaffold a new HSM state.
- `.claude/skills/create-controller` — scaffold a new controller.
- `.claude/skills/create-transition` — scaffold a new transition.
- `.claude/skills/create-extension` — scaffold a new extension state.
- `.mcp.json` ships `context7` (Roslyn/Unity docs), `github` (needs `GITHUB_PERSONAL_ACCESS_TOKEN`) and `hsm-analyzer` (HSM tree introspection).
- `.github/workflows/claude.yml` runs `anthropics/claude-code-action` in two jobs:
  - `review`: one automatic review when a PR opens or leaves draft;
  - `mention`: replies to `@claude` comments in PRs and issues.

  Edit the review rules and the `Verdict: <N> blocking, <M> minor` format in `.github/claude-review.md`.
  The jobs need the `CLAUDE_CODE_OAUTH_TOKEN` repository secret.
- `.claude/asp-pr.md` — PR rules for the `asp-pr` skill: types, scopes and the review loop.
