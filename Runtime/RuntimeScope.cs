using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kott.Scopes
{
    /// <summary>A loaded instance of a <see cref="Scope" />, part of the tree of loaded scopes.</summary>
    public sealed class RuntimeScope
    {
        private readonly List<RuntimeScope> _children = new();
        private readonly Dictionary<string, SceneLink> _sceneLinks = new();
        private readonly ScopeRootBehaviour _driver;

        internal RuntimeScope(Scope scope, Scope instance, RuntimeScope parent, GameObject root)
        {
            Scope = scope;
            Instance = instance;
            Parent = parent;
            Root = root;
            _driver = root.AddComponent<ScopeRootBehaviour>();
            _driver.RuntimeScope = this;
        }

        /// <summary>The Scope asset this instance was loaded from.</summary>
        public Scope Scope { get; }

        /// <summary>Name of the Scope asset.</summary>
        public string Name => Scope != null ? Scope.name : "<destroyed>";

        /// <summary>The scope this one was loaded under, or <c>null</c> for the root scope.</summary>
        public RuntimeScope Parent { get; }

        /// <summary>Scopes loaded under this one, most recently loaded first.</summary>
        public IReadOnlyList<RuntimeScope> Children => _children;

        /// <summary>The manager instances of this load, in initialization order.</summary>
        public IReadOnlyList<Manager> Managers => Instance.Managers;

        /// <summary>
        ///     GameObject that lives as long as this scope: parent for objects managers spawn (pools, UI) and
        ///     host for <see cref="StartCoroutine" />. Child scopes' roots are parented under it.
        /// </summary>
        public GameObject Root { get; }

        /// <summary>The scope's scene; invalid for scene-less scopes and before the scene has loaded.</summary>
        public Scene Scene => SceneLoader?.Scene ?? default;

        /// <summary>Current lifecycle state.</summary>
        public ScopeState State { get; internal set; }

        internal Scope Instance { get; }
        internal List<RuntimeScope> ChildrenInternal => _children;
        internal ISceneLoader SceneLoader { get; set; }
        internal bool SceneLoaded { get; set; }
        internal bool SceneBound { get; private set; }

        /// <summary>An exception thrown while binding the scene (e.g. duplicate <see cref="SceneLink" /> keys).</summary>
        internal Exception SceneError { get; set; }

        /// <summary>Starts a coroutine that stops when this scope unloads.</summary>
        /// <exception cref="InvalidOperationException">The scope is already unloaded.</exception>
        public Coroutine StartCoroutine(IEnumerator routine)
        {
            if (_driver == null)
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

        /// <summary>Collects the scene's <see cref="SceneLink" />s; called by its <see cref="SceneRoot" />.</summary>
        internal void BindScene(Scene scene)
        {
            SceneBound = true;
            _sceneLinks.Clear();

            List<SceneLink> links = new();
            foreach (GameObject sceneObject in scene.GetRootGameObjects())
            {
                links.Clear();
                sceneObject.GetComponentsInChildren(true, links);
                foreach (SceneLink link in links)
                {
                    if (string.IsNullOrEmpty(link.Key))
                    {
                        Debug.LogWarning($"SceneLink on '{link.name}' in {this} has no key and is ignored.", link);
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
        }

        internal void DestroyRoot()
        {
            _sceneLinks.Clear();
            ObjectUtility.DestroySafe(Root);
            ObjectUtility.DestroySafe(Instance);
        }

        internal void InvokeSceneAwake()
        {
            foreach (Manager manager in Managers)
            {
                try
                {
                    manager.SceneAwake();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        internal void InvokeSceneStart()
        {
            foreach (Manager manager in Managers)
            {
                try
                {
                    manager.SceneStart();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        internal void InvokeSceneUpdate()
        {
            foreach (Manager manager in Managers)
            {
                try
                {
                    manager.SceneUpdate();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        internal void InvokeSceneFixedUpdate()
        {
            foreach (Manager manager in Managers)
            {
                try
                {
                    manager.SceneFixedUpdate();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        internal void InvokeSceneExit()
        {
            foreach (Manager manager in Managers)
            {
                try
                {
                    manager.SceneExit();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
    }
}
