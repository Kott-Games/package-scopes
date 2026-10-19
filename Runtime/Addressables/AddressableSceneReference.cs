using System;
using System.Collections;
using Kott.Utilities.Progress;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Kott.Scopes
{
    /// <summary>
    ///     A scene loaded through Addressables. Available when the Addressables package is installed.
    /// </summary>
    [Serializable]
    public sealed class AddressableSceneReference : SceneReference
    {
        [SerializeField]
        private AssetReference _scene = new(string.Empty);

        /// <summary>Creates an unassigned reference.</summary>
        public AddressableSceneReference()
        { }

        /// <summary>Creates a reference to the given addressable scene.</summary>
        public AddressableSceneReference(AssetReference scene)
        {
            _scene = scene;
        }

        /// <summary>The addressable scene.</summary>
        public AssetReference Scene => _scene;

        /// <inheritdoc />
        public override bool IsAssigned => _scene != null && _scene.RuntimeKeyIsValid();

        /// <inheritdoc />
        public override string DisplayName
        {
            get
            {
#if UNITY_EDITOR
                if (_scene?.editorAsset != null)
                {
                    return _scene.editorAsset.name;
                }
#endif
                return _scene?.RuntimeKey?.ToString();
            }
        }

        /// <inheritdoc />
        public override ISceneLoader CreateLoader(ILogger logger)
        {
            return new AddressableSceneLoader(_scene.RuntimeKey, DisplayName);
        }
    }

    internal sealed class AddressableSceneLoader : ISceneLoader
    {
        private readonly object _key;
        private readonly string _displayName;
        private AsyncOperationHandle<SceneInstance> _handle;

        public AddressableSceneLoader(object key, string displayName)
        {
            _key = key;
            _displayName = displayName;
        }

        public Scene Scene { get; private set; }

        public IEnumerator Load(IProgress<ProgressReport> progress)
        {
            _handle = Addressables.LoadSceneAsync(_key, LoadSceneMode.Additive, activateOnLoad: true);
            yield return new WaitForAsyncOperationHandleWithProgress(_handle, progress);

            if (_handle.Status != AsyncOperationStatus.Succeeded)
            {
                Exception exception = _handle.OperationException;
                _handle.Release();
                _handle = default; // nothing left for Unload to do
                throw new InvalidOperationException($"Loading addressable scene '{_displayName}' failed.", exception);
            }

            Scene = _handle.Result.Scene;
            SceneManager.SetActiveScene(Scene);
            progress?.Report(new ProgressReport(1f, null));
        }

        public IEnumerator Unload(IProgress<ProgressReport> progress)
        {
            if (!_handle.IsValid())
            {
                yield break;
            }

            // keep the handle until we checked the result (auto release would invalidate it on completion)
            AsyncOperationHandle<SceneInstance> unloadHandle =
                Addressables.UnloadSceneAsync(_handle, autoReleaseHandle: false);
            yield return new WaitForAsyncOperationHandleWithProgress(unloadHandle, progress);

            AsyncOperationStatus status = unloadHandle.Status;
            Exception exception = unloadHandle.OperationException;
            unloadHandle.Release();

            if (status != AsyncOperationStatus.Succeeded)
            {
                throw new InvalidOperationException($"Unloading addressable scene '{_displayName}' failed.", exception);
            }

            Scene = default;
            progress?.Report(new ProgressReport(1f, null));
        }
    }
}
