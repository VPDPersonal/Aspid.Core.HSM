---
name: create-transition
description: Scaffold an HSM transition with [Transition], guard logic, and Before/After hooks. Use when defining how the game moves between states.
---

# create-transition

Scaffold a transition class between two HSM states with guard and lifecycle hooks.

## Steps

1. Ask the user for:
   - **Source state** (the state being exited)
   - **Target state** (the state being entered)
   - **Guard condition** (what must be true to allow the transition, or always-true)
   - **Namespace** (default: infer from surrounding files)
2. Create the transition `.cs` file. Name convention: `SourceToTarget` (e.g., `FreerideToRace`).
3. The class **must** be `partial` (source generator emits `SourceState`/`TargetState` properties).
4. Remind the user to register the transition.

## Template

```csharp
using Aspid.Core.HSM;

namespace YOUR_NAMESPACE
{
    [Transition(typeof(SOURCE_STATE), typeof(TARGET_STATE))]
    public partial class SOURCE_TO_TARGET : ITransition
    {
        public bool CanTransition() => true;

        public void OnBeforeTransition() { }

        public void OnAfterTransition() { }
    }
}
```

## Pipeline

The transition pipeline runs in this order: `CanTransition()` (guard) -> `OnBeforeTransition()` -> Exit source -> Enter target -> `OnAfterTransition()`.

## Checklist

- [ ] Class is `partial`
- [ ] `[Transition(typeof(Source), typeof(Target))]` attribute set correctly
- [ ] Class implements `ITransition`
- [ ] `CanTransition()` returns proper guard logic
- [ ] File placed in the correct project directory
- [ ] Remind user: register with `stateMachine.RegisterTransition(new SourceToTarget());`
- [ ] Remind user: trigger via `stateMachine.TransitionTo<TargetState>()` or `stateMachine.TransitionVia<SourceToTarget>()`
