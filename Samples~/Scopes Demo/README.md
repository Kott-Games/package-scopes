# Scopes Demo

A small board game that walks through everything Scopes does: a root scope without a scene, scopes swapped
in and out under a parent that keeps the state, scene-less overlay scopes, dependency injection across
levels, both scene loaders, and a mini game that fails on purpose.

Visuals are primitives and uGUI built in code, so the scenes stay tiny and the code stays the point.

## Requirements

- Addressables (`com.unity.addressables`): Board and the mini games are Addressables scenes.
- uGUI (`com.unity.ugui`).
- Active Input Handling set to *Input Manager (Old)* or *Both* (Project Settings > Player).
- Built-in render pipeline or URP.

## Setup after importing

1. **Tools > Scopes > Validate All.** Press the *Register with …* fix button on every issue: it adds
   `Menu.unity` to the build profile's scene list and marks `Board`, `Reaction` and `Mash` addressable
   (samples can't bring their scene registration along). Validate again: no issues left.
2. Open `Scenes/Boot.unity` and press Play.

For a player build, also put `Boot.unity` first in the build profile's scene list.

## Controls

| Key | Where | |
|---|---|---|
| Space | Board | roll the dice |
| Space | mini games | the action (or click *Go*) |
| Esc | Board, mini games | pause |
| F1 | everywhere | toggle the scope tree / lifecycle log |

## The scope tree

```
Boot.unity (build list; Bootstrap → Core)
Core              root, scene-less   ISaveManager      profile (PlayerPrefs): games, coins, best scores
│                                    ILoadingScreen    progress overlay for scope operations
│                                    IScopeDebugOverlay live scope tree + lifecycle log (F1)
│                                    IAppFlow          swaps Menu ↔ Game
├─ Menu           build-list scene   Start Game, Stats, Quit
│  └─ Stats       scene-less         panel spawned under its scope root
└─ Game           scene-less         IGameSession      players, coins, positions, turn, round
   │                                 IDice
   │                                 IGameFlow         swaps Board ↔ mini game, opens Pause (Esc)
   ├─ Pause       scene-less         overlay, time scale 0 while loaded
   ├─ Board       addressable scene  tiles, tokens, camera, HUD; rebuilt from IGameSession on every load
   └─ Reaction | Mash | Broken       one mini game at a time, each provides IMiniGame
```

Only one heavy world is loaded at a time: playing a mini game unloads Board, finishing it unloads the mini
game and loads Board again. The game state lives one level up, in Game, so nothing is lost.

## What to look at

| Feature | Where |
|---|---|
| Root scope without a scene, session-long managers | `Scopes/Core.asset`, `Scripts/Core` |
| Queueing a load from `Initialize` (runs once the current load is done) | `AppFlow.Initialize`, `GameFlow.Initialize` |
| Swapping sibling scopes; unloading a scope unloads its children first | `AppFlow.Swap`, `GameFlow.Switch` |
| Loading screen driven by `ScopeOperation` progress; `DebugDelayManager` makes loads visible | `LoadingScreen` |
| `ScopeLoaded`/`ScopeUnloaded` events, live tree with state, scene and provided contracts | `ScopeDebugOverlay` |
| Scene-less child scopes with UI under `Scope.Root` | `StatsPanel`, `PauseMenu` |
| State in a parent surviving reloads of heavy children | `GameSession`, `BoardManager` |
| Injection across levels: own scene, parent, root | `MiniGameBase` |
| Dependency-ordered initialization (`GameSession` listed before `Dice`, initialized after it) | `Scopes/Game.asset`, `GameSession.Initialize` |
| `[InjectScene]` on managers | `BoardManager` (camera, tiles, tokens, HUD), `MenuManager`, `MiniGameBase` |
| `[Inject]` on scene components | `BoardTile` |
| `Scope.Instantiate` injecting a prefab before its `Awake` | `BoardManager.OnSceneAwake`, `PlayerToken` |
| Sibling definitions providing the same contract; resolving through a child scope | `IMiniGame`, `GameFlow.PlayMiniGame` |
| A failed load: already initialized managers are uninitialized, the scope is removed, the operation fails | `BrokenGame`, `GameFlow.PlayMiniGame` |
| Build-profile scene vs. Addressables scene | `Scopes/Menu.asset` vs. `Scopes/Board.asset` |
| Declared parents: every definition passes Validate All | all `Scopes/*.asset` |

Picking *Broken* logs an error to the console (`Load of scope 'Broken' failed`); that is the point.

## Tests

- `Tests/Editor/GameSessionTests`: unit tests of a manager with a fake `IDice`, injected through
  `TestInjector` (Kott.Scopes.Testing). No scope or scene involved.
- `Tests/Runtime/DemoSmokeTest`: plays Core → Menu → Game/Board → Reaction → Board → Menu with the real
  scenes and checks the scope tree at every step. Needs the setup above.
