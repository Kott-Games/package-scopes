using System;
using System.Collections;
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
                        sceneFields.Add(new InjectedSceneField(field, injectScene.Key, injectScene.Optional,
                            GetCollectionElementType(field.FieldType)));
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

            if (field.IsCollection)
            {
                return IsSceneObjectType(field.ElementType)
                    ? null
                    : $"'{field.ElementType.Name}' cannot come from a scene object. Use an array or List of " +
                      "GameObject, a component type or an interface.";
            }

            Type type = field.Field.FieldType;
            return IsSceneObjectType(type)
                ? null
                : $"'{type.Name}' cannot come from a scene object. Use GameObject, a component type or an interface.";
        }

        /// <summary>The element type of a collection field type (<c>T[]</c> or <c>List&lt;T&gt;</c>), or <c>null</c>.</summary>
        internal static Type GetCollectionElementType(Type type)
        {
            if (type.IsArray)
            {
                return type.GetArrayRank() == 1 ? type.GetElementType() : null;
            }

            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)
                ? type.GetGenericArguments()[0]
                : null;
        }

        private static bool IsSceneObjectType(Type type)
        {
            return type == typeof(GameObject) || typeof(Component).IsAssignableFrom(type) || type.IsInterface;
        }

        /// <summary>
        ///     Fills the <see cref="InjectAttribute" /> fields of <paramref name="target" />. Either all fields are
        ///     filled or, if one cannot be, none (and the exception lists every problem).
        /// </summary>
        /// <param name="target">The object to inject.</param>
        /// <param name="resolve">Returns the provider of a contract, or <c>null</c>.</param>
        /// <param name="source">Where contracts come from, for messages ("scope 'Game' or its parents").</param>
        /// <exception cref="InvalidOperationException">
        ///     A field is not a contract, a required contract is missing, or <paramref name="target" /> is not a manager
        ///     but has <see cref="InjectSceneAttribute" /> fields.
        /// </exception>
        public static void Inject(object target, Func<Type, object> resolve, string source)
        {
            InjectionInfo info = GetInfo(target.GetType());
            if (target is not Manager && info.SceneFields.Count > 0)
            {
                throw new InvalidOperationException(
                    $"{target.GetType().Name} has [InjectScene] fields, which are only supported on managers. " +
                    "Reference scene objects with serialized fields instead.");
            }

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
        /// <param name="findLinks">
        ///     Returns the GameObjects of all <see cref="SceneLink" />s with a key, in scene order; empty or
        ///     <c>null</c> for none.
        /// </param>
        /// <param name="source">The scene, for messages ("the scene of scope 'Board'").</param>
        /// <exception cref="InvalidOperationException">
        ///     A required link or component is missing, several links share the key of a single-object field, or a
        ///     field type is unsupported.
        /// </exception>
        public static void InjectScene(object target, Func<string, IReadOnlyList<GameObject>> findLinks,
            string source)
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
                string problem = GetFieldProblem(field);
                if (problem == null)
                {
                    IReadOnlyList<GameObject> linked = findLinks(field.Key) ?? Array.Empty<GameObject>();
                    problem = field.IsCollection
                        ? ResolveCollection(field, linked, source, out values[i])
                        : ResolveSingle(field, linked, source, out values[i]);
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

        /// <summary>
        ///     Like <see cref="InjectScene(object, Func{string, IReadOnlyList{GameObject}}, string)" />, with at most
        ///     one link per key.
        /// </summary>
        public static void InjectScene(object target, Func<string, GameObject> findLink, string source)
        {
            InjectScene(target, key => findLink(key) is { } linked ? new[] { linked } : null, source);
        }

        private static string ResolveSingle(InjectedSceneField field, IReadOnlyList<GameObject> linked, string source,
            out object value)
        {
            value = null;
            if (linked.Count == 0)
            {
                return field.Optional ? null : $"no SceneLink with key '{field.Key}' in {source}.";
            }

            if (linked.Count > 1)
            {
                return $"{linked.Count} SceneLinks share the key '{field.Key}' in {source}, but the field takes one. " +
                       "Use an array or List field to get all of them, or make the keys unique.";
            }

            Type type = field.Field.FieldType;
            if (type == typeof(GameObject))
            {
                value = linked[0];
                return null;
            }

            Component component = linked[0].GetComponent(type);
            if (component != null)
            {
                value = component;
                return null;
            }

            return field.Optional ? null : $"SceneLink '{field.Key}' ({linked[0].name}) has no {type.Name} component.";
        }

        private static string ResolveCollection(InjectedSceneField field, IReadOnlyList<GameObject> linked,
            string source, out object value)
        {
            value = null;
            if (linked.Count == 0 && !field.Optional)
            {
                return $"no SceneLink with key '{field.Key}' in {source}.";
            }

            Type element = field.ElementType;
            List<object> items = new(linked.Count);
            foreach (GameObject gameObject in linked)
            {
                if (element == typeof(GameObject))
                {
                    items.Add(gameObject);
                    continue;
                }

                Component component = gameObject.GetComponent(element);
                if (component != null)
                {
                    items.Add(component);
                }
                else if (!field.Optional)
                {
                    return $"SceneLink '{field.Key}' ({gameObject.name}) has no {element.Name} component.";
                }
            }

            value = CreateCollection(field.Field.FieldType, element, items);
            return null;
        }

        private static object CreateCollection(Type collectionType, Type element, List<object> items)
        {
            if (collectionType.IsArray)
            {
                Array array = Array.CreateInstance(element, items.Count);
                for (int i = 0; i < items.Count; i++)
                {
                    array.SetValue(items[i], i);
                }

                return array;
            }

            IList list = (IList)Activator.CreateInstance(collectionType, items.Count);
            foreach (object item in items)
            {
                list.Add(item);
            }

            return list;
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

        /// <summary>The element type of a <c>T[]</c> or <c>List&lt;T&gt;</c> field; <c>null</c> for a single-object field.</summary>
        public readonly Type ElementType;

        public InjectedSceneField(FieldInfo field, string key, bool optional, Type elementType = null)
        {
            Field = field;
            Key = key;
            Optional = optional;
            ElementType = elementType;
        }

        /// <summary>Whether the field takes the objects of all links with its key.</summary>
        public bool IsCollection => ElementType != null;
    }
}
