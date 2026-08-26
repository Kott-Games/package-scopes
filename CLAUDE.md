# Packages/Scopes

Git submodule (`Kott-Games/package-scopes`). This is the project's core
architecture package — the game's scene/manager lifecycle runs through it. This is
our own code, freely editable, but changes need a commit in this submodule's own
repo, then a pointer bump in the main project (see root `CLAUDE.md` → Submodules).

## The idea

A **Scope** links a scene (loaded via Addressables) to a Prefab containing Manager
components. Loading a scope replaces manual `SceneManager.LoadScene` calls: it
handles all async loading (scene + managers), lets the *calling* scope act as a
loading screen and as the new scope's parent, and initializes all the new scope's
managers before handing control over. This gives hierarchical scopes (parent scope →
child scope) instead of one flat scene stack.

Managers are meant to be generalized/reusable via easy singleton setup:
`ISingletonContract<T>` + `Singleton<T>` struct gives a manager singleton-style
access without the usual boilerplate/footguns.

`SceneLink` lets a manager grab objects that are physically present in the target
scene — this works because manager prefabs are spawned *before* the scene finishes
initializing, so the manager can wire itself up as the scene comes online.

## Key files

- `Scope.cs` / `RuntimeScope.cs` / `RuntimeScope_Internal.cs` — the scope definition
  and its runtime instance/loading logic.
- `ScopeHandler.cs` — orchestrates loading/unloading a scope (scene + managers,
  async, progress reporting via `Packages/Utilities/Runtime/Progress`).
- `Manager.cs` — base class for scope-owned managers.
- `Singleton.cs` / `ISingleton.cs` / `ISingletonContract.cs` — singleton-access
  pattern for managers.
- `SceneLink.cs` / `SceneRoot.cs` — how a manager finds/binds to objects already
  placed in the scene.
- `Bootstrap.cs` — entry point tying into `Assets/Scripts/Runtime/Core/StartupManager.cs`
  to load the first scope on game start.
- `CoroutineRunner.cs` / `CoroutineHandler.cs` — coroutine execution not tied to a
  specific MonoBehaviour lifetime, used for scope-driven async work.
- `DebugDelayManager.cs` — editor/debug tool to simulate load delays.

## Working here

Anywhere a manager needs "the current scene's objects" or "run this at game/scope
start" or "load a new area/screen with a loading screen," it should go through this
package's scope/manager pattern rather than ad hoc `SceneManager` or `FindObjectOfType`
calls.
