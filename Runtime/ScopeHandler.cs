using Kott.Utilities.Progress;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Kott.Scopes
{
    public class ScopeHandler : MonoBehaviour
    {
        public static ScopeHandler Instance { get; private set; }

        private class RuntimeScope
        {
            public Scope Scope { get; }
            public HashSet<Type> ManagerLookupKeys { get; } = new();
            public HashSet<string> SceneLinksKeys { get; } = new();
            public SceneInstance SceneInstance { get; set; }

            public RuntimeScope(Scope scope)
            {
                Scope = scope;
            }
        }

        private Stack<RuntimeScope> _scopes = new();
        private Dictionary<Type, IManager> _managerLookup = new();
        private Dictionary<string, SceneLink> _sceneLinks = new();

        private RuntimeScope _activeScope;

        [RuntimeInitializeOnLoadMethod]
        private static void UpdateLoopManagement()
        {
            PlayerLoopSystem rootSystem = PlayerLoop.GetDefaultPlayerLoop();

            for (int i = 0; i < rootSystem.subSystemList.Length; i++)
            {
                if (rootSystem.subSystemList[i].type == typeof(Update))
                {
                    InsertLoopSystem<ScopeManagerUpdate>(
                        ref rootSystem.subSystemList[i].subSystemList,
                        Instance.ManagerUpdate);
                }
            }

            PlayerLoop.SetPlayerLoop(rootSystem);

            StringBuilder sb = new();
            PrintLoopSystems(sb, rootSystem);
            Debug.Log(sb.ToString());
        }

        private static void InsertLoopSystem<T>(ref PlayerLoopSystem[] systems, PlayerLoopSystem.UpdateFunction updateDelegate)
        {
            PlayerLoopSystem[] systemsEdited = new PlayerLoopSystem[systems.Length + 1];

            systemsEdited[0] = new PlayerLoopSystem()
            {
                type = typeof(T),
                updateDelegate = updateDelegate,
            };

            systems.CopyTo(systemsEdited, 1);
            systems = systemsEdited;
        }

        private static void PrintLoopSystems(StringBuilder sb, PlayerLoopSystem system, string indent = "")
        {
            sb.AppendLine($"{indent}{system.type?.Name}");
            if (system.subSystemList == null) return;
            foreach (var subSystem in system.subSystemList)
            {
                PrintLoopSystems(sb, subSystem, indent + "  ");
            }
        }

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void ManagerUpdate()
        {
            foreach (RuntimeScope runtimeScope in _scopes)
            {
                foreach (Manager manager in runtimeScope.Scope.Managers)
                {
                    manager.Update();
                }
            }
        }

        private void RegisterManager(RuntimeScope runtimeScope, Manager manager)
        {
            Type managerType = manager.GetType();
            runtimeScope.ManagerLookupKeys.Add(managerType);
            _managerLookup.Add(managerType, manager);

            IEnumerable<Type> interfaceTypes = managerType.GetInterfaces()
                .Where(t => t.IsGenericType && t.GetGenericTypeDefinition().IsEquivalentTo(typeof(IManagerContract<>)))
                .Select(t => t.GetGenericArguments()[0]);

            foreach (Type interfaceType in interfaceTypes)
            {
                runtimeScope.ManagerLookupKeys.Add(interfaceType);
                _managerLookup.Add(interfaceType, manager);
            }
        }

        private IEnumerator InitializeManagers(RuntimeScope runtimeScope, IProgress<ProgressReport> progress)
        {
            if (runtimeScope.Scope.Managers.Count == 0) yield break;

            foreach (Manager manager in runtimeScope.Scope.Managers)
            {
                RegisterManager(runtimeScope, manager);
            }

            SubProgress subProgress = new(progress);
            subProgress.Scale = 1f / runtimeScope.Scope.Managers.Count;

            for (int i = 0; i < runtimeScope.Scope.Managers.Count; i++)
            {
                Manager manager = runtimeScope.Scope.Managers[i];

                subProgress.Start = new(i * subProgress.Scale, $"Initialize {manager.GetType().Name}");
                progress.Report(subProgress.Start);

                yield return manager.Initialize(subProgress);
                subProgress.Report(new(1f, null)); // in case manager doesn't report completion
            }
        }

        private IEnumerator UninitializeManagers(RuntimeScope runtimeScope, IProgress<ProgressReport> progress)
        {
            foreach (Type key in runtimeScope.ManagerLookupKeys)
                _managerLookup.Remove(key);
            runtimeScope.ManagerLookupKeys.Clear();

            if (runtimeScope.Scope.Managers.Count == 0) yield break;

            SubProgress subProgress = new(progress);
            subProgress.Scale = 1f / runtimeScope.Scope.Managers.Count;

            for (int i = 0; i < runtimeScope.Scope.Managers.Count; i++)
            {
                Manager manager = runtimeScope.Scope.Managers[i];

                subProgress.Start = new(i * subProgress.Scale, $"Uninitialize {manager.GetType().Name}");
                progress.Report(subProgress.Start);

                yield return manager.Uninitialize(subProgress);
                subProgress.Report(new(1f, null)); // in case manager doesn't report completion
            }
        }

        private IEnumerator LoadScene(RuntimeScope runtimeScope, IProgress<ProgressReport> progress)
        {
            if (!runtimeScope.Scope.SceneRef.RuntimeKeyIsValid()) yield break;

            SubProgress subProgress = new(progress);

            // scene loading - 90%
            subProgress.Scale = 0.9f;
            subProgress.Start = new(0f, $"Load scene {runtimeScope.Scope.SceneRef.SubObjectName}");
            progress.Report(subProgress.Start);

            var loadSceneHandle = runtimeScope.Scope.SceneRef.LoadSceneAsync(LoadSceneMode.Additive, false);
            yield return new WaitForAsyncOperationHandleWithProgress(loadSceneHandle, subProgress);
            runtimeScope.SceneInstance = loadSceneHandle.Result;

            // scene activation - 10%
            subProgress.Scale = 0.1f;
            subProgress.Start = new(0.9f, $"Activate scene {runtimeScope.SceneInstance.Scene.name}");
            progress.Report(subProgress.Start);

            var activateSceneOp = runtimeScope.SceneInstance.ActivateAsync();
            yield return new WaitForAsyncOperationWithProgress(activateSceneOp, subProgress);
            SceneManager.SetActiveScene(runtimeScope.SceneInstance.Scene);
        }
        
        private IEnumerator UnloadScene(RuntimeScope runtimeScope, IProgress<ProgressReport> progress)
        {
            if (!runtimeScope.Scope.SceneRef.IsValid()) yield break;

            SubProgress subProgress = new(progress);

            // notify managers about scene change
            foreach (Manager manager in runtimeScope.Scope.Managers)
            {
                manager.SceneExit();
            }

            // scene loading - 90%
            subProgress.Scale = 0.9f;
            subProgress.Start = new(0f, $"Unload scene {runtimeScope.Scope.SceneRef.SubObjectName}");
            progress.Report(subProgress.Start);

            var unloadSceneHandle = runtimeScope.Scope.SceneRef.UnLoadScene();
            yield return new WaitForAsyncOperationHandleWithProgress(unloadSceneHandle, subProgress);

            // clear scene links
            foreach (string key in runtimeScope.SceneLinksKeys)
                _sceneLinks.Remove(key);
            runtimeScope.SceneLinksKeys.Clear();
        }

        internal void SceneAwake(SceneRoot sceneRoot)
        {
            if (_activeScope == null) return;

            List<SceneLink> sceneLinks = new();
            GameObject[] sceneObjects = _activeScope.SceneInstance.Scene.GetRootGameObjects();
            foreach (GameObject sceneObject in sceneObjects)
            {
                sceneLinks.Clear();
                sceneObject.GetComponentsInChildren(true, sceneLinks);
                foreach (SceneLink sceneLink in sceneLinks)
                {
                    _activeScope.SceneLinksKeys.Add(sceneLink.Key);
                    _sceneLinks.Add(sceneLink.Key, sceneLink);
                }
            }

            foreach (Manager manager in _activeScope.Scope.Managers)
            {
                manager.SceneAwake();
            }
        }

        internal void SceneStart(SceneRoot sceneRoot)
        {
            if (_activeScope == null) return;
            foreach (Manager manager in _activeScope.Scope.Managers)
            {
                manager.SceneStart();
            }
        }

        private IEnumerator LoadScope(RuntimeScope runtimeScope, IProgress<ProgressReport> progress)
        {
            progress.Report(new(0f, $"Load Scope {runtimeScope.Scope.name}"));
            SubProgress subProgress = new(progress);
            _activeScope = runtimeScope;

            // manager initialization - 50%
            subProgress.Scale = 0.5f;
            subProgress.Start = new(0f, null);
            yield return InitializeManagers(runtimeScope, subProgress);

            // scene loading - 50%
            subProgress.Scale = 0.5f;
            subProgress.Start = new(0.5f, null);
            yield return LoadScene(runtimeScope, subProgress);

            _activeScope = null;
            progress.Report(new(1f, "Done"));
        }

        private IEnumerator UnloadScope(RuntimeScope runtimeScope, IProgress<ProgressReport> progress)
        {
            progress.Report(new(0f, $"Load Scope {runtimeScope.Scope.name}"));
            SubProgress subProgress = new(progress);
            _activeScope = runtimeScope;

            // scene unloading - 50%
            subProgress.Scale = 0.5f;
            subProgress.Start = new(0f, null);
            yield return UnloadScene(runtimeScope, subProgress);

            // manager uninitialization - 50%
            subProgress.Scale = 0.5f;
            subProgress.Start = new(0.5f, null);
            yield return UninitializeManagers(runtimeScope, subProgress);

            _activeScope = null;
            progress.Report(new(1f, "Done"));
        }

        public IEnumerator PushScope(Scope scope, IProgress<ProgressReport> progress)
        {
            RuntimeScope runtimeScope = new(scope);
            _scopes.Push(runtimeScope);
            yield return LoadScope(runtimeScope, progress);
        }

        public IEnumerator PopScope(IProgress<ProgressReport> progress)
        {
            RuntimeScope runtimeScope = _scopes.Pop();
            yield return UnloadScope(runtimeScope, progress);
        }

        public IEnumerator SwitchScope(Scope scope, IProgress<ProgressReport> progress)
        {
            SubProgress subProgress = new(progress);

            subProgress.Scale = 0.5f;
            subProgress.Start = new(0f, null);
            yield return PopScope(progress);

            subProgress.Scale = 0.5f;
            subProgress.Start = new(0.5f, null);
            yield return PushScope(scope, progress);
        }

        public T GetManager<T>()
            where T : IManager
        {
            Type key = typeof(T);
            return (T)_managerLookup[key];
        }

        public GameObject GetSceneGameObject(string key)
        {
            return _sceneLinks[key].gameObject;
        }

        public T GetSceneComponent<T>(string key)
        {
            return _sceneLinks[key].GetComponent<T>();
        }
    }
}
