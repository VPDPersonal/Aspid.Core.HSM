# Aspid.Core.HSM

<p>
  <img src="https://img.shields.io/badge/Unity_6000.0%2B-000000?style=flat&logo=unity&logoColor=white&color=4fa35d" alt="Unity 6000.0+" />
  <a href="https://github.com/VPDPersonal/Aspid.Core.HSM/releases"><img src="https://img.shields.io/github/package-json/v/VPDPersonal/Aspid.Core.HSM/upm-preview?label=Preview&labelColor=4d4425&color=a3923d" alt="Preview" /></a>
  <img src="https://img.shields.io/badge/License-MIT-000000?style=flat&labelColor=254d2c&color=4fa35d" alt="MIT" />
</p>

> [!WARNING]
> **Work in progress.** Aspid.Core.HSM is under active development. The public API is not yet stable and may change without notice between releases. Use with care in production.

**Aspid.Core.HSM** is a Roslyn-powered Hierarchical State Machine for Unity, built from small composable abstractions: states with `Enter` / `Exit` hooks, a parent→child hierarchy expressed through the generic `IChildState<TParent>` interface, pluggable per-frame controllers (`IUpdateController`, `IFixedUpdateController`, `ILateUpdateController`, …) aggregated via `[ControllerGroup]`, declarative guarded transitions (`[Transition]`), extension states (`[ExtensionFor]`), state scopes and async enter/exit (`ChangeStateAsync`). `IEnteredController` runs once the transition that entered a state has completed, with a token that lives as long as the state. `MonoStateMachine` wires it all to the Unity `MonoBehaviour` lifecycle.

## Integration

Aspid.Core.HSM needs two packages installed: [UniTask](https://github.com/Cysharp/UniTask) first, then the HSM itself. In the Package Manager click **+ → Install package from git URL…** and paste each URL below.

### 1. UniTask

The async enter/exit controllers are built on UniTask, so the package does not compile without it.

```
https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11
```

> **Why this is a manual step.** UPM does not resolve git dependencies transitively — a `dependencies` entry pointing at a git URL is simply ignored for anyone installing this package. Declaring UniTask in the manifest would therefore not install it, so it is installed explicitly instead. If you already get UniTask from the [OpenUPM](https://openupm.com/packages/com.cysharp.unitask/) scoped registry, that installation works just as well; nothing here depends on how UniTask arrived.

### 2. Aspid.Core.HSM

The `upm-preview` branch always points to the latest **preview** release (alpha, beta, rc, …):

```
https://github.com/VPDPersonal/Aspid.Core.HSM.git#upm-preview
```

To pin a specific preview version, target the immutable per-release tag (see [Releases](https://github.com/VPDPersonal/Aspid.Core.HSM/releases) for the list of available versions):

```
https://github.com/VPDPersonal/Aspid.Core.HSM.git#upm-preview/0.0.1-alpha.3
```

> **Note.** There is no stable release yet, so the `upm` branch does not exist. The [Release workflow](.github/workflows/release.yml) creates it — along with `upm/<version>` tags and a `#upm` install URL — when the first non-prerelease version ships.

### Game Loop sample

Import it from the package's **Samples** tab. Its scripts read input through the [Input System](https://docs.unity3d.com/Packages/com.unity.inputsystem@latest), so install `com.unity.inputsystem` from the Unity Registry first, or the sample does not compile.

## Why an async transition ended

`ChangeStateAsync`, `TransitionToAsync` and `TransitionViaAsync` tell why they ended without completing. Every case is an
`OperationCanceledException`, so an existing `catch (OperationCanceledException)` keeps catching all of them. The cause is
recorded when the transition is cancelled, not guessed from the machine's state when the exception arrives.

| Exception | Meaning | Usual handling |
|---|---|---|
| `TransitionSupersededException` | A newer transition took over; `SupersedingState` names its target, `RollbackExceptions` holds what the rollback threw | Nothing: the newer transition owns the machine |
| `StateMachineDisposedException` | The machine was disposed | Nothing: the machine is gone |
| `OperationCanceledException` (exactly) | The caller's token, or an enter/exit callback cancelled itself (a load gave up) | A failure: apply your error policy |
| any other exception | An enter, exit or hook threw | A failure: apply your error policy |

`AsyncEnteredController` packages this for a stage that waits for an event after its state is entered: it runs
`RunAsync(stateLifetime)`, stays silent when the state exits, the next transition is superseded or the machine is
disposed, and passes everything else to `OnFailed`.

## License

[MIT](LICENSE).
