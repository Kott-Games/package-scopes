using System;
using System.Collections;
using System.IO;
using Kott.Utilities.Progress;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kott.Scopes
{
    /// <summary>
    ///     A scene from the scene list of the build profile, loaded through <see cref="SceneManager" />.
    /// </summary>
    [Serializable]
    public sealed class BuildSceneReference : SceneReference
    {
        [SerializeField]
        private string _scenePath;

        // editor only: keeps the reference intact when the scene asset is moved (see BuildSceneReferenceDrawer)
        [SerializeField]
        private string _sceneGuid;

        /// <summary>Creates an unassigned reference.</summary>
        public BuildSceneReference()
        { }

        /// <summary>Creates a reference to the scene at <paramref name="scenePath" /> (e.g. <c>Assets/Scenes/Menu.unity</c>).</summary>
        public BuildSceneReference(string scenePath)
        {
            _scenePath = scenePath;
        }

        internal BuildSceneReference(string scenePath, string sceneGuid)
        {
            _scenePath = scenePath;
            _sceneGuid = sceneGuid;
        }

        /// <summary>Project-relative path of the scene asset.</summary>
        public string ScenePath => _scenePath;

        /// <summary>Asset GUID of the scene (editor bookkeeping; empty if assigned through code).</summary>
        internal string SceneGuid => _sceneGuid;

        /// <inheritdoc />
        public override bool IsAssigned => !string.IsNullOrEmpty(_scenePath);

        /// <inheritdoc />
        public override string DisplayName => Path.GetFileNameWithoutExtension(_scenePath);

        /// <inheritdoc />
        public override ISceneLoader CreateLoader()
        {
            return new BuildSceneLoader(_scenePath);
        }
    }

    internal sealed class BuildSceneLoader : ISceneLoader
    {
        private readonly string _scenePath;

        public BuildSceneLoader(string scenePath)
        {
            _scenePath = scenePath;
        }

        public Scene Scene { get; private set; }

        public IEnumerator Load(IProgress<ProgressReport> progress)
        {
            if (SceneUtility.GetBuildIndexByScenePath(_scenePath) < 0)
            {
                throw new InvalidOperationException(
                    $"Scene '{_scenePath}' is not in the scene list of the build profile. Add it there, or reference " +
                    "it through Addressables instead.");
            }

            AsyncOperation operation = SceneManager.LoadSceneAsync(_scenePath, LoadSceneMode.Additive);
            if (operation == null)
            {
                throw new InvalidOperationException($"Loading scene '{_scenePath}' could not be started.");
            }

            yield return new WaitForAsyncOperationWithProgress(operation, progress);

            Scene = FindLoadedScene();
            if (!Scene.IsValid())
            {
                throw new InvalidOperationException($"Scene '{_scenePath}' was not found after loading.");
            }

            SceneManager.SetActiveScene(Scene);
            progress?.Report(new ProgressReport(1f, null));
        }

        public IEnumerator Unload(IProgress<ProgressReport> progress)
        {
            Scene scene = Scene.IsValid() ? Scene : FindLoadedScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                yield break;
            }

            AsyncOperation operation = SceneManager.UnloadSceneAsync(scene);
            if (operation == null)
            {
                throw new InvalidOperationException($"Unloading scene '{_scenePath}' could not be started.");
            }

            yield return new WaitForAsyncOperationWithProgress(operation, progress);
            Scene = default;
            progress?.Report(new ProgressReport(1f, null));
        }

        private Scene FindLoadedScene()
        {
            // newest first: the scene loaded last is at the end of the list
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.path == _scenePath && scene.isLoaded)
                {
                    return scene;
                }
            }

            return default;
        }
    }
}
