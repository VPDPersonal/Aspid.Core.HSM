<picture>
  <source srcset="https://vpdpersonal.github.io/VPDPersonal/banners/aspid-core-hsm/banner.webp" type="image/webp" />
  <img src="https://vpdpersonal.github.io/VPDPersonal/banners/aspid-core-hsm/banner.png" alt="Aspid.Core.HSM" width="100%" />
</picture>

<p>
  <a href="#installation"><img src="https://vpdpersonal.github.io/VPDPersonal/badges/aspid-core-hsm/preview/status-badge-unity.svg" alt="Minimum Unity version" /></a>
  <a href="https://github.com/VPDPersonal/Aspid.Core.HSM/releases"><img src="https://vpdpersonal.github.io/VPDPersonal/badges/aspid-core-hsm/status-badge-preview.svg" alt="Preview release" /></a>
  <a href="LICENSE"><img src="https://vpdpersonal.github.io/VPDPersonal/badges/status-badge-license.svg" alt="MIT License" /></a>
</p>

> [!WARNING]
> **Work in progress.** Aspid.Core.HSM is under active development. The public API is not yet stable and may change without notice between releases. Use with care in production.

**Aspid.Core.HSM** is a Roslyn-powered Hierarchical State Machine for Unity, built from small composable abstractions: states with `Enter` / `Exit` hooks, a parent→child hierarchy expressed through the generic `IChildState<TParent>` interface, pluggable per-frame controllers (`IUpdateController`, `IFixedUpdateController`, `ILateUpdateController`, …) aggregated via `[ControllerGroup]`, composites that pick their own leaf (`IEntrySelector`), declarative guarded transitions (`[Transition]`), extension states (`[ExtensionFor]`), state scopes and async enter/exit (`ChangeStateAsync`). `IEnteredController` runs once the transition that entered a state has completed, with a token that lives as long as the state. `MonoStateMachine` wires it all to the Unity `MonoBehaviour` lifecycle.

## Installation

Requires **Unity 6000.0.53f1 or newer**. Install [UniTask](https://github.com/Cysharp/UniTask) first, then Aspid.Core.HSM.

### 1. UniTask

Open **Window → Package Manager**, choose **+ → Install package from git URL…**, paste the URL below and press **Install**. If UniTask is already installed, skip this step.

```text
https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11
```

<details>
<summary>Why install UniTask separately?</summary>

HSM's async enter/exit controllers use UniTask, so the package does not compile without it. UPM does not resolve Git dependencies transitively; install UniTask explicitly before HSM. An existing [OpenUPM](https://openupm.com/packages/com.cysharp.unitask/) installation works as well.

</details>

### 2. Aspid.Core.HSM

<table>
<tr>
<td width="56%"><picture><source srcset="https://vpdpersonal.github.io/VPDPersonal/installation/aspid-core-hsm/preview/install.webp" type="image/webp"><img src="https://vpdpersonal.github.io/VPDPersonal/installation/aspid-core-hsm/preview/install.png" alt="Package Manager installs Aspid.Core.HSM from its git URL" width="100%"></picture></td>
<td width="44%">
<ol>
<li>Open <b>Window → Package Manager</b></li>
<li>Choose <b>+ → Install package from git URL…</b></li>
<li>Paste the URL and press <b>Install</b></li>
</ol>
</td>
</tr>
</table>

```text
https://github.com/VPDPersonal/Aspid.Core.HSM.git#upm-preview
```

The URL installs the latest **preview** release (alpha, beta, rc, …); **Update** in the Package Manager installs the next one. The public API may change between previews.

<details>
<summary>Pin a version and choose a release channel</summary>

To keep a project on a specific preview, use its immutable release tag:

```text
https://github.com/VPDPersonal/Aspid.Core.HSM.git#upm-preview/0.0.1-alpha.7
```

See [Releases](https://github.com/VPDPersonal/Aspid.Core.HSM/releases) for available versions. There is no stable release yet, so use `#upm-preview`. The [Release workflow](.github/workflows/release.yml) creates the `upm` branch and `upm/<version>` tags when the first stable version ships.

</details>

<details>
<summary>Troubleshooting</summary>

**Compile errors mentioning `Cysharp` or `UniTask`.** Install UniTask using the first URL above, then let Unity recompile.

**Unity is older than 6000.0.53f1.** Upgrade to the supported minimum before installing HSM. Check the Editor version in **Help → About Unity** (**Unity → About Unity** on macOS).

</details>

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

A stage that waits for an event after its state is entered implements `IAsyncEnteredController`: the machine starts
`OnEnteredAsync(StateEntry entry)` once the transition is over and owns its outcome. The state exiting, the next
transition being superseded and the machine being disposed stay silent; any other failure goes to the nearest
`IEnteredFailedController` on the stage's state or above it when the stage started, or to the machine's
`OnEnteredFailed`. Leave the state through `entry.ChangeStateAsync<T>()`, so a failure of that transition is reported
even after the state has exited. Once the state has exited, the entry starts no transition.

## License

[MIT](LICENSE).
