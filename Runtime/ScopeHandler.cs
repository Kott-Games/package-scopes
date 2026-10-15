using System;
using System.Collections.Generic;
using Kott.Utilities.Progress;
using UnityEngine;

namespace Kott.Scopes
{
    /// <summary>
    ///     Entry point of the scope system: loads and unloads scopes and keeps the tree of loaded scopes.
    /// </summary>
    /// <remarks>
    ///     <see cref="Instance" /> creates a persistent host object on first use, so no setup is needed. A
    ///     hand-placed ScopeHandler is used instead if one exists; additional ones remove themselves.
    ///     Usually the first scope is loaded by a <see cref="Bootstrap" /> component.
    /// </remarks>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)]
    [AddComponentMenu("Scopes/Scope Handler")]
    public sealed class ScopeHandler : MonoBehaviour
    {
        private static ScopeHandler _instance;

        private ScopeSystem _system;

        /// <summary>The scope handler; created on first access.</summary>
        /// <exception cref="InvalidOperationException">Accessed outside play mode.</exception>
        public static ScopeHandler Instance
        {
            get
            {
                if (_instance == null)
                {
                    ObjectUtility.FindOrCreateHost<ScopeHandler>("Scopes").Initialize();
                }

                return _instance;
            }
        }

        /// <summary>Whether the handler exists, without creating it.</summary>
        public static bool HasInstance => _instance != null;

        /// <summary>The root of the scope tree, or <c>null</c> if nothing is loaded.</summary>
        public RuntimeScope RootScope => _system.RootScope;

        /// <summary>All scopes in the tree, including ones still loading or unloading.</summary>
        public IReadOnlyList<RuntimeScope> Scopes => _system.Scopes;

        /// <summary>Invoked when a scope finished loading successfully.</summary>
        public event Action<RuntimeScope> ScopeLoaded
        {
            add => _system.ScopeLoaded += value;
            remove => _system.ScopeLoaded -= value;
        }

        /// <summary>Invoked when a scope finished unloading (once per scope, children first).</summary>
        public event Action<RuntimeScope> ScopeUnloaded
        {
            add => _system.ScopeUnloaded += value;
            remove => _system.ScopeUnloaded -= value;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"A ScopeHandler already exists on '{_instance.name}'; removing the one on '{name}'.", this);
                Destroy(this);
                return;
            }

            Initialize();
        }

        private void OnApplicationQuit()
        {
            _system?.NotifyApplicationQuit();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        ///     Queues loading <paramref name="scope" />: registers its managers' singletons, initializes the
        ///     managers in order, then loads its scene (if any).
        /// </summary>
        /// <param name="scope">The scope to load. A scope can only be loaded once at a time.</param>
        /// <param name="parent">Scope to load under; <c>null</c> loads under the root scope, or as the root if none is loaded.</param>
        /// <param name="progress">Receives progress from 0 to 1. May be <c>null</c>.</param>
        /// <returns>The operation; on failure the partially loaded scope is cleaned up and the error is in <see cref="ScopeOperation.Exception" />.</returns>
        public ScopeOperation LoadScope(Scope scope, RuntimeScope parent = null, IProgress<ProgressReport> progress = null)
        {
            return _system.LoadScope(scope, parent, progress);
        }

        /// <summary>
        ///     Queues unloading <paramref name="runtimeScope" /> and its children (children first). Per scope:
        ///     <see cref="Manager.SceneExit" />, scene unload, <see cref="Manager.Uninitialize" /> in reverse order,
        ///     singletons removed, root object destroyed.
        /// </summary>
        /// <param name="runtimeScope">The loaded scope to unload.</param>
        /// <param name="progress">Receives progress from 0 to 1. May be <c>null</c>.</param>
        public ScopeOperation UnloadScope(RuntimeScope runtimeScope, IProgress<ProgressReport> progress = null)
        {
            return _system.UnloadScope(runtimeScope, progress);
        }

        /// <summary>Whether <paramref name="scope" /> is fully loaded.</summary>
        public bool IsLoaded(Scope scope)
        {
            return _system.IsLoaded(scope);
        }

        /// <summary>Gets the loaded instance of <paramref name="scope" /> if it is fully loaded.</summary>
        public bool TryGetRuntimeScope(Scope scope, out RuntimeScope runtimeScope)
        {
            return _system.TryGetRuntimeScope(scope, out runtimeScope);
        }

        private void Initialize()
        {
            if (_system != null)
            {
                return;
            }

            _instance = this;
            DontDestroyOnLoad(transform.root.gameObject);
            _system = new ScopeSystem(SingletonRegistry.Default, routine => StartCoroutine(routine), transform);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }
    }
}
