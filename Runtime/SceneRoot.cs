using System;
using UnityEngine;

namespace Kott.Scopes
{
    /// <summary>
    ///     Connects a scope's scene to its managers: binds <see cref="SceneLink" />s and calls
    ///     <see cref="Manager.SceneAwake" /> / <see cref="Manager.SceneStart" /> before other scene objects'
    ///     <c>Awake</c> / <c>Start</c>. Every scope scene needs exactly one (the ScopeScene template has it).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9001)]
    [AddComponentMenu("Scopes/Scene Root")]
    public sealed class SceneRoot : MonoBehaviour
    {
        /// <summary>The scope this scene belongs to; <c>null</c> if the scene was not loaded through a scope.</summary>
        public RuntimeScope RuntimeScope { get; private set; }

        private void Awake()
        {
            RuntimeScope activating = ScopeSystem.ActivatingScope;
            if (activating == null)
            {
                Debug.LogWarning(
                    $"SceneRoot in scene '{gameObject.scene.name}' was not loaded through a scope; manager scene callbacks are not invoked.",
                    this);
                return;
            }

            if (activating.SceneBound)
            {
                Debug.LogWarning($"The scene of {activating} has more than one SceneRoot; '{name}' is ignored.", this);
                return;
            }

            RuntimeScope = activating;

            try
            {
                RuntimeScope.BindScene(gameObject.scene);
            }
            catch (Exception exception)
            {
                // fails the load once the scene finished loading
                RuntimeScope.SceneError = exception;
                return;
            }

            RuntimeScope.InvokeSceneAwake();
        }

        private void Start()
        {
            if (RuntimeScope is { SceneError: null })
            {
                RuntimeScope.InvokeSceneStart();
            }
        }
    }
}
