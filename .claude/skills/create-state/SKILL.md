---
name: create-state
description: Scaffold a new HSM state with [ControllerGroup], optional [ParentState], and AddControllers. Use when adding a game state (e.g., a new gameplay mode, menu screen).
---

# create-state

Scaffold a new HSM state class with the correct attributes and controller registration.

## Steps

1. Ask the user for:
   - **State name** (e.g., `GameplayState`, `MenuState`)
   - **Parent state** (optional; omit `[ParentState]` for a root state, use `[ParentState(null)]` only for the single root)
   - **Controllers** to include (existing or new classes)
   - **Namespace** (default: infer from surrounding files)
2. Create the state `.cs` file in the appropriate directory.
3. The class **must** be `partial` (source generators require it).
4. Remind the user to register the state in their `StateFactory` subclass.

## Template

```csharp
using Aspid.Core.HSM;

namespace YOUR_NAMESPACE
{
    [ControllerGroup]
    [ParentState(typeof(PARENT_STATE))]  // remove if root state
    public partial class STATE_NAME : IState
    {
        public STATE_NAME()
        {
            AddControllers(/* controllers here */);
        }
    }
}
```

## Checklist

- [ ] Class is `partial`
- [ ] `[ControllerGroup]` attribute present
- [ ] `[ParentState(typeof(...))]` set correctly (or omitted for root)
- [ ] `AddControllers(...)` called in constructor with required controllers
- [ ] File placed in the correct project directory
- [ ] Remind user: register in `StateFactory` — `factory.RegisterState<STATE_NAME>();`
