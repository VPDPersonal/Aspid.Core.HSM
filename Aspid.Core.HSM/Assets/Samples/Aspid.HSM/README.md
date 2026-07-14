# Aspid Quest — Aspid.Core.HSM sample

A tiny IMGUI game driving a deep hierarchical state machine. Open `Scenes/HSM_GameLoop.unity`,
press Play, and open **Window > Aspid > State Tree** to watch the HSM live.

## Core idea: states compose, controllers behave

States are thin composition points: they declare their place in the tree (`IChildState<TParent>`)
and assemble a `[ControllerGroup]` of controllers in the constructor. All behavior — input,
simulation, timers, screens — lives in controllers dispatched by the machine. The two exceptions
are deliberate: `SinglePlayerState`/`MultiplayerState` show that a *trivial* state may implement
a controller interface itself, and `RootState` carries the teardown `IDisposableController`.

Screens are controllers too (`IGuiController`), registered in the group like everything else and
drawn by `SampleGameManager.OnGUI` dispatching through the active chain.

## Layout mirrors the state tree

Every feature folder holds everything about that branch: the state, its controllers, its screen,
its data slice, and the transitions that originate from it. All files share the single
`Aspid.Core.HSM.Sample` namespace — folders carry the structure.

```
Scripts/
├── SampleGameManager.cs         MonoStateMachine host: navigation, hotkeys, result polling, OnGUI dispatch
├── SampleStateFactory.cs        type → constructor map, injects GameSession / IGameActions
├── IGameActions.cs              navigation contract screens call into
├── RootState.cs                 always active; demos IDisposableController on machine teardown
├── Common/                      GameSession (slice composition), PlayerData, styles, GameHUD, IGuiController
├── Loading/                     AssetLoadingController (async enter) + screen
├── Menu/
│   ├── Settings/                SettingsPersistenceController + screen; SettingsData persists across runs
│   └── Credits/                 CreditsRollController owns the roll; state forwards IsFinished
├── Gameplay/
│   ├── Pause/                   TimeScaleController + screen
│   ├── SinglePlayer/
│   │   ├── Exploration/         movement, wild-encounter, NPC controllers + screen + HUD
│   │   ├── Combat/              setup, input, AI, hit-flash, log controllers + screen + HUD
│   │   └── Dialogue/            ConversationController owns the dialogue; screen talks to it
│   └── Multiplayer/
│       ├── Lobby/               MatchmakingController + screen; LobbyData guards the transition
│       └── Match/               MatchTimerController + screen + HUD
├── GameOver/
└── Extensions/                  DebugOverlay (manual), FpsCounter ([ExtensionFor]), SlowMotion (manual)
```

## State tree

```
RootState
├── LoadingState                 (Transient, async enter)
├── MainMenuState
│   ├── SettingsState
│   └── CreditsState             (Transient)
├── GameplayState                (Cached)
│   ├── SinglePlayerState
│   │   ├── ExplorationState
│   │   ├── CombatState          (Transient)
│   │   └── DialogueState        (Transient)
│   ├── MultiplayerState         (Cached)
│   │   ├── LobbyState           (Transient)
│   │   └── MatchState           (Transient)
│   └── PauseState
└── GameOverState                (Transient)
```

## Controls

| Input | Action |
| --- | --- |
| WASD / arrows | Move in exploration |
| E | Talk to the NPC (when near) |
| Space | Attack / advance dialogue / score a goal |
| Enter | Join match (in lobby) |
| Esc | Pause / back / resume |
| F1 | Toggle debug overlay extension |
| F2 | Toggle FPS counter extension |

## What demonstrates what

| Feature | Where |
| --- | --- |
| Parent chain via `IChildState<TParent>` | every state |
| `[ControllerGroup]` + `AddControllers` | almost every state — `Combat/CombatState.cs` is the richest |
| Registration order inside a group | Exploration: movement writes `IsMoving`, encounter reads it; Combat: setup before AI, screen before HUD |
| Controllers sharing an instance | `Dialogue/DialogueState.cs`, `Menu/Credits/CreditsState.cs` (controller passed to its screen) |
| State as its own controller (trivial only) | `SinglePlayer/SinglePlayerState.cs`, `Multiplayer/MultiplayerState.cs`, `RootState.cs` |
| `IUpdateController` / `ILateUpdateController` / `IFixedUpdateController` | `Combat/PlayerCombatController.cs` / `Combat/HitFlashController.cs` / `Exploration/NpcController.cs` |
| `IDisposableController` (machine teardown) | `RootState.cs` |
| Async enter in a group (`IAsyncEnterController` + `ChangeStateAsync`) | `Loading/AssetLoadingController.cs`, `SampleGameManager.BootAsync` |
| `[Transition]` + generator (attribute style) | `Exploration/ExplorationToCombatTransition.cs` and other hook-only transitions |
| `ITransition<TSource, TTarget>` (interface style) | `Menu/MenuToGameplayTransition.cs` and other guarded transitions |
| Transition guards (`CanTransition`) | `Combat/CombatToExplorationTransition.cs`, `Lobby/LobbyToMatchTransition.cs` |
| `TransitionTo` vs `TransitionVia` | `SampleGameManager` (`JoinMatch` uses `TransitionVia`) |
| `[ExtensionFor]` + generator / manual `CanAttachTo` | `Extensions/FpsCounterExtension.cs` / `Extensions/SlowMotionExtension.cs` |
| `[ScopeLifetime]` (Cached / Transient) | `Gameplay/GameplayState.cs`, `Combat/CombatState.cs`, ... |
| Custom controller interface dispatched by the host | `Common/IGuiController.cs` + `SampleGameManager.OnGUI` |

## Data ownership rules

- **Controller-owned** — read only by the owning group (its screen) or polled off the typed leaf
  by the manager: loading progress, the credits roll, the whole conversation. States forward
  what the manager needs (`DialogueState.IsFinished`), nothing touches `GameSession`.
- **Session slices** (`Common/GameSession.cs`) — data that must outlive Transient states or is
  read across features/transitions: `CombatData` (a fight survives pause), `ExplorationData`
  (position survives combat detours), `LobbyData` (transition guard), `MatchData`, `PlayerData`.
  `ResetRun()` recreates the run-scoped slices, so defaults live in field initializers only.
- **Persistent** — `SettingsData` survives `ResetRun()`.

Screens are plain IMGUI renderers with no state-machine knowledge: they read their slice or
controller and raise intent flags / call `IGameActions`.
