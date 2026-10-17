using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Kott.Scopes
{
    /// <summary>
    ///     Checks Scope assets for authoring mistakes: unresolved or empty managers, singletons that are required
    ///     but not provided (or provided twice) along the declared parents, declared-parent cycles, scenes that are
    ///     missing, unregistered with their loader, without a <see cref="SceneRoot" />, or with duplicate
    ///     <see cref="SceneLink" /> keys.
    /// </summary>
    /// <remarks>
    ///     Used by the Scope inspector and <c>Tools &gt; Scopes &gt; Validate All</c>; call it from tests or CI to
    ///     fail a build on errors:
    ///     <code>
    /// foreach (ScopeValidationResult result in ScopeValidator.ValidateAll())
    ///     Assert.That(result.HasErrors, Is.False, result.ToString());
    /// </code>
    /// </remarks>
    public static class ScopeValidator
    {
        private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                                BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        /// <summary>Validates one scope.</summary>
        /// <param name="scope">The scope to validate.</param>
        /// <param name="inspectScene">
        ///     Also check the content of the scope's scene. Opens the scene additively (and closes it again) if it
        ///     is not loaded in the editor. Ignored in play mode.
        /// </param>
        public static ScopeValidationResult Validate(Scope scope, bool inspectScene = true)
        {
            if (scope == null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            List<ScopeIssue> issues = new();
            List<SingletonRequirement> requirements = new();

            ValidateManagers(scope, issues);
            List<Scope> ancestors = CollectDeclaredAncestors(scope, issues);
            ValidateSingletons(scope, ancestors, issues, requirements);
            bool sceneInspected = ValidateScene(scope, inspectScene, issues);

            return new ScopeValidationResult(scope, issues, requirements, sceneInspected);
        }

        /// <summary>Validates every Scope asset in the project (including packages).</summary>
        /// <param name="inspectScenes">See <see cref="Validate" />.</param>
        public static IReadOnlyList<ScopeValidationResult> ValidateAll(bool inspectScenes = true)
        {
            List<ScopeValidationResult> results = new();
            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(Scope)}"))
            {
                Scope scope = AssetDatabase.LoadAssetAtPath<Scope>(AssetDatabase.GUIDToAssetPath(guid));
                if (scope != null)
                {
                    results.Add(Validate(scope, inspectScenes));
                }
            }

            return results;
        }

        /// <summary>
        ///     The singleton types a manager of <paramref name="managerType" /> is registered as: the concrete type
        ///     (if it implements <see cref="ISingleton" />) and every <see cref="ISingletonContract{T}" />.
        /// </summary>
        public static IReadOnlyList<Type> GetProvidedSingletons(Type managerType)
        {
            if (managerType == null)
            {
                throw new ArgumentNullException(nameof(managerType));
            }

            List<Type> provided = new();
            if (typeof(ISingleton).IsAssignableFrom(managerType))
            {
                provided.Add(managerType);
                provided.AddRange(SingletonRegistry.GetContractTypes(managerType));
            }

            return provided;
        }

        /// <summary>
        ///     The singleton types a manager of <paramref name="managerType" /> requires: the type arguments of its
        ///     <see cref="Singleton{T}" /> fields (including inherited and private ones) and of its
        ///     <see cref="RequiresSingletonAttribute" />s.
        /// </summary>
        public static IReadOnlyList<Type> GetRequiredSingletons(Type managerType)
        {
            if (managerType == null)
            {
                throw new ArgumentNullException(nameof(managerType));
            }

            List<Type> required = new();
            for (Type type = managerType; type != null && type != typeof(object); type = type.BaseType)
            {
                foreach (FieldInfo field in type.GetFields(FieldFlags))
                {
                    Type fieldType = field.FieldType;
                    if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(Singleton<>))
                    {
                        AddUnique(required, fieldType.GetGenericArguments()[0]);
                    }
                }
            }

            foreach (RequiresSingletonAttribute attribute in managerType.GetCustomAttributes(
                         typeof(RequiresSingletonAttribute), true))
            {
                if (attribute.Type != null)
                {
                    AddUnique(required, attribute.Type);
                }
            }

            return required;
        }

        /// <summary>Checks the content of a scope's scene, given its root objects.</summary>
        internal static List<ScopeIssue> ValidateSceneContent(Scope scope, string sceneName,
            IEnumerable<GameObject> roots, Object context = null)
        {
            List<ScopeIssue> issues = new();
            List<SceneRoot> sceneRoots = new();
            List<SceneLink> links = new();
            List<SceneRoot> rootBuffer = new();
            List<SceneLink> linkBuffer = new();

            foreach (GameObject root in roots)
            {
                root.GetComponentsInChildren(true, rootBuffer);
                sceneRoots.AddRange(rootBuffer);
                root.GetComponentsInChildren(true, linkBuffer);
                links.AddRange(linkBuffer);
            }

            if (sceneRoots.Count == 0)
            {
                issues.Add(new ScopeIssue(scope, ScopeIssueKind.SceneWithoutSceneRoot, ScopeIssueSeverity.Error,
                    $"The scene '{sceneName}' has no SceneRoot, so managers get no scene callbacks and SceneLinks are " +
                    "not bound. Add one (Add Component > Scopes > Scene Root), or create the scene from the Scope " +
                    "Scene template.", context));
            }
            else if (sceneRoots.Count > 1)
            {
                issues.Add(new ScopeIssue(scope, ScopeIssueKind.MultipleSceneRoots, ScopeIssueSeverity.Error,
                    $"The scene '{sceneName}' has {sceneRoots.Count} SceneRoots " +
                    $"({string.Join(", ", sceneRoots.ConvertAll(sceneRoot => $"'{sceneRoot.name}'"))}); only one " +
                    "is used. Remove the others.", context));
            }

            Dictionary<string, SceneLink> linksByKey = new();
            foreach (SceneLink link in links)
            {
                if (string.IsNullOrEmpty(link.Key))
                {
                    issues.Add(new ScopeIssue(scope, ScopeIssueKind.EmptySceneLinkKey, ScopeIssueSeverity.Warning,
                        $"The SceneLink on '{link.name}' in scene '{sceneName}' has no key and is ignored.", context));
                }
                else if (linksByKey.TryGetValue(link.Key, out SceneLink existing))
                {
                    issues.Add(new ScopeIssue(scope, ScopeIssueKind.DuplicateSceneLinkKey, ScopeIssueSeverity.Error,
                        $"Duplicate SceneLink key '{link.Key}' in scene '{sceneName}': on '{existing.name}' and " +
                        $"'{link.name}'. Keys must be unique within a scene.", context));
                }
                else
                {
                    linksByKey.Add(link.Key, link);
                }
            }

            return issues;
        }

        internal static string FormatMissingType(ManagedReferenceMissingType missingType)
        {
            string ns = string.IsNullOrEmpty(missingType.namespaceName)
                ? string.Empty
                : missingType.namespaceName + ".";
            return $"{ns}{missingType.className}, {missingType.assemblyName}";
        }

        internal static Dictionary<long, ManagedReferenceMissingType> GetMissingTypes(Object target)
        {
            Dictionary<long, ManagedReferenceMissingType> missing = new();
            if (SerializationUtility.HasManagedReferencesWithMissingTypes(target))
            {
                foreach (ManagedReferenceMissingType missingType in
                         SerializationUtility.GetManagedReferencesWithMissingTypes(target))
                {
                    missing[missingType.referenceId] = missingType;
                }
            }

            return missing;
        }

        /// <summary>
        ///     For each manager slot: the stored type of an unresolved manager, or <c>null</c> (resolved, or empty).
        /// </summary>
        /// <remarks>
        ///     Unity keeps missing-type data aside and leaves the live entry <c>null</c>, so the serialized element
        ///     usually reports the null reference id instead of the missing one. Slots whose id matches are assigned
        ///     directly; the remaining missing types go to the remaining <c>null</c> slots in reference id order
        ///     (ids are allocated in creation order).
        /// </remarks>
        internal static string[] GetUnresolvedManagerTypes(Scope scope)
        {
            IReadOnlyList<Manager> managers = scope.Managers;
            string[] result = new string[managers.Count];
            Dictionary<long, ManagedReferenceMissingType> missing = GetMissingTypes(scope);
            if (missing.Count == 0)
            {
                return result;
            }

            List<int> unmatchedSlots = new();
            using (SerializedObject serializedObject = new(scope))
            {
                SerializedProperty list = serializedObject.FindProperty(Scope.ManagersField);
                for (int i = 0; i < managers.Count && i < list.arraySize; i++)
                {
                    if (managers[i] != null)
                    {
                        continue;
                    }

                    long referenceId = list.GetArrayElementAtIndex(i).managedReferenceId;
                    if (missing.TryGetValue(referenceId, out ManagedReferenceMissingType missingType))
                    {
                        result[i] = FormatMissingType(missingType);
                        missing.Remove(referenceId);
                    }
                    else
                    {
                        unmatchedSlots.Add(i);
                    }
                }
            }

            List<long> remainingIds = new(missing.Keys);
            remainingIds.Sort();
            for (int i = 0; i < unmatchedSlots.Count && i < remainingIds.Count; i++)
            {
                result[unmatchedSlots[i]] = FormatMissingType(missing[remainingIds[i]]);
            }

            return result;
        }

        private static void ValidateManagers(Scope scope, List<ScopeIssue> issues)
        {
            string[] unresolved = GetUnresolvedManagerTypes(scope);
            for (int i = 0; i < scope.Managers.Count; i++)
            {
                if (scope.Managers[i] != null)
                {
                    continue;
                }

                if (unresolved[i] != null)
                {
                    issues.Add(new ScopeIssue(scope, ScopeIssueKind.UnresolvedManagerType, ScopeIssueSeverity.Error,
                        $"Manager {i + 1} has the unresolved type '{unresolved[i]}'. Was the class renamed, moved or " +
                        "deleted? Restore it, or keep it loadable with [MovedFrom]."));
                }
                else
                {
                    issues.Add(new ScopeIssue(scope, ScopeIssueKind.EmptyManagerSlot, ScopeIssueSeverity.Error,
                        $"Manager {i + 1} is empty. Remove the entry."));
                }
            }
        }

        /// <summary>The declared parents of <paramref name="scope" />, nearest first; stops at a cycle.</summary>
        private static List<Scope> CollectDeclaredAncestors(Scope scope, List<ScopeIssue> issues)
        {
            List<Scope> ancestors = new();
            HashSet<Scope> visited = new() { scope };

            for (Scope current = scope.DeclaredParent; current != null; current = current.DeclaredParent)
            {
                if (!visited.Add(current))
                {
                    List<string> names = new() { scope.name };
                    names.AddRange(ancestors.ConvertAll(ancestor => ancestor.name));
                    names.Add(current.name);
                    issues.Add(new ScopeIssue(scope, ScopeIssueKind.DeclaredParentCycle, ScopeIssueSeverity.Error,
                        $"The declared parents form a cycle: {string.Join(" → ", names)}."));
                    break;
                }

                ancestors.Add(current);
            }

            return ancestors;
        }

        private static void ValidateSingletons(Scope scope, List<Scope> ancestors, List<ScopeIssue> issues,
            List<SingletonRequirement> requirements)
        {
            // duplicates among the ancestors themselves are reported when those are validated
            Dictionary<Type, (Scope Scope, Type Manager)> inherited = new();
            foreach (Scope ancestor in ancestors)
            {
                foreach (Manager manager in ancestor.Managers)
                {
                    if (manager == null)
                    {
                        continue;
                    }

                    foreach (Type type in GetProvidedSingletons(manager.GetType()))
                    {
                        if (!inherited.ContainsKey(type))
                        {
                            inherited.Add(type, (ancestor, manager.GetType()));
                        }
                    }
                }
            }

            Dictionary<Type, (Scope Scope, Type Manager)> own = new();
            foreach (Manager manager in scope.Managers)
            {
                if (manager == null)
                {
                    continue;
                }

                foreach (Type type in GetProvidedSingletons(manager.GetType()))
                {
                    if (own.TryGetValue(type, out (Scope Scope, Type Manager) existing) ||
                        inherited.TryGetValue(type, out existing))
                    {
                        issues.Add(new ScopeIssue(scope, ScopeIssueKind.DuplicateSingleton, ScopeIssueSeverity.Error,
                            $"'{type.Name}' is provided twice: by {existing.Manager.Name} in '{existing.Scope.name}' " +
                            $"and by {manager.GetType().Name} in '{scope.name}'. Only one provider can be loaded at a " +
                            "time."));
                    }
                    else
                    {
                        own.Add(type, (scope, manager.GetType()));
                    }
                }
            }

            Dictionary<Type, SingletonRequirement> requirementsByType = new();
            foreach (Manager manager in scope.Managers)
            {
                if (manager == null)
                {
                    continue;
                }

                Type managerType = manager.GetType();
                foreach (Type type in GetRequiredSingletons(managerType))
                {
                    string problem = GetRequirementProblem(type);
                    if (problem != null)
                    {
                        issues.Add(new ScopeIssue(scope, ScopeIssueKind.InvalidRequirement, ScopeIssueSeverity.Error,
                            $"{managerType.Name} requires '{type.Name}', which {problem}"));
                        continue;
                    }

                    if (!requirementsByType.TryGetValue(type, out SingletonRequirement requirement))
                    {
                        requirement = new SingletonRequirement(type);
                        requirementsByType.Add(type, requirement);
                        requirements.Add(requirement);
                    }

                    requirement.AddRequiredBy(managerType);
                }
            }

            foreach (SingletonRequirement requirement in requirements)
            {
                if (own.TryGetValue(requirement.Type, out (Scope Scope, Type Manager) provider) ||
                    inherited.TryGetValue(requirement.Type, out provider))
                {
                    requirement.ProvidedBy = provider.Scope;
                    requirement.Provider = provider.Manager;
                    continue;
                }

                string requiredBy = string.Join(", ", requirement.RequiredBy.Select(type => type.Name));
                string message = ancestors.Count > 0
                    ? $"'{requirement.Type.Name}' (required by {requiredBy}) is not provided by '{scope.name}' or " +
                      "its declared parents."
                    : $"'{requirement.Type.Name}' (required by {requiredBy}) is not provided by '{scope.name}'. " +
                      "Declare a parent scope that provides it, or make sure the scope it is loaded under does.";
                issues.Add(new ScopeIssue(scope, ScopeIssueKind.MissingSingleton, ScopeIssueSeverity.Warning,
                    message));
            }
        }

        /// <summary>Why <paramref name="type" /> can never be resolved as a singleton, or <c>null</c>.</summary>
        private static string GetRequirementProblem(Type type)
        {
            if (!typeof(ISingleton).IsAssignableFrom(type))
            {
                return "is not a singleton type. Implement ISingleton or a singleton contract.";
            }

            if (type.IsInterface && !type.IsGenericTypeDefinition &&
                !typeof(ISingletonContract<>).MakeGenericType(type).IsAssignableFrom(type))
            {
                return $"is an interface but not a singleton contract. Declare it as '{type.Name} : " +
                       $"ISingletonContract<{type.Name}>'.";
            }

            return null;
        }

        /// <returns>Whether the scene's content was inspected.</returns>
        private static bool ValidateScene(Scope scope, bool inspectScene, List<ScopeIssue> issues)
        {
            SceneReference reference = scope.Scene;
            if (reference == null || !reference.IsAssigned)
            {
                return false;
            }

            // custom reference types have no editor support to check them with
            SceneReferenceAuthoring authoring = SceneReferenceAuthoring.For(reference.GetType());
            if (authoring == null)
            {
                return false;
            }

            string scenePath = authoring.GetScenePath(reference);
            if (scenePath == null)
            {
                issues.Add(new ScopeIssue(scope, ScopeIssueKind.SceneNotFound, ScopeIssueSeverity.Error,
                    $"The scene '{reference.DisplayName}' does not exist. Assign an existing scene, or set the scene " +
                    "to None."));
                return false;
            }

            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            string problem = authoring.GetRegistrationProblem(reference, scenePath);
            if (problem != null)
            {
                issues.Add(new ScopeIssue(scope, ScopeIssueKind.SceneNotRegistered, ScopeIssueSeverity.Error, problem,
                    sceneAsset));
            }

            if (!inspectScene || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return false;
            }

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool wasInHierarchy = scene.IsValid();
            bool wasLoaded = wasInHierarchy && scene.isLoaded;
            if (!wasLoaded)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }

            try
            {
                issues.AddRange(ValidateSceneContent(scope, sceneAsset.name, scene.GetRootGameObjects(), sceneAsset));
            }
            finally
            {
                if (!wasLoaded)
                {
                    EditorSceneManager.CloseScene(scene, !wasInHierarchy);
                }
            }

            return true;
        }

        private static void AddUnique(List<Type> types, Type type)
        {
            if (!types.Contains(type))
            {
                types.Add(type);
            }
        }
    }
}
