using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Kott.Scopes
{
    /// <summary>
    ///     Checks scope definitions for authoring mistakes: unresolved or empty managers, injected contracts that are
    ///     not provided (or provided twice) along the declared parents, invalid injected fields, dependency cycles,
    ///     declared-parent cycles, scenes that are missing, unregistered with their loader, without a
    ///     <see cref="SceneRoot" />, with duplicate <see cref="SceneLink" /> keys, or without the links
    ///     <see cref="InjectSceneAttribute" /> fields need.
    /// </summary>
    /// <remarks>
    ///     Used by the definition inspector and <c>Tools &gt; Scopes &gt; Validate All</c>; call it from tests or CI
    ///     to fail a build on errors:
    ///     <code>
    /// foreach (ScopeValidationResult result in ScopeValidator.ValidateAll())
    ///     Assert.That(result.HasErrors, Is.False, result.ToString());
    /// </code>
    ///     A contract missing along the declared parents is a warning if the definition declares no parent (the
    ///     scope it is loaded under may provide it) and an error otherwise: declare parents up to the root scope.
    /// </remarks>
    public static class ScopeValidator
    {
        /// <summary>Validates one definition.</summary>
        /// <param name="definition">The definition to validate.</param>
        /// <param name="inspectScene">
        ///     Also check the content of the scope's scene. Opens the scene additively (and closes it again) if it
        ///     is not loaded in the editor. Ignored in play mode.
        /// </param>
        public static ScopeValidationResult Validate(ScopeDefinition definition, bool inspectScene = true)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            List<ScopeIssue> issues = new();
            List<ContractRequirement> requirements = new();

            ValidateManagers(definition, issues);
            List<ScopeDefinition> ancestors = CollectDeclaredAncestors(definition, issues);
            ValidateContracts(definition, ancestors, issues, requirements);
            List<int> order = ValidateOrder(definition, issues);
            ValidateSceneFields(definition, issues);
            bool sceneInspected = ValidateScene(definition, inspectScene, issues);

            return new ScopeValidationResult(definition, issues, requirements, order, sceneInspected);
        }

        /// <summary>Validates every scope definition in the project (including packages).</summary>
        /// <param name="inspectScenes">See <see cref="Validate" />.</param>
        public static IReadOnlyList<ScopeValidationResult> ValidateAll(bool inspectScenes = true)
        {
            List<ScopeValidationResult> results = new();
            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(ScopeDefinition)}"))
            {
                ScopeDefinition definition =
                    AssetDatabase.LoadAssetAtPath<ScopeDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (definition != null)
                {
                    results.Add(Validate(definition, inspectScenes));
                }
            }

            return results;
        }

        /// <summary>The manager contracts a manager of <paramref name="managerType" /> provides.</summary>
        internal static IReadOnlyList<Type> GetProvidedContracts(Type managerType)
        {
            return Injector.GetContracts(managerType);
        }

        /// <summary>
        ///     The types of the <see cref="InjectAttribute" /> fields of <paramref name="managerType" /> (including
        ///     inherited and private ones), without duplicates. Includes types that are not valid contracts.
        /// </summary>
        internal static IReadOnlyList<Type> GetInjectedContracts(Type managerType)
        {
            List<Type> injected = new();
            foreach (InjectedField field in Injector.GetInfo(managerType).Fields)
            {
                if (!injected.Contains(field.Field.FieldType))
                {
                    injected.Add(field.Field.FieldType);
                }
            }

            return injected;
        }

        /// <summary>The <see cref="SceneLink" /> keys of the <see cref="InjectSceneAttribute" /> fields of <paramref name="managerType" />.</summary>
        internal static IReadOnlyList<string> GetSceneKeys(Type managerType)
        {
            List<string> keys = new();
            foreach (InjectedSceneField field in Injector.GetInfo(managerType).SceneFields)
            {
                if (!string.IsNullOrEmpty(field.Key) && !keys.Contains(field.Key))
                {
                    keys.Add(field.Key);
                }
            }

            return keys;
        }

        /// <summary>Checks the content of a scope's scene, given its root objects.</summary>
        internal static List<ScopeIssue> ValidateSceneContent(ScopeDefinition definition, string sceneName,
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
                issues.Add(new ScopeIssue(definition, ScopeIssueKind.SceneWithoutSceneRoot, ScopeIssueSeverity.Error,
                    $"The scene '{sceneName}' has no SceneRoot, so nothing in it is injected, managers get no scene " +
                    "callbacks and SceneLinks are not bound. Add one (Add Component > Scopes > Scene Root), or create " +
                    "the scene from the Scope Scene template.", context));
            }
            else if (sceneRoots.Count > 1)
            {
                issues.Add(new ScopeIssue(definition, ScopeIssueKind.MultipleSceneRoots, ScopeIssueSeverity.Error,
                    $"The scene '{sceneName}' has {sceneRoots.Count} SceneRoots " +
                    $"({string.Join(", ", sceneRoots.ConvertAll(sceneRoot => $"'{sceneRoot.name}'"))}); only one " +
                    "is used. Remove the others.", context));
            }

            Dictionary<string, SceneLink> linksByKey = new();
            foreach (SceneLink link in links)
            {
                if (string.IsNullOrEmpty(link.Key))
                {
                    issues.Add(new ScopeIssue(definition, ScopeIssueKind.EmptySceneLinkKey, ScopeIssueSeverity.Warning,
                        $"The SceneLink on '{link.name}' in scene '{sceneName}' has no key and is ignored.", context));
                }
                else if (linksByKey.TryGetValue(link.Key, out SceneLink existing))
                {
                    issues.Add(new ScopeIssue(definition, ScopeIssueKind.DuplicateSceneLinkKey,
                        ScopeIssueSeverity.Error,
                        $"Duplicate SceneLink key '{link.Key}' in scene '{sceneName}': on '{existing.name}' and " +
                        $"'{link.name}'. Keys must be unique within a scene.", context));
                }
                else
                {
                    linksByKey.Add(link.Key, link);
                }
            }

            foreach (Manager manager in definition.Managers)
            {
                if (manager == null)
                {
                    continue;
                }

                foreach (InjectedSceneField field in Injector.GetInfo(manager.GetType()).SceneFields)
                {
                    if (field.Optional || Injector.GetFieldProblem(field) != null)
                    {
                        continue;
                    }

                    string fieldName = $"{manager.GetType().Name}.{field.Field.Name}";
                    Type type = field.Field.FieldType;
                    if (!linksByKey.TryGetValue(field.Key, out SceneLink link))
                    {
                        issues.Add(new ScopeIssue(definition, ScopeIssueKind.MissingSceneLink,
                            ScopeIssueSeverity.Error,
                            $"{fieldName} needs the SceneLink '{field.Key}', which scene '{sceneName}' does not have. " +
                            "Add a SceneLink with that key, or mark the field Optional.", context));
                    }
                    else if (type != typeof(GameObject) && link.GetComponent(type) == null)
                    {
                        issues.Add(new ScopeIssue(definition, ScopeIssueKind.MissingSceneLinkComponent,
                            ScopeIssueSeverity.Error,
                            $"{fieldName} needs a {type.Name} on SceneLink '{field.Key}' ('{link.name}' in scene " +
                            $"'{sceneName}'), which it does not have.", context));
                    }
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
        internal static string[] GetUnresolvedManagerTypes(ScopeDefinition definition)
        {
            IReadOnlyList<Manager> managers = definition.Managers;
            string[] result = new string[managers.Count];
            Dictionary<long, ManagedReferenceMissingType> missing = GetMissingTypes(definition);
            if (missing.Count == 0)
            {
                return result;
            }

            List<int> unmatchedSlots = new();
            using (SerializedObject serializedObject = new(definition))
            {
                SerializedProperty list = serializedObject.FindProperty(ScopeDefinition.ManagersField);
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

        private static void ValidateManagers(ScopeDefinition definition, List<ScopeIssue> issues)
        {
            string[] unresolved = GetUnresolvedManagerTypes(definition);
            for (int i = 0; i < definition.Managers.Count; i++)
            {
                if (definition.Managers[i] != null)
                {
                    continue;
                }

                if (unresolved[i] != null)
                {
                    issues.Add(new ScopeIssue(definition, ScopeIssueKind.UnresolvedManagerType,
                        ScopeIssueSeverity.Error,
                        $"Manager {i + 1} has the unresolved type '{unresolved[i]}'. Was the class renamed, moved or " +
                        "deleted? Restore it, or keep it loadable with [MovedFrom]."));
                }
                else
                {
                    issues.Add(new ScopeIssue(definition, ScopeIssueKind.EmptyManagerSlot, ScopeIssueSeverity.Error,
                        $"Manager {i + 1} is empty. Remove the entry."));
                }
            }
        }

        /// <summary>The declared parents of <paramref name="definition" />, nearest first; stops at a cycle.</summary>
        private static List<ScopeDefinition> CollectDeclaredAncestors(ScopeDefinition definition,
            List<ScopeIssue> issues)
        {
            List<ScopeDefinition> ancestors = new();
            HashSet<ScopeDefinition> visited = new() { definition };

            for (ScopeDefinition current = definition.DeclaredParent;
                 current != null;
                 current = current.DeclaredParent)
            {
                if (!visited.Add(current))
                {
                    List<string> names = new() { definition.name };
                    names.AddRange(ancestors.ConvertAll(ancestor => ancestor.name));
                    names.Add(current.name);
                    issues.Add(new ScopeIssue(definition, ScopeIssueKind.DeclaredParentCycle, ScopeIssueSeverity.Error,
                        $"The declared parents form a cycle: {string.Join(" → ", names)}."));
                    break;
                }

                ancestors.Add(current);
            }

            return ancestors;
        }

        private static void ValidateContracts(ScopeDefinition definition, List<ScopeDefinition> ancestors,
            List<ScopeIssue> issues, List<ContractRequirement> requirements)
        {
            // duplicates among the ancestors themselves are reported when those are validated
            Dictionary<Type, (ScopeDefinition Definition, Type Manager)> inherited = new();
            foreach (ScopeDefinition ancestor in ancestors)
            {
                foreach (Manager manager in ancestor.Managers)
                {
                    if (manager == null)
                    {
                        continue;
                    }

                    foreach (Type type in Injector.GetContracts(manager.GetType()))
                    {
                        inherited.TryAdd(type, (ancestor, manager.GetType()));
                    }
                }
            }

            Dictionary<Type, (ScopeDefinition Definition, Type Manager)> own = new();
            foreach (Manager manager in definition.Managers)
            {
                if (manager == null)
                {
                    continue;
                }

                foreach (Type type in Injector.GetContracts(manager.GetType()))
                {
                    if (own.TryGetValue(type, out (ScopeDefinition Definition, Type Manager) existing) ||
                        inherited.TryGetValue(type, out existing))
                    {
                        issues.Add(new ScopeIssue(definition, ScopeIssueKind.DuplicateContract,
                            ScopeIssueSeverity.Error,
                            $"'{type.Name}' is provided twice: by {existing.Manager.Name} in " +
                            $"'{existing.Definition.name}' and by {manager.GetType().Name} in '{definition.name}'. A " +
                            "contract can only have one provider per scope chain."));
                    }
                    else
                    {
                        own.Add(type, (definition, manager.GetType()));
                    }
                }
            }

            Dictionary<Type, ContractRequirement> requirementsByType = new();
            foreach (Manager manager in definition.Managers)
            {
                if (manager == null)
                {
                    continue;
                }

                Type managerType = manager.GetType();
                foreach (InjectedField field in Injector.GetInfo(managerType).Fields)
                {
                    string problem = Injector.GetFieldProblem(field);
                    if (problem != null)
                    {
                        issues.Add(new ScopeIssue(definition, ScopeIssueKind.InvalidInjection,
                            ScopeIssueSeverity.Error, $"[Inject] {managerType.Name}.{field.Field.Name}: {problem}"));
                        continue;
                    }

                    Type type = field.Field.FieldType;
                    if (!requirementsByType.TryGetValue(type, out ContractRequirement requirement))
                    {
                        requirement = new ContractRequirement(type);
                        requirementsByType.Add(type, requirement);
                        requirements.Add(requirement);
                    }

                    requirement.AddRequiredBy(managerType, field.Optional);
                }
            }

            foreach (ContractRequirement requirement in requirements)
            {
                if (own.TryGetValue(requirement.Type, out (ScopeDefinition Definition, Type Manager) provider) ||
                    inherited.TryGetValue(requirement.Type, out provider))
                {
                    requirement.ProvidedBy = provider.Definition;
                    requirement.Provider = provider.Manager;
                    continue;
                }

                if (requirement.IsOptional)
                {
                    continue;
                }

                string requiredBy = string.Join(", ", requirement.RequiredBy.Select(type => type.Name));
                bool hasDeclaredParent = ancestors.Count > 0;
                string message = hasDeclaredParent
                    ? $"'{requirement.Type.Name}' (injected by {requiredBy}) is not provided by '{definition.name}' " +
                      "or its declared parents."
                    : $"'{requirement.Type.Name}' (injected by {requiredBy}) is not provided by '{definition.name}'. " +
                      "Declare a parent scope that provides it, or make sure the scope it is loaded under does.";
                issues.Add(new ScopeIssue(definition, ScopeIssueKind.MissingContract,
                    hasDeclaredParent ? ScopeIssueSeverity.Error : ScopeIssueSeverity.Warning, message));
            }
        }

        /// <returns>The initialization order, or an empty list if it cannot be computed.</returns>
        private static List<int> ValidateOrder(ScopeDefinition definition, List<ScopeIssue> issues)
        {
            if (definition.Managers.Any(manager => manager == null))
            {
                return new List<int>();
            }

            List<Type> types = definition.Managers.Select(manager => manager.GetType()).ToList();
            if (InitializationOrder.TryCompute(types, out List<int> order, out List<int> cycle))
            {
                return order;
            }

            issues.Add(new ScopeIssue(definition, ScopeIssueKind.DependencyCycle, ScopeIssueSeverity.Error,
                $"The managers inject each other in a cycle: {InitializationOrder.DescribeCycle(types, cycle)}. " +
                "Break it, e.g. by moving shared state into a third manager."));
            return new List<int>();
        }

        private static void ValidateSceneFields(ScopeDefinition definition, List<ScopeIssue> issues)
        {
            bool hasScene = definition.Scene is { IsAssigned: true };
            foreach (Manager manager in definition.Managers)
            {
                if (manager == null)
                {
                    continue;
                }

                Type managerType = manager.GetType();
                foreach (InjectedSceneField field in Injector.GetInfo(managerType).SceneFields)
                {
                    string problem = Injector.GetFieldProblem(field);
                    if (problem != null)
                    {
                        issues.Add(new ScopeIssue(definition, ScopeIssueKind.InvalidInjection,
                            ScopeIssueSeverity.Error,
                            $"[InjectScene] {managerType.Name}.{field.Field.Name}: {problem}"));
                    }
                    else if (!hasScene && !field.Optional)
                    {
                        issues.Add(new ScopeIssue(definition, ScopeIssueKind.SceneInjectionWithoutScene,
                            ScopeIssueSeverity.Error,
                            $"{managerType.Name}.{field.Field.Name} needs the SceneLink '{field.Key}', but " +
                            $"'{definition.name}' has no scene. Assign a scene, or mark the field Optional."));
                    }
                }
            }
        }

        /// <returns>Whether the scene's content was inspected.</returns>
        private static bool ValidateScene(ScopeDefinition definition, bool inspectScene, List<ScopeIssue> issues)
        {
            SceneReference reference = definition.Scene;
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
                issues.Add(new ScopeIssue(definition, ScopeIssueKind.SceneNotFound, ScopeIssueSeverity.Error,
                    $"The scene '{reference.DisplayName}' does not exist. Assign an existing scene, or set the scene " +
                    "to None."));
                return false;
            }

            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            string problem = authoring.GetRegistrationProblem(reference, scenePath);
            if (problem != null)
            {
                issues.Add(new ScopeIssue(definition, ScopeIssueKind.SceneNotRegistered, ScopeIssueSeverity.Error,
                    problem, sceneAsset, $"Register with {authoring.DisplayName}",
                    () => RegisterScene(definition, authoring, scenePath)));
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
                issues.AddRange(ValidateSceneContent(definition, sceneAsset.name, scene.GetRootGameObjects(),
                    sceneAsset));
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

        private static void RegisterScene(ScopeDefinition definition, SceneReferenceAuthoring authoring,
            string scenePath)
        {
            SceneReference reference = authoring.CreateAndRegister(scenePath);
            Undo.RecordObject(definition, "Register Scope Scene");
            definition.SetScene(reference);
            EditorUtility.SetDirty(definition);
            if (EditorUtility.IsPersistent(definition))
            {
                AssetDatabase.SaveAssetIfDirty(definition);
            }
        }
    }
}
