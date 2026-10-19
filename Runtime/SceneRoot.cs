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
        private Scope _scope;

        private void Awake()
        {
            Scope activating = ScopeSystem.ActivatingScope;
            if (activating == null)
            {
                ScopeLogging.Current.Log(LogType.Warning, (object)(
                    $"SceneRoot in scene '{gameObject.scene.name}' was not loaded through a scope; nothing is injected " +
                    "and manager scene callbacks are not invoked."), this);
                return;
            }

            if (activating.SceneBound)
            {
                activating.Logger.Log(LogType.Warning, (object)
                    $"The scene of {activating} has more than one SceneRoot; '{name}' is ignored.", this);
                return;
            }

            _scope = activating;
            _scope.ActivateScene(gameObject.scene);
        }

        private void Start()
        {
            if (_scope is { SceneError: null })
            {
                _scope.InvokeSceneStart();
            }
        }
    }
}
