using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Kott.Scopes
{
    /// <summary>
    ///     A loaded instance of a <see cref="ScopeDefinition" />, part of the tree of loaded scopes. Owns the copies
    ///     of the definition's managers and the contracts they provide.
    /// </summary>
    /// <remarks>
    ///     Contracts (see <see cref="IManagerContract" />) resolve along the parent chain: this scope, its parent, …,
    ///     the root. Code outside the scope tree resolves them through a scope it holds, e.g.
    ///     <c>ScopeHandler.Instance.RootScope.Resolve&lt;ISaveManager&gt;()</c>.
    /// </remarks>
    public sealed class Scope
    {
        private readonly List<Scope> _children = new();
        private readonly Dictionary<string, SceneLink> _sceneLinks = new();
        private readonly Dictionary<Type, Manager> _contracts = new();
        private readonly List<MonoBehaviour> _componentBuffer = new();
        private readonly ScopeRootBehaviour _driver;
        private readonly Func<Type, object> _resolve;
        private List<Manager> _managers;
        private bool _disposed;

        internal Scope(ScopeDefinition definition, ScopeDefinition instance, Scope parent, GameObject root,
            ILogger logger)
        {
            Definition = definition;
            Logger = logger;
            Instance = instance;
            Parent = parent;
            Root = root;
            _managers = new List<Manager>(instance.Managers);
            _resolve = type => TryFindProvider(type, out Manager provider, out _) ? provider : null;
            _driver = root.AddComponent<ScopeRootBehaviour>();
            _driver.Scope = this;
        }

        /// <summary>The definition asset this scope was loaded from.</summary>
        public ScopeDefinition Definition { get; }

        /// <summary>Name of the definition asset.</summary>
        public string Name => Definition != null ? Definition.name : "<destroyed>";

        /// <summary>The scope this one was loaded under, or <c>null</c> for the root scope.</summary>
        public Scope Parent { get; }

        /// <summary>Scopes loaded under this one, most recently loaded first.</summary>
        public IReadOnlyList<Scope> Children => _children;

        /// <summary>
        ///     The manager instances of this load, in initialization order (dependencies first, list order
        ///     otherwise).
        /// </summary>
        public IReadOnlyList<Manager> Managers => _managers;

        /// <summary>
        ///     GameObject that lives as long as this scope: parent for objects managers spawn (pools, UI) and
        ///     host for <see cref="StartCoroutine" />. Child scopes' roots are parented under it.
        /// </summary>
        public GameObject Root { get; }

        /// <summary>The scope's scene; invalid for scene-less scopes and before the scene has loaded.</summary>
        public Scene Scene => SceneLoader?.Scene ?? default;

        /// <summary>Current lifecycle state.</summary>
        public ScopeState State { get; internal set; }

        internal ScopeDefinition Instance { get; }

        /// <summary>The scope system's logger (category <see cref="ScopeLogging.Category" />).</summary>
        internal ILogger Logger { get; }

        internal List<Scope> ChildrenInternal => _children;
        internal ISceneLoader SceneLoader { get; set; }
        internal bool SceneLoaded { get; set; }
        internal bool SceneBound { get; private set; }

        /// <summary>An exception thrown while binding or injecting the scene; fails the load.</summary>
        internal Exception SceneError { get; set; }

        /// <summary>Returns the provider of contract <typeparamref name="T" /> in this scope or one of its parents.</summary>
        /// <exception cref="InvalidOperationException">No scope in the chain provides it, or this scope is unloaded.</exception>
        public T Resolve<T>()
            where T : class, IManagerContract
        {
            ThrowIfDisposed();
            if (TryResolve(out T manager))
            {
                return manager;
            }

            throw new InvalidOperationException(Injector.IsContract(typeof(T))
                ? $"'{typeof(T).Name}' is not provided by {this} or its parents."
                : $"'{typeof(T).Name}' is not a manager contract. Resolve an interface extending IManagerContract.");
        }

        /// <summary>
        ///     Gets the provider of contract <typeparamref name="T" /> in this scope or one of its parents. Always
        ///     <c>false</c> once the scope is unloaded.
        /// </summary>
        public bool TryResolve<T>(out T manager)
            where T : class, IManagerContract
        {
            manager = _disposed ? null : _resolve(typeof(T)) as T;
            return manager != null;
        }

        /// <summary>
        ///     Fills the <see cref="InjectAttribute" /> fields of <paramref name="target" /> from this scope's chain,
        ///     e.g. for objects created at runtime. The fields are not cleared when the scope unloads.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        ///     A required contract is missing, a field is not a contract, the target has
        ///     <see cref="InjectSceneAttribute" /> fields but is no manager, or this scope is unloaded.
        /// </exception>
        public void Inject(object target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            ThrowIfDisposed();
            InjectObject(target);
        }

        /// <summary>
        ///     Instantiates <paramref name="prefab" /> and injects all of its components before their <c>Awake</c>.
        /// </summary>
        /// <param name="prefab">The prefab or object to copy.</param>
        /// <param name="parent">Parent of the copy; <c>null</c> parents it under <see cref="Root" />, so it lives as long as the scope.</param>
        /// <exception cref="InvalidOperationException">Injection failed (the copy is destroyed again), or this scope is unloaded.</exception>
        public GameObject Instantiate(GameObject prefab, Transform parent = null)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            return InstantiateInjected(prefab, parent, instance => instance);
        }

        /// <summary>
        ///     Instantiates the GameObject of <paramref name="prefab" /> and injects all of its components before
        ///     their <c>Awake</c>.
        /// </summary>
        /// <inheritdoc cref="Instantiate(GameObject, Transform)" />
        /// <returns>The copy of <paramref name="prefab" />.</returns>
        public T Instantiate<T>(T prefab, Transform parent = null)
            where T : Component
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            return InstantiateInjected(prefab, parent, instance => instance.gameObject);
        }

        /// <summary>Starts a coroutine that stops when this scope unloads.</summary>
        /// <exception cref="InvalidOperationException">The scope is already unloaded.</exception>
        public Coroutine StartCoroutine(IEnumerator routine)
        {
            if (State is not (ScopeState.Loading or ScopeState.Loaded))
            {
                throw new InvalidOperationException($"Cannot start a coroutine on {this}: it is {State}.");
            }

            return _driver.StartCoroutine(routine);
        }

        /// <summary>Stops a coroutine started with <see cref="StartCoroutine" />.</summary>
        public void StopCoroutine(Coroutine coroutine)
        {
            if (_driver != null && coroutine != null)
            {
                _driver.StopCoroutine(coroutine);
            }
        }

        /// <summary>Returns the GameObject of the <see cref="SceneLink" /> with <paramref name="key" /> in this scope's scene.</summary>
        /// <exception cref="KeyNotFoundException">No link with that key exists.</exception>
        public GameObject GetSceneGameObject(string key)
        {
            if (TryGetSceneGameObject(key, out GameObject gameObject))
            {
                return gameObject;
            }

            throw new KeyNotFoundException(
                $"No SceneLink with key '{key}' in the scene of {this}" +
                (SceneBound ? "." : " (the scene is not loaded yet, or has no SceneRoot)."));
        }

        /// <summary>Gets the GameObject of the <see cref="SceneLink" /> with <paramref name="key" /> if it exists.</summary>
        public bool TryGetSceneGameObject(string key, out GameObject gameObject)
        {
            if (key != null && _sceneLinks.TryGetValue(key, out SceneLink link) && link != null)
            {
                gameObject = link.gameObject;
                return true;
            }

            gameObject = null;
            return false;
        }

        /// <summary>Returns component <typeparamref name="T" /> of the <see cref="SceneLink" /> with <paramref name="key" />.</summary>
        /// <exception cref="KeyNotFoundException">No link with that key exists.</exception>
        /// <exception cref="InvalidOperationException">The linked GameObject has no <typeparamref name="T" />.</exception>
        public T GetSceneComponent<T>(string key)
        {
            GameObject gameObject = GetSceneGameObject(key);
            if (gameObject.TryGetComponent(out T component))
            {
                return component;
            }

            throw new InvalidOperationException(
                $"SceneLink '{key}' in the scene of {this} ({gameObject.name}) has no {typeof(T).Name} component.");
        }

        /// <summary>Gets component <typeparamref name="T" /> of the <see cref="SceneLink" /> with <paramref name="key" /> if both exist.</summary>
        public bool TryGetSceneComponent<T>(string key, out T component)
        {
            if (TryGetSceneGameObject(key, out GameObject gameObject) && gameObject.TryGetComponent(out component))
            {
                return true;
            }

            component = default;
            return false;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"scope '{Name}'";
        }

        /// <summary>Finds the provider of <paramref name="contract" /> along the chain, and the scope providing it.</summary>
        internal bool TryFindProvider(Type contract, out Manager provider, out Scope owner)
        {
            for (Scope current = this; current != null; current = current.Parent)
            {
                if (!current._disposed && current._contracts.TryGetValue(contract, out provider))
                {
                    owner = current;
                    return true;
                }
            }

            provider = null;
            owner = null;
            return false;
        }

        /// <summary>Registers the contracts of all managers. Fails if one is provided twice in the chain.</summary>
        internal void RegisterContracts()
        {
            foreach (Manager manager in _managers)
            {
                foreach (Type contract in Injector.GetContracts(manager.GetType()))
                {
                    if (_contracts.TryGetValue(contract, out Manager existing))
                    {
                        throw new InvalidOperationException(
                            $"'{contract.Name}' is provided twice in {this}: by {existing.GetType().Name} and by " +
                            $"{manager.GetType().Name}. A contract can only have one provider per scope chain.");
                    }

                    if (Parent != null && Parent.TryFindProvider(contract, out Manager inherited, out Scope owner))
                    {
                        throw new InvalidOperationException(
                            $"'{contract.Name}' is provided by {manager.GetType().Name} in {this} and by " +
                            $"{inherited.GetType().Name} in its ancestor {owner}. A contract can only have one " +
                            "provider per scope chain; otherwise both scopes would resolve it to different managers.");
                    }

                    _contracts.Add(contract, manager);
                }
            }
        }

        /// <summary>Fills the <see cref="InjectAttribute" /> fields of all managers; fails listing every problem.</summary>
        /// <param name="hasScene">Whether the scope loads a scene; without one, required scene fields are an error.</param>
        internal void InjectManagers(bool hasScene)
        {
            List<string> problems = new();
            foreach (Manager manager in _managers)
            {
                try
                {
                    Injector.Inject(manager, _resolve, $"{this} or its parents");
                }
                catch (InvalidOperationException exception)
                {
                    problems.Add(exception.Message);
                }

                if (!hasScene)
                {
                    foreach (InjectedSceneField field in Injector.GetInfo(manager.GetType()).SceneFields)
                    {
                        if (!field.Optional)
                        {
                            problems.Add($"{manager.GetType().Name}.{field.Field.Name} needs SceneLink " +
                                         $"'{field.Key}', but {this} has no scene.");
                        }
                    }
                }
            }

            ThrowIfAny(problems);
        }

        /// <summary>Sorts <see cref="Managers" /> into initialization order. Fails on a dependency cycle.</summary>
        internal void OrderManagers()
        {
            List<Type> types = _managers.ConvertAll(manager => manager.GetType());
            if (!InitializationOrder.TryCompute(types, out List<int> order, out List<int> cycle))
            {
                throw new InvalidOperationException(
                    $"The managers of {this} depend on each other in a cycle: " +
                    $"{InitializationOrder.DescribeCycle(types, cycle)}. Break it, e.g. by moving shared state into " +
                    "a third manager.");
            }

            List<Manager> listOrder = _managers;
            _managers = order.ConvertAll(index => listOrder[index]);
        }

        /// <summary>
        ///     Binds <paramref name="scene" /> and calls <see cref="Manager.OnSceneAwake" />. A binding error is stored
        ///     in <see cref="SceneError" /> and fails the load once the scene finished loading. Called by its
        ///     <see cref="SceneRoot" />.
        /// </summary>
        internal void ActivateScene(Scene scene)
        {
            try
            {
                BindScene(scene);
            }
            catch (Exception exception)
            {
                SceneError = exception;
                return;
            }

            InvokeSceneAwake();
        }

        /// <summary>
        ///     Collects the scene's <see cref="SceneLink" />s, fills the managers' <see cref="InjectSceneAttribute" />
        ///     fields and injects the scene's components.
        /// </summary>
        internal void BindScene(Scene scene)
        {
            SceneBound = true;
            _sceneLinks.Clear();

            List<SceneLink> links = new();
            GameObject[] rootObjects = scene.GetRootGameObjects();
            foreach (GameObject sceneObject in rootObjects)
            {
                links.Clear();
                sceneObject.GetComponentsInChildren(true, links);
                foreach (SceneLink link in links)
                {
                    if (string.IsNullOrEmpty(link.Key))
                    {
                        Logger.Log(LogType.Warning,
                            (object)$"SceneLink on '{link.name}' in {this} has no key and is ignored.",
                            link);
                        continue;
                    }

                    if (_sceneLinks.TryGetValue(link.Key, out SceneLink existing))
                    {
                        throw new InvalidOperationException(
                            $"Duplicate SceneLink key '{link.Key}' in the scene of {this}: on '{existing.name}' and '{link.name}'.");
                    }

                    _sceneLinks.Add(link.Key, link);
                }
            }

            List<string> problems = new();
            Func<string, GameObject> findLink = key =>
                TryGetSceneGameObject(key, out GameObject linked) ? linked : null;
            foreach (Manager manager in _managers)
            {
                try
                {
                    Injector.InjectScene(manager, findLink, $"the scene of {this}");
                }
                catch (InvalidOperationException exception)
                {
                    problems.Add(exception.Message);
                }
            }

            foreach (GameObject sceneObject in rootObjects)
            {
                _componentBuffer.Clear();
                sceneObject.GetComponentsInChildren(true, _componentBuffer);
                foreach (MonoBehaviour component in _componentBuffer)
                {
                    try
                    {
                        // null: missing script
                        if (component != null)
                        {
                            InjectObject(component);
                        }
                    }
                    catch (InvalidOperationException exception)
                    {
                        problems.Add($"'{component.name}' in the scene of {this}: {exception.Message}");
                    }
                }
            }

            _componentBuffer.Clear();
            ThrowIfAny(problems);
        }

        /// <summary>Clears the managers' <see cref="InjectSceneAttribute" /> fields and the SceneLink lookup.</summary>
        internal void UnbindScene()
        {
            foreach (Manager manager in _managers)
            {
                if (manager != null)
                {
                    Injector.ClearScene(manager);
                }
            }

            _sceneLinks.Clear();
        }

        /// <summary>Removes all contracts and clears the managers' <see cref="InjectAttribute" /> fields.</summary>
        internal void DisposeContainer()
        {
            // null entries: a failed load with an unresolved manager type
            foreach (Manager manager in _managers)
            {
                if (manager != null)
                {
                    Injector.Clear(manager);
                }
            }

            _contracts.Clear();
            _disposed = true;
        }

        internal void DestroyRoot()
        {
            _sceneLinks.Clear();
            ObjectUtility.DestroySafe(Root);
            ObjectUtility.DestroySafe(Instance);
        }

        internal void InvokeSceneAwake()
        {
            Invoke(manager => manager.OnSceneAwake());
        }

        internal void InvokeSceneStart()
        {
            Invoke(manager => manager.OnSceneStart());
        }

        internal void InvokeUpdate()
        {
            Invoke(manager => manager.Update());
        }

        internal void InvokeFixedUpdate()
        {
            Invoke(manager => manager.FixedUpdate());
        }

        internal void InvokeSceneExit()
        {
            Invoke(manager => manager.OnSceneExit());
        }

        private void Invoke(Action<Manager> callback)
        {
            foreach (Manager manager in _managers)
            {
                try
                {
                    callback(manager);
                }
                catch (Exception exception)
                {
                    Logger.LogException(exception);
                }
            }
        }

        private void InjectObject(object target)
        {
            Injector.Inject(target, _resolve, $"{this} or its parents");
        }

        private T InstantiateInjected<T>(T prefab, Transform parent, Func<T, GameObject> getGameObject)
            where T : Object
        {
            ThrowIfDisposed();
            Transform target = parent != null ? parent : Root.transform;

            // under an inactive holder, Awake waits until the copy is injected and moved to its parent
            GameObject holder = new("Scope.Instantiate");
            holder.SetActive(false);
            holder.transform.SetParent(target, false);

            T instance = Object.Instantiate(prefab, holder.transform);
            GameObject instanceObject = getGameObject(instance);
            try
            {
                _componentBuffer.Clear();
                instanceObject.GetComponentsInChildren(true, _componentBuffer);
                foreach (MonoBehaviour component in _componentBuffer)
                {
                    if (component != null)
                    {
                        InjectObject(component);
                    }
                }
            }
            catch
            {
                ObjectUtility.DestroySafe(holder);
                throw;
            }
            finally
            {
                _componentBuffer.Clear();
            }

            instanceObject.transform.SetParent(target, false);
            ObjectUtility.DestroySafe(holder);
            return instance;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new InvalidOperationException($"{this} is {State}; its contracts are no longer available.");
            }
        }

        private static void ThrowIfAny(List<string> problems)
        {
            if (problems.Count > 0)
            {
                throw new InvalidOperationException(string.Join("\n", problems));
            }
        }
    }
}
