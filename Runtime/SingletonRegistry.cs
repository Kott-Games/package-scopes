using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kott.Scopes
{
    /// <summary>
    ///     Maps singleton types to instances. Loaded scopes register their <see cref="ISingleton" /> managers
    ///     under the concrete manager type and every <see cref="ISingletonContract{T}" /> it implements.
    /// </summary>
    /// <remarks>
    ///     <see cref="Singleton{T}" /> resolves through <see cref="Current" />, which is <see cref="Default" />
    ///     unless a test has installed an override (see <c>SingletonTestScope</c> in Kott.Scopes.Testing).
    /// </remarks>
    public sealed class SingletonRegistry
    {
        private static readonly List<SingletonRegistry> _overrides = new();

        private readonly Dictionary<Type, Entry> _entries = new();

        /// <summary>The registry the scope system registers managers in.</summary>
        public static SingletonRegistry Default { get; private set; } = new();

        /// <summary>The registry <see cref="Singleton{T}" /> resolves from: the innermost test override, else <see cref="Default" />.</summary>
        public static SingletonRegistry Current => _overrides.Count > 0 ? _overrides[^1] : Default;

        /// <summary>
        ///     Registers <paramref name="singleton" /> under its concrete type and all of its singleton contracts.
        ///     Either all types are registered or, if any is already taken, none.
        /// </summary>
        /// <param name="owner">The registering party (usually a <see cref="RuntimeScope" />); used to unregister and in messages.</param>
        /// <param name="singleton">The instance to register.</param>
        /// <exception cref="InvalidOperationException">One of the types is already registered.</exception>
        public void Register(object owner, ISingleton singleton)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (singleton == null)
            {
                throw new ArgumentNullException(nameof(singleton));
            }

            Type concreteType = singleton.GetType();
            List<Type> keys = new() { concreteType };
            keys.AddRange(GetContractTypes(concreteType));

            foreach (Type key in keys)
            {
                ThrowIfRegistered(key, singleton, owner);
            }

            foreach (Type key in keys)
            {
                _entries.Add(key, new Entry(singleton, owner));
            }
        }

        /// <summary>Registers <paramref name="singleton" /> under <paramref name="key" /> only.</summary>
        internal void RegisterAs(object owner, Type key, ISingleton singleton)
        {
            if (singleton == null)
            {
                throw new ArgumentNullException(nameof(singleton));
            }

            if (!key.IsInstanceOfType(singleton))
            {
                throw new ArgumentException($"{singleton.GetType().Name} is not assignable to {key.Name}.",
                    nameof(singleton));
            }

            ThrowIfRegistered(key, singleton, owner);
            _entries.Add(key, new Entry(singleton, owner));
        }

        /// <summary>Removes every registration made by <paramref name="owner" />.</summary>
        public void Unregister(object owner)
        {
            List<Type> keys = new();
            foreach (KeyValuePair<Type, Entry> pair in _entries)
            {
                if (ReferenceEquals(pair.Value.Owner, owner))
                {
                    keys.Add(pair.Key);
                }
            }

            foreach (Type key in keys)
            {
                _entries.Remove(key);
            }
        }

        /// <summary>Returns the instance registered as <typeparamref name="T" />.</summary>
        /// <exception cref="InvalidOperationException">Nothing is registered as <typeparamref name="T" />.</exception>
        public T Get<T>()
            where T : ISingleton
        {
            if (TryGet(out T instance))
            {
                return instance;
            }

            throw new InvalidOperationException(DescribeMissing(typeof(T)));
        }

        /// <summary>Gets the instance registered as <typeparamref name="T" /> if there is one.</summary>
        public bool TryGet<T>(out T instance)
            where T : ISingleton
        {
            if (_entries.TryGetValue(typeof(T), out Entry entry))
            {
                instance = (T)entry.Singleton;
                return true;
            }

            instance = default;
            return false;
        }

        /// <summary>Whether an instance is registered as <paramref name="type" />.</summary>
        public bool IsRegistered(Type type)
        {
            return type != null && _entries.ContainsKey(type);
        }

        /// <summary>The contract types <paramref name="type" /> provides through <see cref="ISingletonContract{T}" />.</summary>
        public static IReadOnlyList<Type> GetContractTypes(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            List<Type> contracts = new();
            foreach (Type interfaceType in type.GetInterfaces())
            {
                if (interfaceType.IsGenericType &&
                    interfaceType.GetGenericTypeDefinition() == typeof(ISingletonContract<>))
                {
                    contracts.Add(interfaceType.GetGenericArguments()[0]);
                }
            }

            return contracts;
        }

        internal static void PushOverride(SingletonRegistry registry)
        {
            _overrides.Add(registry ?? throw new ArgumentNullException(nameof(registry)));
        }

        internal static void PopOverride(SingletonRegistry registry)
        {
            if (_overrides.Count == 0 || _overrides[^1] != registry)
            {
                throw new InvalidOperationException(
                    "Singleton registry overrides must be removed in reverse order of creation (dispose nested SingletonTestScopes innermost first).");
            }

            _overrides.RemoveAt(_overrides.Count - 1);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // play mode without domain reload must not see registrations from the previous session
            Default = new SingletonRegistry();
            _overrides.Clear();
        }

        private void ThrowIfRegistered(Type key, ISingleton singleton, object owner)
        {
            if (_entries.TryGetValue(key, out Entry existing))
            {
                throw new InvalidOperationException(
                    $"Singleton '{key.FullName}' is provided twice: by {existing.Singleton.GetType().Name} ({existing.Owner}) " +
                    $"and by {singleton.GetType().Name} ({owner}). Each singleton type may only be provided once at a time.");
            }
        }

        private static string DescribeMissing(Type type)
        {
            if (type.IsInterface && !typeof(ISingletonContract<>).MakeGenericType(type).IsAssignableFrom(type))
            {
                return $"No singleton registered for '{type.FullName}'. Interfaces are only resolvable if they are a " +
                       $"singleton contract: declare it as '{type.Name} : ISingletonContract<{type.Name}>'.";
            }

            return $"No singleton registered for '{type.FullName}'. Is the scope whose manager provides it loaded?";
        }

        private readonly struct Entry
        {
            public readonly ISingleton Singleton;
            public readonly object Owner;

            public Entry(ISingleton singleton, object owner)
            {
                Singleton = singleton;
                Owner = owner;
            }
        }
    }
}
