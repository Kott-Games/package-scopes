using UnityEngine;

namespace Kott.Scopes
{
    /// <summary>
    ///     Makes a scene object reachable from the scope's managers by key: through
    ///     <see cref="InjectSceneAttribute" /> fields or <see cref="Scope.GetSceneComponent{T}" />. Keys must be
    ///     unique within a scene.
    /// </summary>
    [AddComponentMenu("Scopes/Scene Link")]
    public sealed class SceneLink : MonoBehaviour
    {
        [SerializeField]
        private string _key;

        /// <summary>The key managers look this object up by.</summary>
        public string Key => _key;
    }
}
