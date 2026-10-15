using System;
using System.Collections;
using System.Collections.Generic;
using Kott.Utilities.Progress;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Kott.Scopes
{
    /// <summary>
    ///     The scope tree and its load/unload queue. Plain C#, so the lifecycle can be tested without a host:
    ///     <see cref="ScopeHandler" /> owns the instance used at runtime.
    /// </summary>
    internal sealed class ScopeSystem
    {
        private readonly SingletonRegistry _registry;
        private readonly Action<IEnumerator> _startRoutine;
        private readonly ILogger _logger;
        private readonly Transform _rootParent;
        private readonly Queue<PendingOperation> _queue = new();
        private readonly List<RuntimeScope> _scopes = new();

        private bool _processing;
        private PendingOperation _current;

        /// <param name="registry">Registry the managers' singletons are registered in.</param>
        /// <param name="startRoutine">Runs the processing coroutine (e.g. <c>MonoBehaviour.StartCoroutine</c>).</param>
        /// <param name="rootParent">Parent transform for the root scope's root object; may be <c>null</c>.</param>
        /// <param name="logger">Receives failures and cleanup errors (<see cref="Debug.unityLogger" /> at runtime).</param>
        public ScopeSystem(SingletonRegistry registry, Action<IEnumerator> startRoutine, Transform rootParent,
            ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _startRoutine = startRoutine ?? throw new ArgumentNullException(nameof(startRoutine));
            _rootParent = rootParent;
        }

        /// <summary>The scope whose scene is activating right now; picked up by <see cref="SceneRoot" />.</summary>
        internal static RuntimeScope ActivatingScope { get; private set; }

        public RuntimeScope RootScope { get; private set; }

        /// <summary>All scopes in the tree, including ones still loading or unloading.</summary>
        public IReadOnlyList<RuntimeScope> Scopes => _scopes;

        public event Action<RuntimeScope> ScopeLoaded;
        public event Action<RuntimeScope> ScopeUnloaded;

        public ScopeOperation LoadScope(Scope scope, RuntimeScope parent, IProgress<ProgressReport> progress)
        {
            if (scope == null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            ScopeOperation operation = new(ScopeOperationType.Load, scope, runtimeScope: null);
            Enqueue(new PendingOperation(operation, parent, progress));
            return operation;
        }

        public ScopeOperation UnloadScope(RuntimeScope runtimeScope, IProgress<ProgressReport> progress)
        {
            if (runtimeScope == null)
            {
                throw new ArgumentNullException(nameof(runtimeScope));
            }

            ScopeOperation operation = new(ScopeOperationType.Unload, runtimeScope.Scope, runtimeScope);
            Enqueue(new PendingOperation(operation, parent: null, progress));
            return operation;
        }

        public bool TryGetRuntimeScope(Scope scope, out RuntimeScope runtimeScope)
        {
            foreach (RuntimeScope candidate in _scopes)
            {
                if (candidate.Scope == scope && candidate.State == ScopeState.Loaded)
                {
                    runtimeScope = candidate;
                    return true;
                }
            }

            runtimeScope = null;
            return false;
        }

        public bool IsLoaded(Scope scope)
        {
            return TryGetRuntimeScope(scope, out _);
        }

        /// <summary>Sends <see cref="Manager.SceneExit" /> to every scope with a loaded scene, children first.</summary>
        public void NotifyApplicationQuit()
        {
            if (RootScope == null)
            {
                return;
            }

            foreach (RuntimeScope runtimeScope in CollectDepthFirst(RootScope))
            {
                if (runtimeScope.SceneLoaded)
                {
                    runtimeScope.InvokeSceneExit();
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ActivatingScope = null;
        }

        private void Enqueue(PendingOperation pending)
        {
            _queue.Enqueue(pending);
            if (!_processing)
            {
                StartProcessing();
            }
        }

        private void StartProcessing()
        {
            _processing = true;
            _startRoutine(SafeRoutine.Run(ProcessQueue(), OnProcessingFailed));
        }

        private IEnumerator ProcessQueue()
        {
            while (_queue.Count > 0)
            {
                _current = _queue.Dequeue();
                yield return _current.Operation.Type == ScopeOperationType.Load ? Load(_current) : Unload(_current);
                _current = null;
            }

            _processing = false;
        }

        // only reached through a bug in this class: manager and loader code is isolated by SafeRoutine
        private void OnProcessingFailed(Exception exception)
        {
            _logger.LogException(exception);
            ActivatingScope = null;
            _current?.Operation.Complete(exception);
            _current = null;
            _processing = false;

            if (_queue.Count > 0)
            {
                StartProcessing();
            }
        }

        private IEnumerator Load(PendingOperation pending)
        {
            ScopeOperation operation = pending.Operation;
            Scope scope = operation.Scope;

            if (scope == null)
            {
                Fail(operation,
                    new InvalidOperationException("The Scope asset was destroyed before it could be loaded."));
                yield break;
            }

            foreach (RuntimeScope existing in _scopes)
            {
                if (existing.Scope == scope)
                {
                    Fail(operation,
                        new InvalidOperationException($"Scope '{scope.name}' is already loaded ({existing.State})."));
                    yield break;
                }
            }

            RuntimeScope parent = pending.Parent ?? RootScope;
            if (parent != null && parent.State != ScopeState.Loaded)
            {
                Fail(operation, new InvalidOperationException(
                    $"Cannot load scope '{scope.name}' under {parent}: the parent is {parent.State}."));
                yield break;
            }

            // managers are [SerializeReference] objects in the asset: work on a copy so runtime state stays out of it
            Scope instance = Object.Instantiate(scope);
            instance.name = scope.name;

            GameObject root = new($"Scope '{scope.name}'");
            Transform rootParent = parent != null ? parent.Root.transform : _rootParent;
            if (rootParent != null)
            {
                root.transform.SetParent(rootParent, worldPositionStays: false);
            }

            RuntimeScope runtimeScope = new(scope, instance, parent, root) { State = ScopeState.Loading };
            operation.RuntimeScope = runtimeScope;
            _scopes.Add(runtimeScope);
            RootScope ??= runtimeScope;
            parent?.ChildrenInternal.Insert(0, runtimeScope);

            IProgress<ProgressReport> progress = pending.Progress;
            progress?.Report(new ProgressReport(0f, $"Load scope '{scope.name}'"));

            IReadOnlyList<Manager> managers = runtimeScope.Managers;
            SceneReference sceneReference = instance.Scene;
            bool hasScene = sceneReference != null && sceneReference.IsAssigned;
            float managerShare = hasScene ? 0.5f : 1f;
            Exception error = null;
            int initializedCount = 0;

            for (int i = 0; i < managers.Count && error == null; i++)
            {
                if (managers[i] == null)
                {
                    error = new InvalidOperationException(
                        $"{runtimeScope} has a missing manager at index {i}: its type could not be resolved " +
                        "(renamed or deleted class?).");
                }
            }

            if (error == null)
            {
                try
                {
                    foreach (Manager manager in managers)
                    {
                        if (manager is ISingleton singleton)
                        {
                            _registry.Register(runtimeScope, singleton);
                        }
                    }
                }
                catch (Exception exception)
                {
                    error = exception;
                }
            }

            if (error == null && managers.Count > 0)
            {
                SubProgress managerProgress = new(progress) { Scale = managerShare / managers.Count };
                for (int i = 0; i < managers.Count; i++)
                {
                    Manager manager = managers[i];
                    manager.RuntimeScope = runtimeScope;

                    managerProgress.Start =
                        new ProgressReport(i * managerProgress.Scale, $"Initialize {manager.GetType().Name}");
                    managerProgress.Report(new ProgressReport(0f, null));

                    yield return SafeRoutine.Run(() => manager.Initialize(managerProgress),
                        exception => error = exception);
                    if (error != null)
                    {
                        break;
                    }

                    initializedCount++;
                    managerProgress.Report(new ProgressReport(1f,
                        null)); // in case the manager doesn't report completion
                }
            }

            if (error == null && hasScene)
            {
                SubProgress sceneProgress = new(progress)
                {
                    Start = new ProgressReport(managerShare, $"Load scene {sceneReference.DisplayName}"),
                    Scale = 1f - managerShare,
                };
                sceneProgress.Report(new ProgressReport(0f, null));

                try
                {
                    runtimeScope.SceneLoader = sceneReference.CreateLoader();
                }
                catch (Exception exception)
                {
                    error = exception;
                }

                if (error == null)
                {
                    ActivatingScope = runtimeScope;
                    yield return SafeRoutine.Run(() => runtimeScope.SceneLoader.Load(sceneProgress),
                        exception => error = exception);
                    ActivatingScope = null;

                    runtimeScope.SceneLoaded = true; // also on failure: a partially loaded scene must be unloaded
                    error ??= runtimeScope.SceneError;
                }
            }

            if (error != null)
            {
                yield return CleanUpFailedLoad(runtimeScope, initializedCount);
                Fail(operation, error);
                yield break;
            }

            runtimeScope.State = ScopeState.Loaded;
            progress?.Report(new ProgressReport(1f, $"Loaded scope '{scope.name}'"));
            Raise(ScopeLoaded, runtimeScope);
            operation.Complete(null);
        }

        private IEnumerator CleanUpFailedLoad(RuntimeScope runtimeScope, int initializedCount)
        {
            runtimeScope.State = ScopeState.Unloading;

            if (runtimeScope.SceneLoaded)
            {
                if (runtimeScope.SceneBound)
                {
                    runtimeScope.InvokeSceneExit();
                }

                yield return SafeRoutine.Run(() => runtimeScope.SceneLoader.Unload(null), _logger.LogException);
            }

            for (int i = initializedCount - 1; i >= 0; i--)
            {
                Manager manager = runtimeScope.Managers[i];
                yield return SafeRoutine.Run(() => manager.Uninitialize(null), _logger.LogException);
            }

            _registry.Unregister(runtimeScope);
            Detach(runtimeScope, ScopeState.Failed);
        }

        private IEnumerator Unload(PendingOperation pending)
        {
            ScopeOperation operation = pending.Operation;
            RuntimeScope target = operation.RuntimeScope;

            if (target.State != ScopeState.Loaded || !_scopes.Contains(target))
            {
                Fail(operation, new InvalidOperationException($"Cannot unload {target}: it is {target.State}."));
                yield break;
            }

            List<RuntimeScope> order = CollectDepthFirst(target);
            List<Exception> errors = new();

            for (int i = 0; i < order.Count; i++)
            {
                SubProgress scopeProgress = new(pending.Progress)
                {
                    Start = new ProgressReport((float)i / order.Count, null),
                    Scale = 1f / order.Count,
                };
                yield return UnloadSingle(order[i], scopeProgress, errors);
            }

            pending.Progress?.Report(new ProgressReport(1f, $"Unloaded scope '{target.Name}'"));

            Exception error = errors.Count switch
            {
                0 => null,
                1 => errors[0],
                _ => new AggregateException($"Unloading {target} threw {errors.Count} exceptions.", errors),
            };

            if (error != null)
            {
                _logger.LogException(error);
            }

            operation.Complete(error);
        }

        private IEnumerator UnloadSingle(RuntimeScope runtimeScope, IProgress<ProgressReport> progress,
            List<Exception> errors)
        {
            runtimeScope.State = ScopeState.Unloading;
            progress.Report(new ProgressReport(0f, $"Unload scope '{runtimeScope.Name}'"));

            float sceneShare = runtimeScope.SceneLoaded ? 0.5f : 0f;
            if (runtimeScope.SceneLoaded)
            {
                runtimeScope.InvokeSceneExit();

                SubProgress sceneProgress = new(progress)
                {
                    Start = new ProgressReport(0f, $"Unload scene of '{runtimeScope.Name}'"),
                    Scale = sceneShare,
                };
                yield return SafeRoutine.Run(() => runtimeScope.SceneLoader.Unload(sceneProgress), errors.Add);
                runtimeScope.SceneLoaded = false;
            }

            IReadOnlyList<Manager> managers = runtimeScope.Managers;
            if (managers.Count > 0)
            {
                SubProgress managerProgress = new(progress) { Scale = (1f - sceneShare) / managers.Count };
                for (int i = managers.Count - 1, step = 0; i >= 0; i--, step++)
                {
                    Manager manager = managers[i];
                    managerProgress.Start = new ProgressReport(
                        sceneShare + step * managerProgress.Scale, $"Uninitialize {manager.GetType().Name}");
                    managerProgress.Report(new ProgressReport(0f, null));

                    yield return SafeRoutine.Run(() => manager.Uninitialize(managerProgress), errors.Add);
                    managerProgress.Report(new ProgressReport(1f, null));
                }
            }

            _registry.Unregister(runtimeScope);
            Detach(runtimeScope, ScopeState.Unloaded);
            Raise(ScopeUnloaded, runtimeScope);
        }

        private void Detach(RuntimeScope runtimeScope, ScopeState finalState)
        {
            runtimeScope.Parent?.ChildrenInternal.Remove(runtimeScope);
            _scopes.Remove(runtimeScope);
            if (RootScope == runtimeScope)
            {
                RootScope = null;
            }

            runtimeScope.DestroyRoot();
            runtimeScope.State = finalState;
        }

        private void Fail(ScopeOperation operation, Exception exception)
        {
            _logger.LogError("Scopes",
                $"{operation.Type} of scope '{(operation.Scope != null ? operation.Scope.name : "?")}' " +
                $"failed: {exception}");
            operation.Complete(exception);
        }

        private void Raise(Action<RuntimeScope> handler, RuntimeScope runtimeScope)
        {
            try
            {
                handler?.Invoke(runtimeScope);
            }
            catch (Exception exception)
            {
                _logger.LogException(exception);
            }
        }

        /// <summary>The subtree of <paramref name="runtimeScope" />, children before their parent.</summary>
        private static List<RuntimeScope> CollectDepthFirst(RuntimeScope runtimeScope)
        {
            List<RuntimeScope> result = new();
            Collect(runtimeScope);
            return result;

            void Collect(RuntimeScope current)
            {
                foreach (RuntimeScope child in current.Children)
                {
                    Collect(child);
                }

                result.Add(current);
            }
        }

        private sealed class PendingOperation
        {
            public PendingOperation(ScopeOperation operation, RuntimeScope parent, IProgress<ProgressReport> progress)
            {
                Operation = operation;
                Parent = parent;
                Progress = progress;
            }

            public ScopeOperation Operation { get; }
            public RuntimeScope Parent { get; }
            public IProgress<ProgressReport> Progress { get; }
        }
    }
}
