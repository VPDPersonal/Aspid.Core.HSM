# Consumer Project Guidelines

Guidelines for projects that use `Aspid.Core.HSM` as a dependency (e.g., CarX Street).

## 1. Project Structure

Organize by feature, not by type. Each feature owns its states, controllers, and transitions.

```
Assets/
├── Features/
│   ├── Freeride/
│   │   ├── CLAUDE.md              # describes this feature's HSM structure
│   │   ├── States/
│   │   │   ├── FreerideState.cs
│   │   │   └── FreerideInventoryState.cs
│   │   ├── Controllers/
│   │   │   ├── FreerideMovementController.cs
│   │   │   └── FreerideHudController.cs
│   │   └── Transitions/
│   │       └── FreerideToRaceTransition.cs
│   ├── Race/
│   │   ├── CLAUDE.md
│   │   ├── States/
│   │   ├── Controllers/
│   │   └── Transitions/
│   └── Shared/
│       ├── Controllers/           # reusable controllers
│       │   └── InputControllerGroup.cs
│       └── Extensions/            # extension states
│           └── MiniGameExtension.cs
├── Core/
│   ├── StateMachines/
│   │   └── GameStateMachine.cs    # main state machine
│   └── StateFactory/
│       └── GameStateFactory.cs    # DI-aware factory
```

Why: Claude Code navigates by reading `CLAUDE.md` files. Feature-based layout means each `CLAUDE.md` describes a self-contained slice of your state machine, and Claude can find the right code without scanning the whole project.

## 2. Feature CLAUDE.md Template

Each feature folder should have a `CLAUDE.md` describing its HSM structure. This is the primary way Claude Code understands your state machine topology.

```markdown
# Feature: Freeride

## States
- `FreerideState` — main freeride gameplay, parent: `GameplayState`
- `FreerideInventoryState` — inventory overlay, parent: `FreerideState`

## Controllers
- `FreerideMovementController` — handles player movement in open world
- `FreerideHudController` — manages HUD elements specific to freeride

## Transitions
- `FreerideToRaceTransition` — triggered when player enters race zone, guard: checks race availability

## Extension States
- Compatible with: `MiniGameExtension`, `ChatExtension`
```

Keep it factual: state name, parent, what each controller does, what guards check. Claude Code uses this to decide which files to read when working on a task.

## 3. XML Documentation Patterns

Controllers should have concise XML docs explaining purpose and active state:

```csharp
/// <summary>Handles player car physics in freeride mode. Active during FreerideState.</summary>
public class FreerideMovementController : IUpdateController, IEnterController
{
    void IEnterController.OnEnter() { /* init */ }
    void IUpdateController.Update(float dt) { /* move */ }
}
```

States need less documentation — their name + `[ParentState]` attribute tell the story. But complex guard logic in transitions should be documented:

```csharp
[Transition(typeof(FreerideState), typeof(RaceState))]
public partial class FreerideToRaceTransition : ITransition
{
    /// <summary>
    /// Guards: player must be in a race zone AND the race must not be full.
    /// </summary>
    public bool CanTransition() => _raceZone.IsPlayerInside && !_raceSession.IsFull;
}
```

## 4. Naming Conventions

| Type | Pattern | Example |
|------|---------|---------|
| State | `{Feature}State` | `FreerideState` |
| Child state | `{Feature}{SubFeature}State` | `FreerideInventoryState` |
| Controller | `{Feature}{Responsibility}Controller` | `FreerideMovementController` |
| ControllerGroup | `{Feature}ControllerGroup` or `{Responsibility}ControllerGroup` | `InputControllerGroup` |
| Transition | `{Source}To{Target}Transition` | `FreerideToRaceTransition` |
| Extension | `{Feature}Extension` or `{Feature}ExtensionState` | `MiniGameExtension` |

## 5. State Machine Setup

Create a `MonoStateMachine` subclass as the entry point. Register transitions in `OnInitialized()` and kick off the initial state:

```csharp
public class GameStateMachine : MonoStateMachine
{
    [SerializeField] private GameStateFactory _factory;

    private void Start()
    {
        Initialize(_factory);
    }

    protected override void OnInitialized()
    {
        RegisterTransition(new FreerideToRaceTransition());
        RegisterTransition(new RaceToFreerideTransition());
        // ...
        ChangeState<MainMenuState>();
    }
}
```

Create a `StateFactory` subclass to wire up DI or manual instantiation:

```csharp
public class GameStateFactory : StateFactory
{
    protected override IState CreateStateInternal(Type type)
    {
        // Manual, or resolve from DI container
        return (IState)Activator.CreateInstance(type);
    }
}
```

For DI integration, override `CreateScopeForState` to create container child scopes:

```csharp
public class VContainerStateFactory : StateFactory
{
    private readonly IObjectResolver _rootResolver;

    protected override IState CreateStateInternal(Type type)
        => (IState)_rootResolver.Resolve(type);

    protected override IStateScope? CreateScopeForState(Type stateType, IStateScope? parentScope)
        => new VContainerStateScope(_rootResolver.CreateScope(/* ... */));
}
```

## 6. Testing Patterns

### Controllers — plain unit tests

Controllers are plain classes with no framework dependency. Test them directly:

```csharp
[Test]
public void MovementController_UpdatesPosition()
{
    var controller = new FreerideMovementController(mockInput, mockPhysics);
    controller.OnEnter();
    controller.Update(0.016f);
    Assert.AreEqual(expectedPosition, mockPhysics.Position);
}
```

### Transitions — test guard logic

```csharp
[Test]
public void FreerideToRace_BlocksWhenRaceIsFull()
{
    var transition = new FreerideToRaceTransition(mockRaceZone, mockSession);
    mockSession.IsFull = true;
    Assert.IsFalse(transition.CanTransition());
}
```

### States — integration tests via StateMachineBase

For state lifecycle and hierarchy tests, use `StateMachineBase` directly with a test factory:

```csharp
public class TestFactory : StateFactory
{
    protected override IState CreateStateInternal(Type type)
        => (IState)Activator.CreateInstance(type);
}

[Test]
public void TransitionTo_ExitsOldState_EntersNewState()
{
    var sm = new StateMachineBase(new TestFactory());
    sm.ChangeState<FreerideState>();
    sm.RegisterTransition(new FreerideToRaceTransition());

    sm.TransitionTo<RaceState>();

    Assert.IsInstanceOf<RaceState>(sm.CurrentStates[^1]);
}
```

## 7. Common Pitfalls

**Don't put logic in State classes — use Controllers.**
States are structural (hierarchy + controller composition). All behavioral logic belongs in controllers. This keeps controllers testable in isolation and reusable across states via ControllerGroups.

**Don't forget to register transitions.**
`TransitionTo<T>()` works without a registered transition, but it falls back to `ChangeState<T>()` — skipping all guards, `OnBeforeTransition`, and `OnAfterTransition`. If you need guards, register the transition explicitly.

**Don't make controllers depend on each other's initialization order.**
When using `[AsyncMode(typeof(IAsyncEnterController), AsyncExecutionMode.Parallel)]`, controllers enter concurrently. Design each controller to be self-contained during `OnEnter` / `OnEnterAsync`. If ordering matters, use `Sequential` mode.

**Extension States auto-detach on incompatible transitions.**
When the state machine transitions to a state not listed in `[ExtensionFor(...)]`, the extension is automatically detached. Don't assume an extension persists across all transitions — check `ActiveExtensions` or re-attach when needed.

**Don't call `ChangeState` during an async transition.**
If an async `ChangeStateAsync` or `TransitionToAsync` is in progress, calling `ChangeState` synchronously throws `InvalidOperationException`. Either await the async transition or cancel it first.
