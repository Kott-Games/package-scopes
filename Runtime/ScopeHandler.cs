using Kott.Utilities.Addressables;
using Kott.Utilities.Progress;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using UnityEngine.SceneManagement;

namespace Kott.Scopes
{
    [StructLayout(LayoutKind.Sequential, Size = 1)]
    public struct ScopeManagerUpdate { }

    public static class ScopeHandler
    {
        private static Scope _currentScope;
        private static List<Manager> _managers = new();

        [RuntimeInitializeOnLoadMethod]
        private static void UpdateLoopManagement()
        {
            PlayerLoopSystem rootSystem = PlayerLoop.GetDefaultPlayerLoop();

            for (int i = 0; i < rootSystem.subSystemList.Length; i++)
            {
                if (rootSystem.subSystemList[i].type == typeof(Update))
                {
                    rootSystem.subSystemList[i].subSystemList = rootSystem.subSystemList[i].subSystemList
                        .Prepend(new PlayerLoopSystem()
                        {
                            type = typeof(ScopeManagerUpdate),
                            updateDelegate = ManagerUpdate,
                        })
                        .ToArray();
                    break;
                }
            }

            PlayerLoop.SetPlayerLoop(rootSystem);
        }

        private static void ManagerUpdate()
        {
            for (int i = 0; i < _managers.Count; i++)
            {
                _managers[i].Update();
            }
        }

        public static IEnumerator InitializeManagers(IList<Manager> managers, IProgress<ProgressReport> progress)
        {
            if (managers == null || managers.Count == 0) yield break;

            SubProgress subProgress = new(progress);
            subProgress.Scale = 1f / managers.Count;

            for (int i = 0; i < managers.Count; i++)
            {
                Manager manager = managers[i];
                _managers.Add(manager);

                subProgress.Start = new(i * subProgress.Scale, $"Initialize manager {manager.name}");
                progress.Report(subProgress.Start);

                yield return manager.Initialize(progress);
            }
        }

        public static IEnumerator BeginScope(Scope scope, IProgress<ProgressReport> progress)
        {
            progress.Report(new(0f, "Begin Scope"));
            SubProgress subProgress = new(progress);

            // manager initialization - 50%
            subProgress.Scale = 0.5f;
            subProgress.Start = new(0f, null);
            yield return InitializeManagers(scope.Managers, subProgress);

            // scene loading - 30%
            subProgress.Scale = 0.3f;
            subProgress.Start = new(0.5f, $"Load scene {scope.SceneRef.SubObjectName}");
            progress.Report(subProgress.Start);

            var loadSceneHandle = scope.SceneRef.LoadSceneAsync(LoadSceneMode.Additive, false);
            yield return new WaitForAsyncOperationHandleWithProgress(loadSceneHandle, subProgress);
            var scene = loadSceneHandle.Result;

            if (_currentScope != null)
            {
                for (int i = 0; i < _currentScope.Managers.Length; i++)
                {
                    Manager manager = scope.Managers[i];
                    _managers.Remove(manager);
                }

                // scene unloading - 10%
                subProgress.Scale = 0.1f;
                subProgress.Start = new(0.8f, $"Unload scene {scope.SceneRef.SubObjectName}");
                progress.Report(subProgress.Start);

                var unloadSceneHandle = _currentScope.SceneRef.UnLoadScene();
                yield return new WaitForAsyncOperationHandleWithProgress(unloadSceneHandle, subProgress);
                var currentScene = unloadSceneHandle.Result;

                _currentScope = null;
            }

            // scene activation - 10%
            subProgress.Scale = 0.1f;
            subProgress.Start = new(0.9f, $"Activate scene {scene.Scene.name}");
            progress.Report(subProgress.Start);

            yield return new WaitForAsyncOperationWithProgress(scene.ActivateAsync(), subProgress);
            SceneManager.SetActiveScene(scene.Scene);

            for (int i = 0; i < scope.Managers.Length; i++)
            {
                Manager manager = scope.Managers[i];
                manager.OnSceneLoaded();
            }

            yield return scene.ActivateAsync();

            progress.Report(new(1f, "Done"));

            _currentScope = scope;
        }
    }
}
