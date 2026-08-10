# Aspid.Core.HSM

<p>
  <img src="https://img.shields.io/badge/Unity_2022.3%2B-000000?style=flat&logo=unity&logoColor=white&color=4fa35d" alt="Unity 2022.3+" />
  <a href="https://github.com/VPDPersonal/Aspid.Core.HSM/releases"><img src="https://img.shields.io/github/package-json/v/VPDPersonal/Aspid.Core.HSM/upm-preview?label=Preview&labelColor=4d4425&color=a3923d" alt="Preview" /></a>
  <img src="https://img.shields.io/badge/License-MIT-000000?style=flat&labelColor=254d2c&color=4fa35d" alt="MIT" />
</p>

> [!WARNING]
> **Work in progress.** Aspid.Core.HSM is under active development. The public API is not yet stable and may change without notice between releases. Use with care in production.

**Aspid.Core.HSM** is a Roslyn-powered Hierarchical State Machine for Unity, built from small composable abstractions: states with `Enter` / `Exit` hooks, a parent→child hierarchy expressed through the generic `IChildState<TParent>` interface, pluggable per-frame controllers (`IUpdateController`, `IFixedUpdateController`, `ILateUpdateController`, …) aggregated via `[ControllerGroup]`, declarative guarded transitions (`[Transition]`), extension states (`[ExtensionFor]`), state scopes and async enter/exit. `MonoStateMachine` wires it all to the Unity `MonoBehaviour` lifecycle.

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
https://github.com/VPDPersonal/Aspid.Core.HSM.git#upm-preview/0.0.1-alpha.2
```

> **Note.** There is no stable release yet, so the `upm` branch does not exist. The [Release workflow](.github/workflows/release.yml) creates it — along with `upm/<version>` tags and a `#upm` install URL — when the first non-prerelease version ships.

## License

[MIT](LICENSE).
