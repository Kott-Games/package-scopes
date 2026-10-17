using System;
using UnityEngine;

namespace Kott.Scopes
{
    /// <summary>
    ///     Connects a scope's scene to its scope before other scene objects' <c>Awake</c>: binds
    ///     <see cref="SceneLink" />s, fills the managers' <see cref="InjectSceneAttribute" /> fields, injects the
    ///     <see cref="InjectAttribute" /> fields of all scene components, then calls
    ///     <see cref="Manager.OnSceneAwake" /> (and <see cref="Manager.OnSceneStart" /> before other <c>Start</c>s).
    ///     Every scope scene needs exactly one (the Scope Scene template has it).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9001)]
    [AddComponentMenu("Scopes/Scene Root")]
    public sealed class SceneRoot : MonoBehaviour
    {
        /// <summary>The scope this scene belongs to; <c>null</c> if the scene was not loaded through a scope.</summary>
        public Scope Scope { get; private set; }

        private void Awake()
        {
            Scope activating = ScopeSystem.ActivatingScope;
            if (activating == null)
            {
                Debug.LogWarning(
                    $"SceneRoot in scene '{gameObject.scene.name}' was not loaded through a scope; nothing is injected " +
                    "and manager scene callbacks are not invoked.", this);
                return;
            }

            if (activating.SceneBound)
            {
                Debug.LogWarning($"The scene of {activating} has more than one SceneRoot; '{name}' is ignored.", this);
                return;
            }

            Scope = activating;

            try
            {
                Scope.BindScene(gameObject.scene);
            }
            catch (Exception exception)
            {
                // fails the load once the scene finished loading
                Scope.SceneError = exception;
                return;
            }

            Scope.InvokeSceneAwake();
        }

        private void Start()
        {
            if (Scope is { SceneError: null })
            {
                Scope.InvokeSceneStart();
            }
        }
    }
}
