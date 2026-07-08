# HSM Phase 5: Scope Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add DI-agnostic scope abstraction to the HSM. Each state gets its own `IStateScope`, child states inherit parent scopes. Scopes can be Transient (create/dispose each transition) or Cached (reused). The actual DI integration (VContainer) is a separate package — this phase builds the core abstraction and tests.

**Architecture:** `IStateScope` is a simple interface with `Parent`, `CreateChildScope()`, `IDisposable`. `StateFactory` is updated to create and manage scopes alongside states. `ScopeLifetimeAttribute` lets states declare Cached lifetime. The scope hierarchy mirrors the state hierarchy.

**Tech Stack:** C# (.NET 10, netstandard2.0 for generator), xUnit, UniTask

---

**Path aliases:**

| Alias | Full Path |
|---|---|
| `$RT` | `Aspid.Core.HSM/Assets/Plugins/Aspid/Core/HSM` |
| `$TESTS` | `Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests` |

Root: `/Users/vladislavpanin/Documents/Docs/Aspid/Packages/Aspid.Core.HSM/Projects/Aspid.Core.HSM/`

---

## File Structure

### Files to create

| File | Responsibility |
|---|---|
| `$RT/Source/IStateScope.cs` | Scope interface |
| `$RT/Source/ScopeLifetime.cs` | Enum: Transient, Cached |
| `$RT/Source/Generation/ScopeLifetimeAttribute.cs` | `[ScopeLifetime(ScopeLifetime.Cached)]` |
| `$TESTS/StateMachineTests/ScopeLifecycleTests.cs` | Tests for scope create/dispose/cache |

### Files to modify

| File | Change |
|---|---|
| `$RT/Source/StateFactory.cs` | Add scope management (create, cache, dispose) |
| `$TESTS/Aspid.Core.HSM.Generators.Tests.csproj` | Link new files |
| `$TESTS/StateMachineTests/TestStateFactory.cs` | Add scope support for testing |

---

### Task 1: Core types — IStateScope, ScopeLifetime, ScopeLifetimeAttribute

**Files to create:**

`$RT/Source/IStateScope.cs`:
```csharp
using System;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public interface IStateScope : IDisposable
    {
        IStateScope? Parent { get; }
        IStateScope CreateChildScope();
    }
}
```

`$RT/Source/ScopeLifetime.cs`:
```csharp
// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public enum ScopeLifetime
    {
        Transient,
        Cached
    }
}
```

`$RT/Source/Generation/ScopeLifetimeAttribute.cs`:
```csharp
using System;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ScopeLifetimeAttribute : Attribute
    {
        public ScopeLifetime Lifetime { get; }

        public ScopeLifetimeAttribute(ScopeLifetime lifetime)
        {
            Lifetime = lifetime;
        }
    }
}
```

Build, commit: `feat: add IStateScope, ScopeLifetime enum, and ScopeLifetimeAttribute`

---

### Task 2: Update StateFactory with scope management

**Files to modify:**
- `$RT/Source/StateFactory.cs`

Add scope lifecycle to StateFactory:

1. **Root scope** — set via constructor or `SetRootScope(IStateScope scope)`
2. **Scope creation** — when creating a state, create a child scope from parent state's scope
3. **Scope tracking** — `Dictionary<Type, IStateScope>` maps state types to their scopes
4. **Cached scopes** — `Dictionary<Type, IStateScope>` for cached scopes that survive Exit
5. **Release** — dispose transient scopes on state exit, keep cached ones
6. **DisposeAllScopes** — dispose everything including cached scopes

Key additions to `StateFactory`:
```csharp
private IStateScope? _rootScope;
private readonly Dictionary<Type, IStateScope> _activeScopes = new();
private readonly Dictionary<Type, IStateScope> _cachedScopes = new();

public void SetRootScope(IStateScope rootScope) => _rootScope = rootScope;

public IStateScope? GetScope(Type stateType) =>
    _activeScopes.GetValueOrDefault(stateType);

// Called during state creation — creates child scope
protected virtual IStateScope? CreateScopeForState(Type stateType, IStateScope? parentScope)
{
    return parentScope?.CreateChildScope();
}

// Called during state release — disposes transient scopes
// Override point for cached behavior
```

The scope creation integrates into `BuildChain` or is called by `StateMachineBase` during Enter/Exit.

**Design choice:** Scope management lives in `StateFactory` (not StateMachineBase) because the factory owns state lifecycle. StateMachineBase calls `MarkInitialized` and `Release` — these are the natural points to create/dispose scopes.

Modify `MarkInitialized(IState state)`:
- If `_rootScope` is set, create a child scope for this state
- Check `[ScopeLifetime]` attribute on the state type for Cached behavior
- Store in `_activeScopes`

Modify `Release(IState state)`:
- If scope is Transient → dispose and remove
- If scope is Cached → keep in `_cachedScopes`, remove from `_activeScopes`

Add `DisposeAllScopes()`:
- Dispose all active and cached scopes

Build, run existing tests (must all pass), commit: `feat: add scope management to StateFactory`

---

### Task 3: Link files + runtime tests

**Files to modify:**
- `$TESTS/Aspid.Core.HSM.Generators.Tests.csproj` — link new files
- `$TESTS/StateMachineTests/TestStateFactory.cs` — add scope support

**Files to create:**
- `$TESTS/StateMachineTests/ScopeLifecycleTests.cs`

**Test helpers:**
```csharp
public class TestScope : IStateScope
{
    public IStateScope? Parent { get; }
    public bool IsDisposed { get; private set; }
    public int ChildScopeCount { get; private set; }

    public TestScope(IStateScope? parent = null) => Parent = parent;

    public IStateScope CreateChildScope()
    {
        ChildScopeCount++;
        return new TestScope(this);
    }

    public void Dispose() => IsDisposed = true;
}
```

**Tests:**

1. **`Scope_created_for_state_on_enter`** — state gets a scope when entering
2. **`Scope_disposed_on_state_exit`** — transient scope disposed on exit
3. **`Child_state_scope_has_parent_scope`** — scope.Parent points to parent state's scope
4. **`Cached_scope_survives_exit`** — scope NOT disposed when state exits
5. **`Cached_scope_reused_on_reenter`** — same scope instance returned on second enter
6. **`Cached_scope_disposed_on_factory_dispose`** — DisposeAllScopes cleans cached scopes
7. **`No_scope_created_when_no_root_scope_set`** — without SetRootScope, no scopes created

Build, run all tests, commit: `test: add scope lifecycle tests`

---

### Task 4: Final verification

Run all tests + build full solution.
