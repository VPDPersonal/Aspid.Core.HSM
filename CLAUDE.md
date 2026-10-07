# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository layout

This repo contains two independent .NET projects that share source files:

- `Aspid.Core.HSM/` — A Unity 6000.4 project that hosts the HSM runtime as a Unity package at `Assets/Plugins/Aspid/Core/HSM/` (package id `com.aspid.core.hsm`). The package contains both the framework source (`Source/`, asmdef `Aspid.Core.HSM`) and Unity-specific runtime types (`Unity/Runtime/`, asmdef `Aspid.Core.HSM.Unity`). The compiled source generator DLL is dropped into the package as `Aspid.Core.HSM.Generators.dll` so Unity picks it up. The package itself promises Unity 6000.0.53f1 (`unity` + `unityRelease` in `package.json`): use no newer Unity API without a `#if UNITY_6000_x_OR_NEWER` guard.
- `Aspid.Core.HSM.Generators/` — A .NET solution (`.slnx`) with the source generator project (`netstandard2.0`, Roslyn incremental generator), a Sample project, and an xUnit test project. The Tests csproj re-includes the runtime `.cs` files from the Unity package via `<Compile Include="..\..\..\Aspid.Core.HSM\Assets\...\*.cs" />` linking, so the runtime can be tested outside Unity.

The generator project depends on `Aspid.Generators.Helper` and `Aspid.Generators.Helper.Unity`, pinned to exact versions in `Aspid.Core.HSM.Generators/Directory.Build.props`. `ILRepack.targets` merges them into the one DLL that ships in the package. Never add `SourceGenerator.Foundations`: its module initializer writes to `Console`, which hangs Unity's compiler server.

## Common commands

Run from `Aspid.Core.HSM.Generators/`:

```bash
dotnet build Aspid.Core.HSM.Generators.slnx
dotnet build Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators -c Release
dotnet test Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests/Aspid.Core.HSM.Generators.Tests.csproj
dotnet test Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests/Aspid.Core.HSM.Generators.Tests.csproj --filter FullyQualifiedName~StateMachineBaseTests
dotnet build Aspid.Core.HSM.Generators/Aspid.Core.HSM.Runtime
node ../scripts/check-package.mjs
```

The Tests, Sample and Runtime projects compile the package sources from `Aspid.Core.HSM.Generators/RuntimeSources.props` by glob: a new runtime file needs no csproj edit. `Aspid.Core.HSM.Runtime` is a compile gate only: it builds the runtime as C# 9 against .NET Standard 2.1, as Unity does, so a newer language feature fails there even when the tests pass. `scripts/check-package.mjs` checks the package for missing sample folders, missing or orphan `.meta` files and a README badge that disagrees with `package.json`.

After modifying the generator, run the Release build above — the `CopyGeneratorToUnityPackage` target in `Aspid.Core.HSM.Generators/.../Directory.Build.targets` drops the merged DLL into `Aspid.Core.HSM/Assets/Plugins/Aspid/Core/HSM/`. Only a Release build copies it: `dotnet test` and other Debug builds never touch the shipped DLL. The build is deterministic, so the same sources give the same bytes on any machine with the SDK from `global.json`. Do not copy by hand and do not add a hook for this. The Unity project itself is built/run from the Unity Editor, not the CLI.

## HSM architecture

The framework is a Hierarchical State Machine driven by composition of small abstractions. Understanding all of these together is required to be productive:

- `IState` — `Enter`/`Exit` hooks (default no-op). `EmptyState` is the initial state used by `StateMachineBase`.
- `IChildState` — exposes `Type ParentState`. User code declares the relationship by implementing the generic `IChildState<TParent>` interface, whose default interface member returns `typeof(TParent)` — no attribute and no generator involved. A root state implements `IState` only (no `IChildState`). The parent is always read from the type via reflection on `IChildState<>` (`StateFactory.GetParentType`), never from an instance; a state implementing only the non-generic `IChildState` throws. An `IExtensionState` may also implement `IChildState<TParent>`: it is then scoped under `TParent`, attaches only while `TParent` is active and is detached before `TParent` exits.
- `IController` — marker. Concrete controllers (e.g. `IUpdateController`, `IEnterController`, `IExitController`, `IFixedUpdateController`, `ILateUpdateController`, `IDisposableController`) are looked up on a state via `state.GetController<T>()` (`StateExtensions.cs`), which uses `is`-pattern: a state is its own controller when it implements the interface directly, or a `[ControllerGroup]` partial class aggregates multiple controllers.
- `StateFactory` / `StateFactory<TState>` — abstract; subclasses implement `CreateStateInternal(Type, IStateScope?)`. `BuildTypeChain` resolves the root→leaf chain of *types* without creating anything; `CreateState(Type)` activates the state's own scope (child of the nearest active ancestor's scope, or a reused `Cached` one) and resolves the state from it. Tracks first-time initialization via `_initializedStates`.
- `StateMachineBase` (`Unity/Runtime`) — holds `_currentStates` (root→leaf list). `ChangeState<T>()` asks the factory for the new type chain, keeps the longest prefix whose types match from the root, exits/releases the rest from the tail, then creates and enters each new state one at a time — a state is created only after its parent has been entered, so its scope's parent is already active. A sync change requested from Enter/Exit or from an update tick is queued (run-to-completion). `Update`/`LateUpdate`/`FixedUpdate` iterate the active chain and dispatch via `GetController<T>()`. `MonoStateMachine` wires this to Unity's MonoBehaviour lifecycle.

Key invariant: state lifetime and parent-chain construction are owned by `StateFactory`; `StateMachineBase` only diffs and enters/exits. When changing one, keep the other's assumptions in mind (e.g. `Release` is called by the machine on exit, and the factory's `_initializedStates` set is what makes "first enter" detectable).

## Source generators

Three incremental generators live in `Aspid.Core.HSM.Generators/`, each triggered via `ForAttributeWithMetadataName` and requiring the target class to be `partial`:

- `ControllersGroupGenerator` (triggered by `[ControllerGroup]`) — emits the controller-aggregation plumbing (e.g. `AddControllers(...)` used in samples) so a single class can dispatch to multiple inner controllers. Interacts with `[ReverseExecute]`, `[AsyncOf]`, and `[AsyncMode]` (`AsyncExecutionMode` Sequential/Parallel) on group methods.
- `TransitionGenerator` (triggered by `[Transition(typeof(Source), typeof(Target))]`) — emits `ITransition.SourceState`/`TargetState`. This is the attribute-based alternative to implementing `ITransition<TSource, TTarget>` by hand (that generic interface supplies the same members via DIM). A non-`partial` transition class is simply skipped, so it must use the generic interface instead.
- `ExtensionStateGenerator` (triggered by `[ExtensionFor(typeof(A), typeof(B), …)]`) — emits `IExtensionState.CanAttachTo` as `hostState is A or B`. A non-`partial` extension implements `CanAttachTo` manually instead.

There is **no** `ChildStateGenerator` and no `[ParentState]` attribute — the parent relationship is expressed purely by the `IChildState<TParent>` interface (see HSM architecture above).

Each generator is split into `Data/` (incremental record), `Factories/` (build the record from `SemanticModel` + `ClassDeclarationSyntax`), and `Bodies/` (emit source). `Descriptions/HsmClasses.cs` and `HsmNamespaces.cs` centralize the framework type names that the generators reference — update these when renaming public attributes/types in the runtime.

When adding a state, implement `IState`, add `IChildState<TParent>` if it has a parent (root states implement `IState` only), and add controller interfaces (`IUpdateController`, `IEnterController`, …) or a `[ControllerGroup] partial class` for multi-controller dispatch. No generator or `partial` is needed for the parent relationship itself. See `Aspid.Core.HSM/Assets/_Scripts/States/RootState.cs` and `SinglePlayerState.cs` for the canonical pattern, `Aspid.Core.HSM/Assets/_Scripts/Transitions/` for `[Transition]`/`ITransition<,>`, `Aspid.Core.HSM/Assets/_Scripts/Extensions/` for `[ExtensionFor]`, and `Aspid.Core.HSM.Generators.Sample/Sample/` for `[ControllerGroup]` usage.

## Claude Code setup

- `.claude/settings.json` allows `dotnet test`, `dotnet build`, `node scripts/check-*` and read-only `gh pr`/`gh run` commands without a prompt.
- `.claude/settings.json` blocks `Edit`/`Write` on `*.meta` (Unity-managed) and `Aspid.Core.HSM.Generators.dll` (build artifact) via `PreToolUse`. Don't try to bypass — fix the source instead. The hook reads `file_path` with `sed`, not a JSON parser: it matches only the path suffix, and `$p` is not the decoded path.
- `.claude/skills/rebuild-generator` — user-invoked rebuild; copy is automatic via `Directory.Build.targets`.
- `.claude/skills/gen-snapshot-test` — template for generator tests under `Aspid.Core.HSM.Generators.Tests/GeneratorTests/` that run the generator with `CSharpGeneratorDriver`.
- `.claude/skills/asp-branch`, `asp-commit`, `asp-pr` — branch names, commits and pull requests. Use them for every commit and PR, so that all contributors follow one format. `asp-pr` reads repo-specific rules (scopes, labels, review loop) from `.claude/asp-pr.md` when that file exists. `asp-commit` and `asp-pr` run `sh` scripts and need `git` and an authenticated `gh`. On Windows, install Git for Windows: Claude Code then runs them in Git Bash.
- `.claude/skills/asp-xmldoc` — XML docs (`///`) conventions for public C# API. It loads before you write a `///` comment.
- `.mcp.json` ships `context7` (Roslyn/Unity docs) and `github` (needs `GITHUB_PERSONAL_ACCESS_TOKEN`).
- `.github/workflows/claude.yml` runs `anthropics/claude-code-action` in two jobs:
  - `review`: one automatic review when a PR opens or leaves draft;
  - `mention`: replies to `@claude` comments in PRs and issues.

  Edit the review rules and the `Verdict: <N> blocking, <M> minor` format in `.github/claude-review.md`.
  The jobs need the `CLAUDE_CODE_OAUTH_TOKEN` repository secret.
- `.claude/asp-pr.md` — PR rules for the `asp-pr` skill: types, scopes and the review loop.
- `.github/workflows/tests.yml` runs on every PR and push to `main`: the .NET tests, the C# 9 runtime build, `scripts/check-package.mjs`, `scripts/check-skills.mjs`, and a Unity job that compiles the package and its sample on the minimum Unity (6000.0.53f1) in a throwaway project (`scripts/make-unity-test-project.sh`) and runs the EditMode tests. `Unity plan` starts it only when the package or its setup changed; the `Unity` job is the one check to require. GameCI activates Unity with the `UNITY_EMAIL` and `UNITY_PASSWORD` secrets; without them the Unity job is skipped on PRs and fails a release. Do not add a `UNITY_LICENSE` secret: the `.ulf` is bound to the Hub machine and breaks the activation on 6000.0.53f1.
- `.github/workflows/pr-checks.yml` checks the PR title against the types and scopes in `.claude/asp-pr.md` and sets the `type:*`, `area:*` and `breaking-change` labels. Keep the two lists in sync.
- `.github/dependabot.yml` updates GitHub Actions weekly. NuGet stays manual: Roslyn versions are pinned to what Unity ships.
