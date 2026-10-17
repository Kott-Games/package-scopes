using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Kott.Scopes
{
    /// <summary>
    ///     Reflection side of dependency injection: finds <see cref="InjectAttribute" /> and
    ///     <see cref="InjectSceneAttribute" /> fields (cached per type) and fills or clears them.
    /// </summary>
    internal static class Injector
    {
        private const BindingFlags FieldFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static readonly Dictionary<Type, InjectionInfo> _infos = new();
        private static readonly Dictionary<Type, Type[]> _contracts = new();

        /// <summary>Whether <paramref name="type" /> is a manager contract: an interface extending <see cref="IManagerContract" />.</summary>
        public static bool IsContract(Type type)
        {
            return type != null && type.IsInterface && type != typeof(IManagerContract) &&
                   typeof(IManagerContract).IsAssignableFrom(type);
        }

        /// <summary>The contracts <paramref name="type" /> implements.</summary>
        public static IReadOnlyList<Type> GetContracts(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (!_contracts.TryGetValue(type, out Type[] contracts))
            {
                contracts = Array.FindAll(type.GetInterfaces(), IsContract);
                Array.Sort(contracts, (a, b) => string.CompareOrdinal(a.FullName, b.FullName));
                _contracts.Add(type, contracts);
            }

            return contracts;
        }

        /// <summary>The injected fields of <paramref name="type" />, including inherited private ones.</summary>
        public static InjectionInfo GetInfo(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (_infos.TryGetValue(type, out InjectionInfo info))
            {
                return info;
            }

            List<InjectedField> fields = new();
            List<InjectedSceneField> sceneFields = new();
            for (Type current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                foreach (FieldInfo field in current.GetFields(FieldFlags))
                {
                    if (field.GetCustomAttribute<InjectAttribute>() is { } inject)
                    {
                        fields.Add(new InjectedField(field, inject.Optional));
                    }

                    if (field.GetCustomAttribute<InjectSceneAttribute>() is { } injectScene)
                    {
                        sceneFields.Add(new InjectedSceneField(field, injectScene.Key, injectScene.Optional));
                    }
                }
            }

            info = new InjectionInfo(fields, sceneFields);
            _infos.Add(type, info);
            return info;
        }

        /// <summary>Why <paramref name="field" /> can never be injected, or <c>null</c>.</summary>
        public static string GetFieldProblem(InjectedField field)
        {
            Type type = field.Field.FieldType;
            if (IsContract(type))
            {
                return null;
            }

            return type.IsInterface
                ? $"'{type.Name}' is not a manager contract. Declare it as '{type.Name} : IManagerContract'."
                : $"'{type.Name}' is not a manager contract. Inject an interface extending IManagerContract.";
        }

        /// <summary>Why <paramref name="field" /> can never be filled from a scene, or <c>null</c>.</summary>
        public static string GetFieldProblem(InjectedSceneField field)
        {
            if (string.IsNullOrEmpty(field.Key))
            {
                return "the [InjectScene] key is empty.";
            }

            Type type = field.Field.FieldType;
            return type == typeof(GameObject) || typeof(Component).IsAssignableFrom(type) || type.IsInterface
                ? null
                : $"'{type.Name}' cannot come from a scene object. Use GameObject, a component type or an interface.";
        }

        /// <summary>
        ///     Fills the <see cref="InjectAttribute" /> fields of <paramref name="target" />. Either all fields are
        ///     filled or, if one cannot be, none (and the exception lists every problem).
        /// </summary>
        /// <param name="target">The object to inject.</param>
        /// <param name="resolve">Returns the provider of a contract, or <c>null</c>.</param>
        /// <param name="source">Where contracts come from, for messages ("scope 'Game' or its parents").</param>
        /// <exception cref="InvalidOperationException">A field is not a contract, or a required contract is missing.</exception>
        public static void Inject(object target, Func<Type, object> resolve, string source)
        {
            InjectionInfo info = GetInfo(target.GetType());
            if (info.Fields.Count == 0)
            {
                return;
            }

            object[] values = new object[info.Fields.Count];
            List<string> problems = null;
            for (int i = 0; i < info.Fields.Count; i++)
            {
                InjectedField field = info.Fields[i];
                string problem = GetFieldProblem(field);
                if (problem == null)
                {
                    values[i] = resolve(field.Field.FieldType);
                    if (values[i] == null && !field.Optional)
                    {
                        problem = $"'{field.Field.FieldType.Name}' is not provided by {source}.";
                    }
                }

                if (problem != null)
                {
                    (problems ??= new List<string>()).Add($"{field.Field.Name}: {problem}");
                }
            }

            if (problems != null)
            {
                throw new InvalidOperationException(
                    $"Cannot inject {target.GetType().Name}: {string.Join(" ", problems)}");
            }

            for (int i = 0; i < info.Fields.Count; i++)
            {
                info.Fields[i].Field.SetValue(target, values[i]);
            }
        }

        /// <summary>Fills the <see cref="InjectSceneAttribute" /> fields of <paramref name="target" />, all or none.</summary>
        /// <param name="target">The manager to inject.</param>
        /// <param name="findLink">Returns the GameObject of the <see cref="SceneLink" /> with a key, or <c>null</c>.</param>
        /// <param name="source">The scene, for messages ("the scene of scope 'Board'").</param>
        /// <exception cref="InvalidOperationException">A required link or component is missing, or a field type is unsupported.</exception>
        public static void InjectScene(object target, Func<string, GameObject> findLink, string source)
        {
            InjectionInfo info = GetInfo(target.GetType());
            if (info.SceneFields.Count == 0)
            {
                return;
            }

            object[] values = new object[info.SceneFields.Count];
            List<string> problems = null;
            for (int i = 0; i < info.SceneFields.Count; i++)
            {
                InjectedSceneField field = info.SceneFields[i];
                Type type = field.Field.FieldType;
                string problem = GetFieldProblem(field);
                if (problem == null)
                {
                    GameObject linked = findLink(field.Key);
                    if (linked == null)
                    {
                        problem = field.Optional ? null : $"no SceneLink with key '{field.Key}' in {source}.";
                    }
                    else if (type == typeof(GameObject))
                    {
                        values[i] = linked;
                    }
                    else
                    {
                        Component component = linked.GetComponent(type);
                        if (component != null)
                        {
                            values[i] = component;
                        }
                        else if (!field.Optional)
                        {
                            problem = $"SceneLink '{field.Key}' ({linked.name}) has no {type.Name} component.";
                        }
                    }
                }

                if (problem != null)
                {
                    (problems ??= new List<string>()).Add($"{field.Field.Name}: {problem}");
                }
            }

            if (problems != null)
            {
                throw new InvalidOperationException(
                    $"Cannot inject scene objects into {target.GetType().Name}: {string.Join(" ", problems)}");
            }

            for (int i = 0; i < info.SceneFields.Count; i++)
            {
                info.SceneFields[i].Field.SetValue(target, values[i]);
            }
        }

        /// <summary>Sets the <see cref="InjectAttribute" /> fields of <paramref name="target" /> to <c>null</c>.</summary>
        public static void Clear(object target)
        {
            foreach (InjectedField field in GetInfo(target.GetType()).Fields)
            {
                field.Field.SetValue(target, null);
            }
        }

        /// <summary>Sets the <see cref="InjectSceneAttribute" /> fields of <paramref name="target" /> to <c>null</c>.</summary>
        public static void ClearScene(object target)
        {
            foreach (InjectedSceneField field in GetInfo(target.GetType()).SceneFields)
            {
                field.Field.SetValue(target, null);
            }
        }
    }

    /// <summary>The injected fields of one type.</summary>
    internal sealed class InjectionInfo
    {
        public InjectionInfo(IReadOnlyList<InjectedField> fields, IReadOnlyList<InjectedSceneField> sceneFields)
        {
            Fields = fields;
            SceneFields = sceneFields;
        }

        /// <summary>Fields marked <see cref="InjectAttribute" />.</summary>
        public IReadOnlyList<InjectedField> Fields { get; }

        /// <summary>Fields marked <see cref="InjectSceneAttribute" />.</summary>
        public IReadOnlyList<InjectedSceneField> SceneFields { get; }
    }

    internal readonly struct InjectedField
    {
        public readonly FieldInfo Field;
        public readonly bool Optional;

        public InjectedField(FieldInfo field, bool optional)
        {
            Field = field;
            Optional = optional;
        }
    }

    internal readonly struct InjectedSceneField
    {
        public readonly FieldInfo Field;
        public readonly string Key;
        public readonly bool Optional;

        public InjectedSceneField(FieldInfo field, string key, bool optional)
        {
            Field = field;
            Key = key;
            Optional = optional;
        }
    }
}
