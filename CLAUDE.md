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

After modifying the generator, the produced DLL must be copied to `Aspid.Core.HSM/Assets/Plugins/Aspid/Core/HSM/Aspid.Core.HSM.Generators.dll` for the Unity project to pick up changes (recent commit `70abeec` was this kind of rebuild). The Unity project itself is built/run from the Unity Editor, not the CLI.

## HSM architecture

The framework is a Hierarchical State Machine driven by composition of small abstractions. Understanding all of these together is required to be productive:

- `IState` — `Enter`/`Exit` hooks (default no-op). `EmptyState` is the initial state used by `StateMachineBase`.
- `IChildState` — exposes `Type ParentState`. Implemented automatically by the generator (see below); user code marks the relationship with `[ParentState(typeof(Parent))]` and the generator emits the `IChildState` implementation. `[ParentState(null)]` marks a root state.
- `IController` — marker. Concrete controllers (e.g. `IUpdateController`, `IEnterController`, `IExitController`, `IFixedUpdateController`, `ILateUpdateController`, `IDisposableController`) are looked up on a state via `state.GetController<T>()` (`StateExtensions.cs`), which uses `is`-pattern: a state is its own controller when it implements the interface directly, or a `[ControllerGroup]` partial class aggregates multiple controllers.
- `StateFactory` / `StateFactory<TState>` — abstract; subclasses implement `CreateStateInternal(Type)`. The factory walks `IChildState.ParentState` chains to materialize the full path from root to the requested leaf state, reusing already-active states when their type matches at the same depth, and tracks first-time initialization via `_initializedStates`.
- `StateMachineBase` (`Unity/Runtime`) — holds `_currentStates` (root→leaf list). `ChangeState<T>()` asks the factory for the new chain, diffs it against the active list, exits/releases removed states from the tail, then enters added states. `Update`/`LateUpdate`/`FixedUpdate` iterate the active chain and dispatch via `GetController<T>()`. `MonoStateMachine` wires this to Unity's MonoBehaviour lifecycle.

Key invariant: state lifetime and parent-chain construction are owned by `StateFactory`; `StateMachineBase` only diffs and enters/exits. When changing one, keep the other's assumptions in mind (e.g. `Release` is called by the machine on exit, and the factory's `_initializedStates` set is what makes "first enter" detectable).

## Source generators

Two incremental generators live in `Aspid.Core.HSM.Generators/`:

- `ChildStateGenerator` (triggered by `[ParentState]`) — emits `IChildState` (or root-state equivalent) on a `partial class`. Class must be `partial` and non-`static`.
- `ControllersGroupGenerator` (triggered by `[ControllerGroup]`) — emits the controller-aggregation plumbing (e.g. `AddControllers(...)` used in samples) so a single class can dispatch to multiple inner controllers. The aggregator interacts with `[ReverseExecuteAttribute]` and `[AsyncAttribute]` on group methods.

Each generator is split into `Data/` (incremental record), `Factories/` (build the record from `SemanticModel` + `ClassDeclarationSyntax`), and `Bodies/` (emit source). `Descriptions/HsmClasses.cs` and `HsmNamespaces.cs` centralize the framework type names that the generators reference — update these when renaming public attributes/types in the runtime.

When adding a state, mark it `partial`, apply `[ParentState(typeof(...))]` (or `[ParentState(null)]` for root), and the `IChildState` implementation is generated. See `Aspid.Core.HSM/Assets/_Scripts/States/RootState.cs` and `SinglePlayerState.cs` for the canonical pattern, and `Aspid.Core.HSM.Generators.Sample/Sample/` for `[ControllerGroup]` usage.
