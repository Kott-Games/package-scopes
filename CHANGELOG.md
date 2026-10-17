# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- `SingletonRegistry`: register/unregister by owner, `Get`/`TryGet`/`IsRegistered`, descriptive errors.
  `Singleton<T>` resolves through `SingletonRegistry.Current`.
- `Kott.Scopes.Testing` assembly with `SingletonTestScope` to substitute singletons in tests.
- Scene loading abstraction: `SceneReference` / `ISceneLoader` with `BuildSceneReference` (build profile
  scene list) and `AddressableSceneReference` (separate assembly, enabled when Addressables is installed).
- `ScopeOperation` result for loads and unloads (`Succeeded`, `Exception`, `Completed`, yieldable).
- `ScopeHandler.IsLoaded`, `TryGetRuntimeScope`, `Scopes`, `ScopeLoaded` / `ScopeUnloaded` events.
- `RuntimeScope.Root` (scope-lifetime GameObject), `State`, `Managers`, `Scene`, `StartCoroutine`,
  `TryGetSceneGameObject` / `TryGetSceneComponent`.
- Scope inspector: scene with loader picker (None / Build Profile / Addressables) and Open button,
  ordered manager list with a searchable type picker, provided singleton contracts per manager,
  unresolved (renamed) manager types shown with their stored type name, and a Health section.
- Optional declared parent (`Scope.DeclaredParent`); loading under a different parent logs a warning.
- `RequiresSingletonAttribute` for dependencies that are not `Singleton<T>` fields.
- `ScopeValidator` (editor, callable from tests/CI): unresolved/empty managers, required singletons not
  provided along the declared parents, singletons provided twice, declared-parent cycles, missing or
  unregistered scenes, scenes without exactly one `SceneRoot`, duplicate/empty `SceneLink` keys.
  `Tools > Scopes > Validate All` lists every issue with click-to-select.
- `Assets > Create > Scopes > Scope with Scene (Build Profile / Addressables)`: creates the Scope and a
  scene from the Scope Scene template and registers the scene with the loader.

### Changed
- **Breaking:** managers are plain `[Serializable]` C# classes stored in the Scope asset
  (`[SerializeReference]`) instead of MonoBehaviours on a prefab. Each load works on a copy.
- **Breaking:** `Scope` holds a `SceneReference` instead of an Addressables `AssetReference`;
  Addressables is no longer a dependency.
- **Breaking:** `LoadScope`/`UnloadScope` return a `ScopeOperation` and run immediately (queued, one
  at a time) instead of returning an `IEnumerator` with a done callback.
- **Breaking:** `Bootstrap` references the root `Scope` directly.
- `ScopeHandler` and `CoroutineHandler` create a persistent host on first use; hand-placed instances are
  reused and never duplicated.
- `SceneUpdate`/`SceneFixedUpdate` are driven by the scope root, so scene-less scopes receive them too;
  they start once the scope is fully loaded.
- Managers uninitialize in reverse order; singletons are removed after the last `Uninitialize`.

### Removed
- `ScopeHandler.GetSingleton` and the obsolete `GetManager` (use `Singleton<T>` or `SingletonRegistry`).

### Fixed
- An exception in `Initialize`/`Uninitialize` no longer deadlocks all later scope operations; a failed
  load cleans up the partially loaded scope.
- `RootScope` is cleared when the root scope unloads.
- `OnApplicationQuit` no longer throws when nothing is loaded.
- `CoroutineHandler` runners return to the pool when their routine throws.
- Duplicate singleton registrations, missing singletons and missing/duplicate `SceneLink` keys report
  descriptive errors.
