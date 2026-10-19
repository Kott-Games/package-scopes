using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kott.Scopes.Testing
{
    /// <summary>
    ///     Fills the <see cref="InjectAttribute" /> and <see cref="InjectSceneAttribute" /> fields of an object with
    ///     fakes, so managers and components can be unit-tested without loading scopes.
    /// </summary>
    /// <remarks>
    ///     Same rules as a loaded scope: a required field without a registered contract (or scene object) throws,
    ///     an optional one stays <c>null</c>. Nothing is global, so tests need no cleanup.
    /// </remarks>
    /// <example>
    ///     <code>
    /// GameSession session = TestInjector.Create()
    ///     .Add&lt;IDice&gt;(new FakeDice(6))
    ///     .Inject(new GameSession());
    /// </code>
    /// </example>
    public sealed class TestInjector
    {
        private readonly Dictionary<Type, object> _contracts = new();
        private readonly Dictionary<string, GameObject> _sceneObjects = new();
        private ILogger _logger;

        private TestInjector()
        { }

        /// <summary>Creates an empty injector.</summary>
        public static TestInjector Create()
        {
            return new TestInjector();
        }

        /// <summary>Provides <paramref name="instance" /> for fields of contract type <typeparamref name="T" />.</summary>
        /// <exception cref="ArgumentException"><typeparamref name="T" /> is not a manager contract.</exception>
        /// <exception cref="InvalidOperationException"><typeparamref name="T" /> was already added.</exception>
        public TestInjector Add<T>(T instance)
            where T : class, IManagerContract
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (!Injector.IsContract(typeof(T)))
            {
                throw new ArgumentException(
                    $"'{typeof(T).Name}' is not a manager contract (an interface extending IManagerContract).");
            }

            if (!_contracts.TryAdd(typeof(T), instance))
            {
                throw new InvalidOperationException($"'{typeof(T).Name}' was already added.");
            }

            return this;
        }

        /// <summary>Provides <paramref name="sceneObject" /> for <see cref="InjectSceneAttribute" /> fields with <paramref name="key" />.</summary>
        /// <exception cref="InvalidOperationException"><paramref name="key" /> was already added.</exception>
        public TestInjector AddSceneObject(string key, GameObject sceneObject)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("The key must not be empty.", nameof(key));
            }

            if (sceneObject == null)
            {
                throw new ArgumentNullException(nameof(sceneObject));
            }

            if (!_sceneObjects.TryAdd(key, sceneObject))
            {
                throw new InvalidOperationException($"Scene object '{key}' was already added.");
            }

            return this;
        }

        /// <summary>
        ///     Sets <paramref name="logger" /> as <see cref="Manager.Logger" /> of injected managers. Without it they get
        ///     a <see cref="UnityLoggerFactory" /> logger with their type name as category.
        /// </summary>
        public TestInjector SetLogger(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            return this;
        }

        /// <summary>
        ///     Fills the injected fields of <paramref name="target" /> (and <see cref="Manager.Logger" /> of a manager)
        ///     and returns it.
        /// </summary>
        /// <exception cref="InvalidOperationException">A required contract or scene object is missing, or a field type is invalid.</exception>
        public T Inject<T>(T target)
            where T : class
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            Injector.Inject(target, type => _contracts.TryGetValue(type, out object instance) ? instance : null,
                "the TestInjector");
            Injector.InjectScene(target, key => _sceneObjects.TryGetValue(key, out GameObject sceneObject)
                ? sceneObject
                : null, "the TestInjector");

            if (target is Manager manager)
            {
                manager.Logger = _logger ?? new UnityLoggerFactory().CreateLogger(manager.GetType());
            }

            return target;
        }
    }
}
