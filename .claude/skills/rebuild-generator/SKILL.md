---
name: rebuild-generator
description: Rebuild the Roslyn source generator. The compiled DLL is automatically delivered into the Unity package via Directory.Build.targets (CopyGeneratorToUnityPackage). Use when generator code under Aspid.Core.HSM.Generators/ has changed and Unity needs the updated DLL.
disable-model-invocation: true
---

# rebuild-generator

Rebuilds `Aspid.Core.HSM.Generators` and lets the build pipeline drop the DLL into the Unity package.

## What runs

```bash
cd Aspid.Core.HSM.Generators
dotnet build Aspid.Core.HSM.Generators.slnx -c Release
```

The copy into `Aspid.Core.HSM/Assets/Plugins/Aspid/Core/HSM/Aspid.Core.HSM.Generators.dll` is **not** performed by this skill — it happens inside MSBuild via the `CopyGeneratorToUnityPackage` target in `Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators/Directory.Build.targets`. Do not add manual copy steps here or in hooks; if the DLL is not appearing, fix the target.

## After running

1. Switch to the Unity Editor — the asset DB picks up the refreshed DLL on focus.
2. If Unity holds the DLL open and the build fails with a file-locked error, close the Editor or remove the asset import lock, then rebuild.
