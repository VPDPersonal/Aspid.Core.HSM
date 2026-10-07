---
name: create-extension
description: Scaffold an HSM extension state with [ExtensionFor], [ControllerGroup], and CanAttachTo constraints. Use when adding dynamic behavior overlays (mini-games, buffs, HUD).
---

# create-extension

Scaffold an extension state that attaches/detaches dynamically to compatible host states.

## Steps

1. Ask the user for:
   - **Extension name** (e.g., `MiniGameExtension`, `BuffOverlay`)
   - **Compatible states** (which host states this extension can attach to)
   - **Controllers** to include (existing or new classes)
   - **Namespace** (default: infer from surrounding files)
2. Create the extension `.cs` file with proper attributes.
3. The class **must** be `partial` (source generator emits `CanAttachTo()` from `[ExtensionFor]`).

## Template

```csharp
using Aspid.Core.HSM;

namespace YOUR_NAMESPACE
{
    [ExtensionFor(typeof(COMPATIBLE_STATE_1), typeof(COMPATIBLE_STATE_2))]
    [ControllerGroup]
    public partial class EXTENSION_NAME : IExtensionState
    {
        public EXTENSION_NAME()
        {
            AddControllers(/* controllers here */);
        }
    }
}
```

## Extension lifecycle

- `CanAttachTo(IState)` — generated from `[ExtensionFor]`, pattern-matches compatible states
- `OnAttached(IState)` — optional, called after attachment (override if needed)
- `OnDetached(IState)` — optional, called after detachment (override if needed)
- Auto-detached on transition to an incompatible state
- Dispatch order: host state controllers first, then extensions in attachment order

## Checklist

- [ ] Class is `partial`
- [ ] `[ExtensionFor(...)]` lists all compatible host states
- [ ] `[ControllerGroup]` attribute present
- [ ] Class implements `IExtensionState`
- [ ] `AddControllers(...)` called in constructor
- [ ] File placed in the correct project directory
- [ ] Remind user: attach via `stateMachine.AttachExtension<EXTENSION_NAME>()`
- [ ] Remind user: detach via `stateMachine.DetachExtension<EXTENSION_NAME>()`
