# Aspid.Core.HSM

<p>
  <img src="https://img.shields.io/badge/Unity_2022.3%2B-000000?style=flat&logo=unity&logoColor=white&color=4fa35d" alt="Unity 2022.3+" />
  <a href="https://github.com/VPDPersonal/Aspid.Core.HSM/releases"><img src="https://img.shields.io/github/package-json/v/VPDPersonal/Aspid.Core.HSM/upm?label=Stable&labelColor=254d2c&color=4fa35d" alt="Stable" /></a>
  <a href="https://github.com/VPDPersonal/Aspid.Core.HSM/releases"><img src="https://img.shields.io/github/package-json/v/VPDPersonal/Aspid.Core.HSM/upm-preview?label=Preview&labelColor=4d4425&color=a3923d" alt="Preview" /></a>
</p>

> [!WARNING]
> **Work in progress.** Aspid.Core.HSM is under active development. The public API is not yet stable and may change without notice between releases. Use with care in production.

**Aspid.Core.HSM** is a Roslyn-powered Hierarchical State Machine for Unity, built from small composable abstractions: states with `Enter` / `Exit` hooks, a parent→child hierarchy expressed through the generic `IChildState<TParent>` interface, pluggable per-frame controllers (`IUpdateController`, `IFixedUpdateController`, `ILateUpdateController`, …) aggregated via `[ControllerGroup]`, declarative guarded transitions (`[Transition]`), extension states (`[ExtensionFor]`), state scopes and async enter/exit. `MonoStateMachine` wires it all to the Unity `MonoBehaviour` lifecycle.

## Integration

Install Aspid.Core.HSM via UPM: in the Package Manager click **+ → Install package from git URL…** and paste one of the URLs below.

### Stable

The `upm` branch always points to the latest **stable** release:

```
https://github.com/VPDPersonal/Aspid.Core.HSM.git#upm
```

To install a specific version, target the immutable per-release tag (see [Releases](https://github.com/VPDPersonal/Aspid.Core.HSM/releases) for the list of available versions):

```
https://github.com/VPDPersonal/Aspid.Core.HSM.git#upm/0.0.1
```

<details>
<summary><strong>Preview</strong></summary>

<br>

The `upm-preview` branch always points to the latest **preview** release (rc, beta, alpha, …):

```
https://github.com/VPDPersonal/Aspid.Core.HSM.git#upm-preview
```

To install a specific preview version, target the immutable per-release tag (see [Releases](https://github.com/VPDPersonal/Aspid.Core.HSM/releases) for the list of available versions):

```
https://github.com/VPDPersonal/Aspid.Core.HSM.git#upm-preview/0.0.1-rc.1
```

</details>

> **Note.** The `upm` / `upm-preview` branches and their badges appear once the [Release workflow](.github/workflows/release.yml) publishes the first stable / preview version.

## Dependency

Aspid.Core.HSM depends on [UniTask](https://github.com/Cysharp/UniTask) (for async enter/exit controllers), pulled in automatically as a package dependency.
