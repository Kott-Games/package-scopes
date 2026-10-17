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
        private readonly Action<IEnumerator> _startRoutine;
        private readonly ILogger _logger;
        private readonly Transform _rootParent;
        private readonly Queue<PendingOperation> _queue = new();
        private readonly List<Scope> _scopes = new();

        private bool _processing;
        private PendingOperation _current;

        /// <param name="startRoutine">Runs the processing coroutine (e.g. <c>MonoBehaviour.StartCoroutine</c>).</param>
        /// <param name="rootParent">Parent transform for the root scope's root object; may be <c>null</c>.</param>
        /// <param name="logger">Receives failures and cleanup errors (<see cref="Debug.unityLogger" /> at runtime).</param>
        public ScopeSystem(Action<IEnumerator> startRoutine, Transform rootParent, ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _startRoutine = startRoutine ?? throw new ArgumentNullException(nameof(startRoutine));
            _rootParent = rootParent;
        }

        /// <summary>The scope whose scene is activating right now; picked up by <see cref="SceneRoot" />.</summary>
        internal static Scope ActivatingScope { get; private set; }

        public Scope RootScope { get; private set; }

        /// <summary>All scopes in the tree, including ones still loading or unloading.</summary>
        public IReadOnlyList<Scope> Scopes => _scopes;

        public event Action<Scope> ScopeLoaded;
        public event Action<Scope> ScopeUnloaded;

        public ScopeOperation LoadScope(ScopeDefinition definition, Scope parent, IProgress<ProgressReport> progress)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            ScopeOperation operation = new(ScopeOperationType.Load, definition, scope: null);
            Enqueue(new PendingOperation(operation, parent, progress));
            return operation;
        }

        public ScopeOperation UnloadScope(Scope scope, IProgress<ProgressReport> progress)
        {
            if (scope == null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            ScopeOperation operation = new(ScopeOperationType.Unload, scope.Definition, scope);
            Enqueue(new PendingOperation(operation, parent: null, progress));
            return operation;
        }

        public bool TryGetScope(ScopeDefinition definition, out Scope scope)
        {
            foreach (Scope candidate in _scopes)
            {
                if (candidate.Definition == definition && candidate.State == ScopeState.Loaded)
                {
                    scope = candidate;
                    return true;
                }
            }

            scope = null;
            return false;
        }

        public bool IsLoaded(ScopeDefinition definition)
        {
            return TryGetScope(definition, out _);
        }

        /// <summary>Sends <see cref="Manager.OnSceneExit" /> to every scope with a loaded scene, children first.</summary>
        public void NotifyApplicationQuit()
        {
            if (RootScope == null)
            {
                return;
            }

            foreach (Scope scope in CollectDepthFirst(RootScope))
            {
                if (scope.SceneLoaded)
                {
                    scope.InvokeSceneExit();
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
            ScopeDefinition definition = operation.Definition;

            if (definition == null)
            {
                Fail(operation,
                    new InvalidOperationException("The scope definition was destroyed before it could be loaded."));
                yield break;
            }

            foreach (Scope existing in _scopes)
            {
                if (existing.Definition == definition)
                {
                    Fail(operation, new InvalidOperationException(
                        $"Scope '{definition.name}' is already loaded ({existing.State})."));
                    yield break;
                }
            }

            Scope parent = pending.Parent ?? RootScope;
            if (parent != null && parent.State != ScopeState.Loaded)
            {
                Fail(operation, new InvalidOperationException(
                    $"Cannot load scope '{definition.name}' under {parent}: the parent is {parent.State}."));
                yield break;
            }

            if (definition.DeclaredParent != null && (parent == null || parent.Definition != definition.DeclaredParent))
            {
                _logger.LogWarning("Scopes",
                    $"Scope '{definition.name}' declares parent '{definition.DeclaredParent.name}' but is loaded " +
                    (parent != null ? $"under {parent}." : "as the root scope."));
            }

            // managers are [SerializeReference] objects in the asset: work on a copy so runtime state stays out of it
            ScopeDefinition instance = Object.Instantiate(definition);
            instance.name = definition.name;

            GameObject root = new($"Scope '{definition.name}'");
            Transform rootParent = parent != null ? parent.Root.transform : _rootParent;
            if (rootParent != null)
            {
                root.transform.SetParent(rootParent, worldPositionStays: false);
            }

            Scope scope = new(definition, instance, parent, root) { State = ScopeState.Loading };
            operation.Scope = scope;
            _scopes.Add(scope);
            RootScope ??= scope;
            parent?.ChildrenInternal.Insert(0, scope);

            IProgress<ProgressReport> progress = pending.Progress;
            progress?.Report(new ProgressReport(0f, $"Load scope '{definition.name}'"));

            SceneReference sceneReference = instance.Scene;
            bool hasScene = sceneReference != null && sceneReference.IsAssigned;
            float managerShare = hasScene ? 0.5f : 1f;
            Exception error = null;
            int initializedCount = 0;

            for (int i = 0; i < scope.Managers.Count && error == null; i++)
            {
                if (scope.Managers[i] == null)
                {
                    error = new InvalidOperationException(
                        $"{scope} has a missing manager at index {i}: its type could not be resolved " +
                        "(renamed or deleted class?).");
                }
            }

            if (error == null)
            {
                try
                {
                    foreach (Manager manager in scope.Managers)
                    {
                        manager.Scope = scope;
                    }

                    scope.RegisterContracts();
                    scope.InjectManagers(hasScene);
                    scope.OrderManagers();
                }
                catch (Exception exception)
                {
                    error = exception;
                }
            }

            IReadOnlyList<Manager> managers = scope.Managers;
            if (error == null && managers.Count > 0)
            {
                SubProgress managerProgress = new(progress) { Scale = managerShare / managers.Count };
                for (int i = 0; i < managers.Count; i++)
                {
                    Manager manager = managers[i];
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
                    // in case the manager doesn't report completion
                    managerProgress.Report(new ProgressReport(1f, null));
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
                    scope.SceneLoader = sceneReference.CreateLoader();
                }
                catch (Exception exception)
                {
                    error = exception;
                }

                if (error == null)
                {
                    ActivatingScope = scope;
                    yield return SafeRoutine.Run(() => scope.SceneLoader.Load(sceneProgress),
                        exception => error = exception);
                    ActivatingScope = null;

                    scope.SceneLoaded = true; // also on failure: a partially loaded scene must be unloaded
                    error ??= scope.SceneError;
                }
            }

            if (error != null)
            {
                yield return CleanUpFailedLoad(scope, initializedCount);
                Fail(operation, error);
                yield break;
            }

            scope.State = ScopeState.Loaded;
            progress?.Report(new ProgressReport(1f, $"Loaded scope '{definition.name}'"));
            Raise(ScopeLoaded, scope);
            operation.Complete(null);
        }

        private IEnumerator CleanUpFailedLoad(Scope scope, int initializedCount)
        {
            scope.State = ScopeState.Unloading;

            if (scope.SceneLoaded)
            {
                if (scope.SceneBound)
                {
                    scope.InvokeSceneExit();
                }

                scope.UnbindScene();
                yield return SafeRoutine.Run(() => scope.SceneLoader.Unload(null), _logger.LogException);
            }

            for (int i = initializedCount - 1; i >= 0; i--)
            {
                Manager manager = scope.Managers[i];
                yield return SafeRoutine.Run(() => manager.Uninitialize(null), _logger.LogException);
            }

            DisposeContainer(scope);
            Detach(scope, ScopeState.Failed);
        }

        private IEnumerator Unload(PendingOperation pending)
        {
            ScopeOperation operation = pending.Operation;
            Scope target = operation.Scope;

            if (target.State != ScopeState.Loaded || !_scopes.Contains(target))
            {
                Fail(operation, new InvalidOperationException($"Cannot unload {target}: it is {target.State}."));
                yield break;
            }

            List<Scope> order = CollectDepthFirst(target);
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

        private IEnumerator UnloadSingle(Scope scope, IProgress<ProgressReport> progress, List<Exception> errors)
        {
            scope.State = ScopeState.Unloading;
            progress.Report(new ProgressReport(0f, $"Unload scope '{scope.Name}'"));

            float sceneShare = scope.SceneLoaded ? 0.5f : 0f;
            if (scope.SceneLoaded)
            {
                scope.InvokeSceneExit();
                scope.UnbindScene();

                SubProgress sceneProgress = new(progress)
                {
                    Start = new ProgressReport(0f, $"Unload scene of '{scope.Name}'"),
                    Scale = sceneShare,
                };
                yield return SafeRoutine.Run(() => scope.SceneLoader.Unload(sceneProgress), errors.Add);
                scope.SceneLoaded = false;
            }

            IReadOnlyList<Manager> managers = scope.Managers;
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

            DisposeContainer(scope);
            Detach(scope, ScopeState.Unloaded);
            Raise(ScopeUnloaded, scope);
        }

        private void DisposeContainer(Scope scope)
        {
            try
            {
                scope.DisposeContainer();
            }
            catch (Exception exception)
            {
                _logger.LogException(exception);
            }
        }

        private void Detach(Scope scope, ScopeState finalState)
        {
            scope.Parent?.ChildrenInternal.Remove(scope);
            _scopes.Remove(scope);
            if (RootScope == scope)
            {
                RootScope = null;
            }

            scope.DestroyRoot();
            scope.State = finalState;
        }

        private void Fail(ScopeOperation operation, Exception exception)
        {
            _logger.LogError("Scopes",
                $"{operation.Type} of scope '{(operation.Definition != null ? operation.Definition.name : "?")}' " +
                $"failed: {exception}");
            operation.Complete(exception);
        }

        private void Raise(Action<Scope> handler, Scope scope)
        {
            try
            {
                handler?.Invoke(scope);
            }
            catch (Exception exception)
            {
                _logger.LogException(exception);
            }
        }

        /// <summary>The subtree of <paramref name="scope" />, children before their parent.</summary>
        private static List<Scope> CollectDepthFirst(Scope scope)
        {
            List<Scope> result = new();
            Collect(scope);
            return result;

            void Collect(Scope current)
            {
                foreach (Scope child in current.Children)
                {
                    Collect(child);
                }

                result.Add(current);
            }
        }

        private sealed class PendingOperation
        {
            public PendingOperation(ScopeOperation operation, Scope parent, IProgress<ProgressReport> progress)
            {
                Operation = operation;
                Parent = parent;
                Progress = progress;
            }

            public ScopeOperation Operation { get; }
            public Scope Parent { get; }
            public IProgress<ProgressReport> Progress { get; }
        }
    }
}
