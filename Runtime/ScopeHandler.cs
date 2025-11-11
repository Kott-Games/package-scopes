using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using UnityEngine.SceneManagement;

namespace ToG.Scopes
{

    [StructLayout(LayoutKind.Sequential, Size = 1)]
    public struct ScopeManagerUpdate
    { }

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
                    PlayerLoopSystem[] subSystems = rootSystem.subSystemList[i].subSystemList;
                    PlayerLoopSystem[] subSystemsEdited = new PlayerLoopSystem[subSystems.Length + 1];

                    subSystemsEdited[0] = new PlayerLoopSystem()
                    {
                        type = typeof(ScopeManagerUpdate),
                        updateDelegate = ManagerUpdate,
                    };

                    subSystems.CopyTo(subSystemsEdited, 1);

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

        public static IEnumerator InitializeManagers(Manager[] managers, IProgress<ScopeProgress> progress)
        {
            SubProgress subProgress = new SubProgress(progress);

            for (int i = 0; i < managers.Length; i++)
            {
                Manager manager = managers[i];
                _managers.Add(manager);

                subProgress.Scale = 1f / managers.Length;
                subProgress.Start = new ScopeProgress(i * subProgress.Scale, $"Initialize {manager.name}");
                progress.Report(subProgress.Start);

                yield return manager.InitializeAsync(subProgress);

                subProgress.Report(new ScopeProgress(1f, null));
            }
        }

        public static IEnumerator BeginScope(Scope scope, IProgress<ScopeProgress> progress)
        {
            ScopeProgress progressData = new ScopeProgress();
            progressData.Info = "Begin Scope";
            progressData.Progress = 0f;
            progress.Report(progressData);

            SubProgress subProgress = new SubProgress(progress);

            subProgress.Scale = 0.5f;
            subProgress.Start = new ScopeProgress(0f, $"Initialize managers");
            yield return InitializeManagers(scope.Managers, subProgress);

            subProgress.Start = new ScopeProgress(0.5f, $"Load scene {scope.SceneRef.SubObjectName}");
            subProgress.Scale = 0.3f;
            progress.Report(subProgress.Start);

            var loadSceneHandle = scope.SceneRef.LoadSceneAsync(LoadSceneMode.Additive, false);
            yield return new WaitForHandle(loadSceneHandle, subProgress);
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
                yield return new WaitForHandle(unloadSceneHandle, subProgress);
                var currentScene = unloadSceneHandle.Result;

                _currentScope = null;
            }

            progressData.Info = $"Activate scene {scene.Scene.name}";
            progressData.Progress = 0.9f;
            progress.Report(progressData);

            yield return scene.ActivateAsync();
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

        public static async Task EndCurrentScope()
        {
            await Task.Delay(0);
        }
    }
}
