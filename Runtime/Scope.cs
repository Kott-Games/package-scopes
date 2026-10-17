using System.Collections.Generic;
using UnityEngine;

namespace Kott.Scopes
{
    /// <summary>
    ///     A unit of game state: an ordered list of <see cref="Manager" />s and an optional scene.
    ///     Load it with <see cref="ScopeHandler.LoadScope" />.
    /// </summary>
    /// <remarks>
    ///     Validate scopes in the editor with <c>ScopeValidator</c> (Kott.Scopes.Editor); its results are also shown
    ///     in the Scope inspector and under <c>Tools &gt; Scopes &gt; Validate All</c>.
    /// </remarks>
    [CreateAssetMenu(fileName = "NewScope", menuName = "Scopes/Scope", order = 0)]
    public sealed class Scope : ScriptableObject
    {
        internal const string SceneField = nameof(_scene);
        internal const string DeclaredParentField = nameof(_declaredParent);
        internal const string ManagersField = nameof(_managers);

        [SerializeReference]
        private SceneReference _scene;

        [SerializeField]
        [Tooltip("The scope this one is expected to be loaded under (optional). Used by validation; loading it under " +
                 "a different parent logs a warning.")]
        private Scope _declaredParent;

        [SerializeReference]
        private List<Manager> _managers = new();

        /// <summary>The scene loaded with this scope, or <c>null</c> for a scene-less scope.</summary>
        public SceneReference Scene => _scene;

        /// <summary>
        ///     The scope this one is expected to be loaded under, or <c>null</c>. Validation resolves the singletons
        ///     this scope requires through its declared parents; at runtime any parent is accepted, with a warning
        ///     if it differs.
        /// </summary>
        public Scope DeclaredParent => _declaredParent;

        /// <summary>
        ///     The managers in initialization order. These are the authored instances; a loaded scope works on
        ///     copies (see <see cref="RuntimeScope.Managers" />). Entries are <c>null</c> if their type no longer exists.
        /// </summary>
        public IReadOnlyList<Manager> Managers => _managers;

        internal void SetScene(SceneReference scene)
        {
            _scene = scene;
        }

        internal void SetDeclaredParent(Scope declaredParent)
        {
            _declaredParent = declaredParent;
        }

        internal void SetManagers(IEnumerable<Manager> managers)
        {
            _managers = new List<Manager>(managers);
        }
    }
}
