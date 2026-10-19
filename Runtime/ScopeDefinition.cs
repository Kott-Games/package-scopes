using System.Collections.Generic;
using UnityEngine;

namespace Kott.Scopes
{
    /// <summary>
    ///     Authoring asset for a unit of game state: a list of <see cref="Manager" />s and an optional scene.
    ///     Load it with <see cref="ScopeHandler.LoadScope" />; each load creates a <see cref="Scope" />.
    /// </summary>
    /// <remarks>
    ///     Validate definitions in the editor with <c>ScopeValidator</c> (Kott.Scopes.Editor); its results are also
    ///     shown in the inspector and under <c>Tools &gt; Scopes &gt; Validate All</c>.
    /// </remarks>
    [CreateAssetMenu(fileName = "NewScope", menuName = "Scopes/Scope Definition", order = 0)]
    public sealed class ScopeDefinition : ScriptableObject
    {
        internal const string SceneField = nameof(_scene);
        internal const string DeclaredParentField = nameof(_declaredParent);
        internal const string ManagersField = nameof(_managers);

        [SerializeReference]
        private SceneReference _scene;

        [SerializeField]
        [Tooltip("The scope this one is expected to be loaded under (optional). Validation resolves injected " +
                 "contracts through it; loading under a different parent logs a warning.")]
        private ScopeDefinition _declaredParent;

        [SerializeReference]
        private List<Manager> _managers = new();

        /// <summary>The scene loaded with this scope, or <c>null</c> for a scene-less scope.</summary>
        public SceneReference Scene => _scene;

        /// <summary>
        ///     The definition this one is expected to be loaded under, or <c>null</c>. Validation resolves the
        ///     contracts this scope injects through its declared parents; at runtime any parent is accepted, with a
        ///     warning if it differs.
        /// </summary>
        public ScopeDefinition DeclaredParent => _declaredParent;

        /// <summary>
        ///     The managers in list order. These are the authored instances; a loaded scope works on copies (see
        ///     <see cref="Scope.Managers" />), initialized in dependency order. Entries are <c>null</c> if their type
        ///     no longer exists.
        /// </summary>
        public IReadOnlyList<Manager> Managers => _managers;

        internal void SetScene(SceneReference scene)
        {
            _scene = scene;
        }

        internal void SetDeclaredParent(ScopeDefinition declaredParent)
        {
            _declaredParent = declaredParent;
        }

        internal void SetManagers(IEnumerable<Manager> managers)
        {
            _managers = new List<Manager>(managers);
        }
    }
}
