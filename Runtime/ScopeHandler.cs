using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Kott.Utilities.Progress;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Kott.Scopes
{
    public class ScopeHandler : MonoBehaviour
    {
        public static ScopeHandler Instance { get; private set; }
        public RuntimeScope RootScope { get; private set; }

        private readonly Dictionary<Type, ISingletonManager> _singletonManagerLookup = new();

        // used to reference the currently loading or unloading scope
        private RuntimeScope _activeScope;

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnApplicationQuit()
        {
            Stack<RuntimeScope> collectionStack = new();
            Stack<RuntimeScope> executionStack = new();

            collectionStack.Push(RootScope);

            while (collectionStack.TryPop(out RuntimeScope runtimeScope))
            {
                executionStack.Push(runtimeScope);
                foreach (RuntimeScope child in runtimeScope.Children) collectionStack.Push(child);
            }

            while (executionStack.TryPop(out RuntimeScope runtimeScope))
                foreach (IManager manager in runtimeScope.Managers)
                    manager.SceneExit();
        }

        private void RegisterSingletonManager(RuntimeScope runtimeScope, ISingletonManager singletonManager)
        {
            Type managerType = singletonManager.GetType();
            _singletonManagerLookup.Add(managerType, singletonManager);
            runtimeScope.SingleManagerTypes.Add(managerType);

            IEnumerable<Type> contractTypes = managerType.GetInterfaces()
                .Where(t => t.IsGenericType && t.GetGenericTypeDefinition().IsEquivalentTo(typeof(IManagerContract<>)))
                .Select(t => t.GetGenericArguments()[0]);

            foreach (Type interfaceType in contractTypes)
            {
                _singletonManagerLookup.Add(interfaceType, singletonManager);
                runtimeScope.SingleManagerTypes.Add(interfaceType);
            }
        }

        private void UnregisterSingletonManagers(RuntimeScope runtimeScope)
        {
            foreach (Type key in runtimeScope.SingleManagerTypes) _singletonManagerLookup.Remove(key);

            runtimeScope.SingleManagerTypes.Clear();
        }

        private IEnumerator InitializeManagers(RuntimeScope runtimeScope, IProgress<ProgressReport> progress)
        {
            if (runtimeScope.Managers.Count == 0) yield break;

            foreach (IManager manager in runtimeScope.Managers)
            {
                if (manager is not ISingletonManager singletonManager) continue;
                RegisterSingletonManager(runtimeScope, singletonManager);
            }

            SubProgress subProgress = new(progress);
            subProgress.Scale = 1f / runtimeScope.Managers.Count;

            for (int i = 0; i < runtimeScope.Managers.Count; i++)
            {
                IManager manager = runtimeScope.Managers[i];

                subProgress.Start = new ProgressReport(i * subProgress.Scale, $"Initialize {manager.GetType().Name}");
                progress?.Report(subProgress.Start);

                yield return manager.Initialize(subProgress);
                subProgress.Report(new ProgressReport(1f, null)); // in case manager doesn't report completion
            }
        }

        private IEnumerator UninitializeManagers(RuntimeScope runtimeScope, IProgress<ProgressReport> progress)
        {
            if (runtimeScope.Managers.Count == 0) yield break;

            UnregisterSingletonManagers(runtimeScope);

            SubProgress subProgress = new(progress);
            subProgress.Scale = 1f / runtimeScope.Managers.Count;

            for (int i = 0; i < runtimeScope.Managers.Count; i++)
            {
                IManager manager = runtimeScope.Managers[i];

                subProgress.Start = new ProgressReport(i * subProgress.Scale, $"Uninitialize {manager.GetType().Name}");
                progress?.Report(subProgress.Start);

                yield return manager.Uninitialize(subProgress);
                subProgress.Report(new ProgressReport(1f, null)); // in case manager doesn't report completion
            }
        }

        private static IEnumerator LoadScene(RuntimeScope runtimeScope, IProgress<ProgressReport> progress)
        {
            if (!runtimeScope.Scope.SceneRef.RuntimeKeyIsValid()) yield break;

            SubProgress subProgress = new(progress);

            // scene loading - 90%
            subProgress.Scale = 0.9f;
            subProgress.Start = new ProgressReport(0f, $"Load scene {runtimeScope.Scope.SceneRef.SubObjectName}");
            progress?.Report(subProgress.Start);

            AsyncOperationHandle<SceneInstance> loadSceneHandle = runtimeScope.Scope.SceneRef
                .LoadSceneAsync(LoadSceneMode.Additive, false);
            yield return new WaitForAsyncOperationHandleWithProgress(loadSceneHandle, subProgress);
            runtimeScope.SceneInstance = loadSceneHandle.Result;

            // scene activation - 10%
            subProgress.Scale = 0.1f;
            subProgress.Start = new ProgressReport(0.9f, $"Activate scene {runtimeScope.SceneInstance.Scene.name}");
            progress?.Report(subProgress.Start);

            AsyncOperation activateSceneOp = runtimeScope.SceneInstance.ActivateAsync();
            yield return new WaitForAsyncOperationWithProgress(activateSceneOp, subProgress);
            SceneManager.SetActiveScene(runtimeScope.SceneInstance.Scene);
        }

        private static IEnumerator UnloadScene(RuntimeScope runtimeScope, IProgress<ProgressReport> progress)
        {
            if (!runtimeScope.Scope.SceneRef.IsValid()) yield break;

            SubProgress subProgress = new(progress);

            // notify managers about scene change
            foreach (IManager manager in runtimeScope.Managers) manager.SceneExit();

            // scene loading - 90%
            subProgress.Scale = 0.9f;
            subProgress.Start = new ProgressReport(0f, $"Unload scene {runtimeScope.Scope.SceneRef.SubObjectName}");
            progress?.Report(subProgress.Start);

            AsyncOperationHandle<SceneInstance> unloadSceneHandle = runtimeScope.Scope.SceneRef.UnLoadScene();
            yield return new WaitForAsyncOperationHandleWithProgress(unloadSceneHandle, subProgress);
        }

        internal void SceneAwake(SceneRoot sceneRoot)
        {
            if (_activeScope == null) return;

            _activeScope.CollectSceneLinks();

            foreach (IManager manager in _activeScope.Managers) manager.SceneAwake();
        }

        internal void SceneStart(SceneRoot sceneRoot)
        {
            if (_activeScope == null) return;

            foreach (IManager manager in _activeScope.Managers) manager.SceneStart();
        }

        public IEnumerator LoadScope(Scope scope, IProgress<ProgressReport> progress = null)
        {
            return LoadScope(scope, null, progress);
        }

        public IEnumerator LoadScope(Scope scope, RuntimeScope parent, IProgress<ProgressReport> progress = null)
        {
            GameObject managerContainer = Instantiate(scope.ManagerContainer, transform);
            managerContainer.name = $"Managers '{scope.name}'";

            RuntimeScope runtimeScope = new(scope, parent, managerContainer);
            RootScope ??= runtimeScope;
            parent?.ChildrenInternal.Insert(0, runtimeScope);

            _activeScope = runtimeScope; // set active loading scope

            progress?.Report(new ProgressReport(0f, $"Load Scope {runtimeScope.Scope.name}"));
            SubProgress subProgress = new(progress);

            // manager initialization - 50%
            subProgress.Scale = 0.5f;
            subProgress.Start = new ProgressReport(0f, null);
            yield return InitializeManagers(runtimeScope, subProgress);

            // scene loading - 50%
            subProgress.Scale = 0.5f;
            subProgress.Start = new ProgressReport(0.5f, null);
            yield return LoadScene(runtimeScope, subProgress);

            progress?.Report(new ProgressReport(1f, "Done"));

            _activeScope = null; // unset active loading scope
        }

        public IEnumerator UnloadScopeRoutine(RuntimeScope runtimeScope, IProgress<ProgressReport> progress = null)
        {
            runtimeScope.Parent?.ChildrenInternal.Remove(runtimeScope);
            while (runtimeScope.ChildrenInternal.Count > 0)
            {
                RuntimeScope child = runtimeScope.ChildrenInternal[0];
                runtimeScope.ChildrenInternal.RemoveAt(0);
                yield return UnloadScopeRoutine(child, progress);
            }
            
            _activeScope = runtimeScope; // set active unloading scope

            progress?.Report(new ProgressReport(0f, $"Load Scope {runtimeScope.Scope.name}"));
            SubProgress subProgress = new(progress);

            // scene unloading - 50%
            subProgress.Scale = 0.5f;
            subProgress.Start = new ProgressReport(0f, null);
            yield return UnloadScene(runtimeScope, subProgress);

            // manager uninitialization - 50%
            subProgress.Scale = 0.5f;
            subProgress.Start = new ProgressReport(0.5f, null);
            yield return UninitializeManagers(runtimeScope, subProgress);

            progress?.Report(new ProgressReport(1f, "Done"));

            _activeScope = null; // unset active unloading scope
        }

        public T GetSingletonManager<T>()
            where T : ISingletonManager
        {
            Type key = typeof(T);
            return (T)_singletonManagerLookup[key];
        }
    }
}