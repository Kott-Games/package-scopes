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
        public Scope RootScope => _system.RootScope;

        /// <summary>All scopes in the tree, including ones still loading or unloading.</summary>
        public IReadOnlyList<Scope> Scopes => _system.Scopes;

        /// <summary>
        ///     The logger the scope system, all managers (<see cref="Manager.Logger" />) and
        ///     <see cref="CoroutineHandler" /> log through, tagged with their source (see <see cref="ScopeLogging" />).
        ///     Defaults to <see cref="Debug.unityLogger" />; replacing it takes effect immediately.
        /// </summary>
        public ILogger Logger
        {
            get => _system.Logger;
            set => _system.Logger = value;
        }

        /// <summary>Invoked when a scope finished loading successfully.</summary>
        public event Action<Scope> ScopeLoaded
        {
            add => _system.ScopeLoaded += value;
            remove => _system.ScopeLoaded -= value;
        }

        /// <summary>Invoked when a scope finished unloading (once per scope, children first).</summary>
        public event Action<Scope> ScopeUnloaded
        {
            add => _system.ScopeUnloaded += value;
            remove => _system.ScopeUnloaded -= value;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                _instance.Logger.Log(LogType.Warning, ScopeLogging.Tag,
                    $"A ScopeHandler already exists on '{_instance.name}'; removing the one on '{name}'.", this);
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
        ///     Queues loading <paramref name="definition" />: copies its managers, registers their contracts, injects
        ///     them, initializes them in dependency order, then loads the scene (if any) and injects its components.
        /// </summary>
        /// <param name="definition">The scope to load. A definition can only be loaded once at a time.</param>
        /// <param name="parent">Scope to load under; <c>null</c> loads under the root scope, or as the root if none is loaded.</param>
        /// <param name="progress">Receives progress from 0 to 1. May be <c>null</c>.</param>
        /// <returns>The operation; on failure the partially loaded scope is cleaned up and the error is in <see cref="ScopeOperation.Exception" />.</returns>
        public ScopeOperation LoadScope(ScopeDefinition definition, Scope parent = null,
            IProgress<ProgressReport> progress = null)
        {
            return _system.LoadScope(definition, parent, progress);
        }

        /// <summary>
        ///     Queues unloading <paramref name="scope" /> and its children (children first). Per scope:
        ///     <see cref="Manager.OnSceneExit" />, scene unload, <see cref="Manager.Uninitialize" /> in reverse
        ///     initialization order, contracts removed and injected fields cleared, root object destroyed.
        /// </summary>
        /// <param name="scope">The loaded scope to unload.</param>
        /// <param name="progress">Receives progress from 0 to 1. May be <c>null</c>.</param>
        public ScopeOperation UnloadScope(Scope scope, IProgress<ProgressReport> progress = null)
        {
            return _system.UnloadScope(scope, progress);
        }

        /// <summary>Whether <paramref name="definition" /> is fully loaded.</summary>
        public bool IsLoaded(ScopeDefinition definition)
        {
            return _system.IsLoaded(definition);
        }

        /// <summary>Gets the loaded scope of <paramref name="definition" /> if it is fully loaded.</summary>
        public bool TryGetScope(ScopeDefinition definition, out Scope scope)
        {
            return _system.TryGetScope(definition, out scope);
        }

        private void Initialize()
        {
            if (_system != null)
            {
                return;
            }

            _instance = this;
            DontDestroyOnLoad(transform.root.gameObject);
            _system = new ScopeSystem(routine => StartCoroutine(routine), transform, Debug.unityLogger);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }
    }
}
