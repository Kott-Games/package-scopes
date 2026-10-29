# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed
- `Kott.Scopes.Testing` now compiles in the editor when the package is installed from a registry or git URL
  without being listed in `testables`. The imported sample tests failed with a missing `Kott.Scopes.Testing`
  namespace.

## [1.0.0] - 2026-10-19

### Added
- Scoped dependency injection. Managers provide **manager contracts** (interfaces extending
  `IManagerContract`) and receive them through `[Inject]` fields (`Optional = true` allows a missing one).
  Contracts resolve along the scope's parent chain only (own scope → parent → … → root). A contract can have
  one provider per chain; siblings may provide the same one. `[Inject]` fields are filled before any manager
  of the scope initializes and cleared after the scope unloads.
- Managers initialize in dependency order: after the same-scope managers they inject, otherwise in list
  order. Dependency cycles fail the load. They uninitialize in reverse initialization order.
- `[InjectScene("key")]` manager fields receive `SceneLink` objects (GameObject, component or interface)
  when the scene activates, before `OnSceneAwake`, and are cleared after `OnSceneExit`.
- Scene components get their `[Inject]` fields filled before their `Awake`. `Scope.Inject(object)` and
  `Scope.Instantiate(prefab, parent)` inject objects created at runtime (instantiated copies are
  injected before `Awake`). `Scope.Resolve<T>()`/`TryResolve<T>()` for code outside the scope tree.
- Logging modeled on .NET logging, with `UnityEngine.ILogger` as logger type: an `ILoggerFactory`
  (`ScopeHandler.LoggerFactory`) creates one logger per category. Each manager gets `Manager.Logger` (category =
  its full type name, set before `Initialize`); the scope system and `CoroutineHandler` log as `Kott.Scopes`.
  Default `UnityLoggerFactory`: writes `[category] message` to `Debug.unityLogger`, filters per category prefix
  (`SetFilter`, longest prefix wins, `DefaultFilter`). `CreateLogger<T>()` / `CreateLogger(Type)` extensions.
  No package code logs through `Debug` directly. `TestInjector.SetLogger` for tests.
- `Kott.Scopes.Testing` assembly with `TestInjector` to fill `[Inject]`/`[InjectScene]` fields with fakes.
- Scene loading abstraction: `SceneReference` / `ISceneLoader` with `BuildSceneReference` (build profile
  scene list) and `AddressableSceneReference` (separate assembly, enabled when Addressables is installed).
- `ScopeOperation` result for loads and unloads (`Succeeded`, `Exception`, `Completed`, yieldable).
- `ScopeHandler.IsLoaded`, `TryGetScope`, `Scopes`, `ScopeLoaded` / `ScopeUnloaded` events.
- `Scope.Root` (scope-lifetime GameObject), `State`, `Managers`, `Scene`, `StartCoroutine`,
  `TryGetSceneGameObject` / `TryGetSceneComponent`.
- Scope definition inspector: scene with loader picker (None / Build Profile / Addressables) and Open
  button, manager list with a searchable type picker, per manager the provided contracts, the injected ones
  and where they come from, and the scene keys; the effective initialization order; unresolved (renamed)
  manager types shown with their stored type name; and a Health section.
- Optional declared parent (`ScopeDefinition.DeclaredParent`); loading under a different parent logs a warning.
- `ScopeValidator` (editor, callable from tests/CI): unresolved/empty managers, injected contracts not
  provided along the declared parents (warning without a declared parent, error with one), contracts
  provided twice, invalid `[Inject]`/`[InjectScene]` fields, dependency cycles, declared-parent cycles,
  missing or unregistered scenes (with a fix that registers the scene), scenes without exactly one
  `SceneRoot`, duplicate/empty `SceneLink` keys, `[InjectScene]` keys missing in the scene or lacking the
  component. `Tools > Scopes > Validate All` lists every issue with click-to-select and fix buttons.
- `Assets > Create > Scopes > Scope with Scene (Build Profile / Addressables)`: creates the definition and
  a scene from the Scope Scene template and registers the scene with the loader.
- In the editor, build-profile scenes that are missing from the scene list still load in play mode, with a
  warning that a build will fail to load them.
- "Scopes Demo" sample: a small board game with a root scope, swapped Menu/Game and Board/mini game
  scopes, scene-less overlay scopes, a loading screen, a live scope tree overlay, injection across levels,
  both scene loaders and a failing mini game; with EditMode tests of a manager and a PlayMode smoke test.

### Changed
- **Breaking:** renamed the `Scope` asset to `ScopeDefinition` and `RuntimeScope` (the loaded instance) to
  `Scope`. Existing assets keep working (same script GUID). `ScopeOperation.Definition`/`Scope`,
  `Manager.Scope`.
- **Breaking:** manager callbacks renamed: `SceneAwake`/`SceneStart`/`SceneExit` →
  `OnSceneAwake`/`OnSceneStart`/`OnSceneExit`, `SceneUpdate`/`SceneFixedUpdate` → `Update`/`FixedUpdate`.
- **Breaking:** managers are plain `[Serializable]` C# classes stored in the definition asset
  (`[SerializeReference]`) instead of MonoBehaviours on a prefab. Each load works on a copy.
- **Breaking:** the definition holds a `SceneReference` instead of an Addressables `AssetReference`;
  Addressables is no longer a dependency.
- **Breaking:** `LoadScope`/`UnloadScope` return a `ScopeOperation` and run immediately (queued, one
  at a time) instead of returning an `IEnumerator` with a done callback.
- **Breaking:** `Bootstrap` references the root `ScopeDefinition` directly instead of an Addressables reference.
- `ScopeHandler` and `CoroutineHandler` create a persistent host on first use; hand-placed instances are
  reused and never duplicated.
- `Update`/`FixedUpdate` are driven by the scope root, so scene-less scopes receive them too; they start
  once the scope is fully loaded.
- `CoroutineHandler.Run` returns the `Coroutine`; routines run on the handler itself (no runner pool).

### Removed
- **Breaking:** the global singleton lookup: `ISingleton`, `ISingletonContract<T>`, `Singleton<T>`,
  `SingletonRegistry`, `ScopeHandler.GetSingleton` and the obsolete `GetManager`. Use contracts with
  `[Inject]`, or `Scope.Resolve<T>()` on a scope you hold.

### Fixed
- An exception in `Initialize`/`Uninitialize` no longer deadlocks all later scope operations; a failed
  load cleans up the partially loaded scope.
- `RootScope` is cleared when the root scope unloads.
- `OnApplicationQuit` no longer throws when nothing is loaded.
- A throwing `CoroutineHandler` routine no longer affects later ones.
- A scope scene without a `SceneRoot` fails the load instead of loading without scene injection and
  callbacks. `OnSceneExit` is only called after `OnSceneAwake`.
- Contracts provided twice, missing contracts and missing/duplicate `SceneLink` keys report descriptive
  errors.
