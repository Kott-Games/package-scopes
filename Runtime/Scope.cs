using System.Collections.Generic;
using UnityEngine;

namespace Kott.Scopes
{
    /// <summary>
    ///     A unit of game state: an ordered list of <see cref="Manager" />s and an optional scene.
    ///     Load it with <see cref="ScopeHandler.LoadScope" />.
    /// </summary>
    [CreateAssetMenu(fileName = "NewScope", menuName = "Scopes/Scope", order = 0)]
    public sealed class Scope : ScriptableObject
    {
        [SerializeReference] private SceneReference _scene;
        [SerializeReference] private List<Manager> _managers = new();

        /// <summary>The scene loaded with this scope, or <c>null</c> for a scene-less scope.</summary>
        public SceneReference Scene => _scene;

        /// <summary>
        ///     The managers in initialization order. These are the authored instances; a loaded scope works on
        ///     copies (see <see cref="RuntimeScope.Managers" />). Entries are <c>null</c> if their type no longer exists.
        /// </summary>
        public IReadOnlyList<Manager> Managers => _managers;

        internal void SetScene(SceneReference scene)
        {
            _scene = scene;
        }

        internal void SetManagers(IEnumerable<Manager> managers)
        {
            _managers = new List<Manager>(managers);
        }
    }
}
