using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using UnityEngine.ResourceManagement.AsyncOperations;
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

        public static IEnumerator BeginScope(Scope scope, IProgress<ScopeProgress> progress)
        {
            ScopeProgress progressData = new ScopeProgress();
            progressData.Info = "Begin Scope";
            progressData.Progress = 0f;
            progress.Report(progressData);

            var subProgress = new MultiScopeProgress(progress);

            for (int i = 0; i < scope.Managers.Length; i++)
            {
                Manager manager = scope.Managers[i];
                _managers.Add(manager);

                subProgress.Scale = 0.5f / scope.Managers.Length;
                subProgress.Start = new ScopeProgress(i * subProgress.Scale, $"Initialize manager {manager.name}");
                progress.Report(subProgress.Start);

                yield return manager.Initialize(progress); // TODO handle progress
            }

            subProgress.Start = new ScopeProgress(0.5f, $"Load scene {scope.SceneRef.SubObjectName}");
            subProgress.Scale = 0.3f;
            progress.Report(subProgress.Start);

            var loadSceneHandle = scope.SceneRef.LoadSceneAsync(LoadSceneMode.Additive, false);
            yield return loadSceneHandle; // TODO handle progress
            var scene = loadSceneHandle.Result;

            if (_currentScope != null)
            {
                for (int i = 0; i < _currentScope.Managers.Length; i++)
                {
                    Manager manager = scope.Managers[i];
                    _managers.Remove(manager);
                }

                subProgress.Start = new ScopeProgress(0.8f, $"Unload scene {scope.SceneRef.SubObjectName}");
                subProgress.Scale = 0.1f;
                progress.Report(subProgress.Start);

                var unloadSceneHandle = _currentScope.SceneRef.UnLoadScene();
                yield return unloadSceneHandle; // TODO handle progress
                var currentScene = unloadSceneHandle.Result;

                _currentScope = null;
            }

            progressData.Info = $"Activate scene {scene.Scene.name}";
            progressData.Progress = 0.9f;
            progress.Report(progressData);

            yield return scene.ActivateAsync(); // TODO handle progress
            SceneManager.SetActiveScene(scene.Scene);

            for (int i = 0; i < scope.Managers.Length; i++)
            {
                Manager manager = scope.Managers[i];
                manager.OnSceneLoaded();
            }

            progressData.Info = "Done";
            progressData.Progress = 1f;
            progress.Report(progressData);

            _currentScope = scope;
        }

        public static async Task<T> HandleWithProgress<T>(AsyncOperationHandle<T> handle, IProgress<ScopeProgress> progress)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            var loadSceneReport = ReportProgress(handle, cts.Token, progress);

            await Task.WhenAny(handle.Task, loadSceneReport);
            cts.Cancel();

            return handle.Result;
        }

        private static async Task ReportProgress<T>(AsyncOperationHandle<T> handle,
            CancellationToken cancellationToken, IProgress<ScopeProgress> progress)
        {
            float p = 0f;
            while (!cancellationToken.IsCancellationRequested)
            {
                if (handle.PercentComplete > p)
                {
                    p = handle.PercentComplete;
                    progress.Report(new ScopeProgress(p, null));
                }

                await Task.Yield();
            }
        }

        public static async Task EndCurrentScope()
        {
            await Task.Delay(0);
        }
    }
}
