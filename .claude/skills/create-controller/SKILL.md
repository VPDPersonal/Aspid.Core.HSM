---
name: create-controller
description: Scaffold a new HSM controller implementing lifecycle interfaces (IUpdateController, IEnterController, IExitController, etc.). Use when adding game logic units.
---

# create-controller

Scaffold a new controller class implementing selected lifecycle interfaces.

## Steps

1. Ask the user for:
   - **Controller name** (e.g., `PlayerMovementController`, `TimerController`)
   - **Interfaces** to implement (pick from the list below)
   - **Constructor parameters** (dependencies the controller needs)
   - **Namespace** (default: infer from surrounding files)
2. Create the controller `.cs` file with explicit interface implementations.

## Available interfaces

| Interface | Method | Notes |
|---|---|---|
| `IEnterController` | `void OnEnter()` | Called when state enters |
| `IExitController` | `void OnExit()` | Called in reverse order on exit |
| `IUpdateController` | `void Update(float deltaTime)` | Per-frame update |
| `ILateUpdateController` | `void LateUpdate(float deltaTime)` | Late update pass |
| `IFixedUpdateController` | `void FixedUpdate(float deltaTime)` | Physics update |
| `IDisposableController` | `void Dispose()` | Cleanup, reverse order |
| `IAsyncEnterController` | `UniTask OnEnterAsync(CancellationToken)` | Async enter (needs `Cysharp.Threading.Tasks`) |
| `IAsyncExitController` | `UniTask OnExitAsync(CancellationToken)` | Async exit, reverse order |

## Template

```csharp
using Aspid.Core.HSM;

namespace YOUR_NAMESPACE
{
    public class CONTROLLER_NAME : IUpdateController, IEnterController
    {
        void IEnterController.OnEnter() { }

        void IUpdateController.Update(float deltaTime) { }
    }
}
```

### Async variant

```csharp
using System.Threading;
using Aspid.Core.HSM;
using Cysharp.Threading.Tasks;

namespace YOUR_NAMESPACE
{
    public class CONTROLLER_NAME : IAsyncEnterController
    {
        public UniTask OnEnterAsync(CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }
    }
}
```

## Checklist

- [ ] Class implements the requested `IController` sub-interfaces
- [ ] Methods use explicit interface implementation (e.g., `void IEnterController.OnEnter()`)
- [ ] Async controllers include `using System.Threading;` and `using Cysharp.Threading.Tasks;`
- [ ] Constructor accepts required dependencies
- [ ] File placed in the correct project directory
