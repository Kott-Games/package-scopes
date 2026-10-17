using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

namespace Kott.Scopes
{
    /// <summary>
    ///     Scope definition inspector: scene with loader picker and Open button, declared parent, manager list with
    ///     type picker and each manager's contracts (provided, injected and where from, scene keys), the effective
    ///     initialization order, and a Health section with the <see cref="ScopeValidator" /> results.
    /// </summary>
    [CustomEditor(typeof(ScopeDefinition))]
    internal sealed class ScopeEditor : UnityEditor.Editor
    {
        private const float Indent = 12f;
        private const float Spacing = 2f;

        private static readonly GUIContent SceneLabel =
            new("Scene", "Loaded with the scope after its managers initialized. None = scene-less scope.");

        private static readonly GUIContent ParentLabel = new("Parent",
            "The scope this one is expected to be loaded under (optional). Validation resolves injected contracts " +
            "through it; loading under a different parent logs a warning.");

        private static GUIStyle _missingStyle;
        private static GUIStyle _detailStyle;

        private readonly AdvancedDropdownState _dropdownState = new();

        private SerializedProperty _sceneProperty;
        private SerializedProperty _parentProperty;
        private SerializedProperty _managersProperty;
        private ReorderableList _managerList;
        private string[] _unresolvedTypes = Array.Empty<string>();
        private string[] _managerDetails = Array.Empty<string>();
        private ScopeValidationResult _validation;

        private static GUIStyle MissingStyle =>
            _missingStyle ??= new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = new Color(0.9f, 0.3f, 0.3f) },
            };

        private static GUIStyle DetailStyle =>
            _detailStyle ??= new GUIStyle(EditorStyles.miniLabel)
            {
                clipping = TextClipping.Clip,
            };

        private ScopeDefinition Definition => (ScopeDefinition)target;

        private void OnEnable()
        {
            _sceneProperty = serializedObject.FindProperty(ScopeDefinition.SceneField);
            _parentProperty = serializedObject.FindProperty(ScopeDefinition.DeclaredParentField);
            _managersProperty = serializedObject.FindProperty(ScopeDefinition.ManagersField);

            _managerList = new ReorderableList(serializedObject, _managersProperty, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Managers"),
                drawElementCallback = DrawManager,
                elementHeightCallback = GetManagerHeight,
                onAddDropdownCallback = (rect, _) => new ManagerTypeDropdown(_dropdownState, AddManager).Show(rect),
            };

            Undo.undoRedoPerformed += OnUndoRedo;
            Revalidate(false);
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            DrawScene();
            EditorGUILayout.PropertyField(_parentProperty, ParentLabel);
            EditorGUILayout.Space();
            _managerList.DoLayoutList();
            bool changed = EditorGUI.EndChangeCheck();

            serializedObject.ApplyModifiedProperties();
            if (changed)
            {
                Revalidate(false);
            }

            DrawInitializationOrder();
            DrawHealth();
        }

        private void OnUndoRedo()
        {
            Revalidate(false);
            Repaint();
        }

        private void Revalidate(bool inspectScene)
        {
            if (Definition == null)
            {
                return;
            }

            _unresolvedTypes = ScopeValidator.GetUnresolvedManagerTypes(Definition);
            _validation = ScopeValidator.Validate(Definition, inspectScene);
            _managerDetails = Definition.Managers
                .Select(manager => manager != null ? DescribeManager(manager.GetType()) : null)
                .ToArray();
        }

        /// <summary>"provides IFoo · needs IBar ← Core, IBaz ✗ · scene: key" for one manager, or <c>null</c>.</summary>
        private string DescribeManager(Type type)
        {
            List<string> parts = new();
            IReadOnlyList<Type> provided = ScopeValidator.GetProvidedContracts(type);
            if (provided.Count > 0)
            {
                parts.Add("provides " + string.Join(", ", provided.Select(contract => contract.Name)));
            }

            IReadOnlyList<Type> injected = ScopeValidator.GetInjectedContracts(type);
            if (injected.Count > 0)
            {
                parts.Add("needs " + string.Join(", ", injected.Select(DescribeRequirement)));
            }

            IReadOnlyList<string> sceneKeys = ScopeValidator.GetSceneKeys(type);
            if (sceneKeys.Count > 0)
            {
                parts.Add("scene: " + string.Join(", ", sceneKeys));
            }

            return parts.Count > 0 ? string.Join("  ·  ", parts) : null;
        }

        private string DescribeRequirement(Type contract)
        {
            ContractRequirement requirement =
                _validation.Requirements.FirstOrDefault(candidate => candidate.Type == contract);
            if (requirement == null)
            {
                return $"{contract.Name} (invalid)";
            }

            if (!requirement.IsProvided)
            {
                return requirement.IsOptional ? $"{contract.Name} (optional, missing)" : $"{contract.Name} ✗";
            }

            return requirement.ProvidedBy == Definition
                ? contract.Name
                : $"{contract.Name} ← {requirement.ProvidedBy.name}";
        }

        private void DrawScene()
        {
            IReadOnlyList<SceneReferenceAuthoring> loaders = SceneReferenceAuthoring.All;
            SceneReference current = _sceneProperty.managedReferenceValue as SceneReference;
            SceneReferenceAuthoring authoring = current != null ? SceneReferenceAuthoring.For(current.GetType()) : null;

            List<string> options = new() { "None" };
            options.AddRange(loaders.Select(loader => loader.DisplayName));
            int index = 0;
            if (current != null)
            {
                index = authoring != null ? loaders.ToList().IndexOf(authoring) + 1 : options.Count;
                if (authoring == null)
                {
                    options.Add(ObjectNames.NicifyVariableName(current.GetType().Name));
                }
            }

            Rect line = EditorGUILayout.GetControlRect();
            Rect field = EditorGUI.PrefixLabel(line, SceneLabel);
            Rect popup = new(field.x, field.y, 100f, field.height);
            Rect open = new(field.xMax - 48f, field.y, 48f, field.height);
            Rect reference = new(popup.xMax + Spacing, field.y, open.x - popup.xMax - 2 * Spacing, field.height);

            int indentLevel = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            int selected = EditorGUI.Popup(popup, index, options.ToArray());
            if (selected != index && selected <= loaders.Count)
            {
                _sceneProperty.managedReferenceValue =
                    selected == 0 ? null : Activator.CreateInstance(loaders[selected - 1].ReferenceType);
                EditorGUI.indentLevel = indentLevel;
                return;
            }

            string scenePath = null;
            if (authoring != null)
            {
                authoring.DrawField(reference, _sceneProperty);
                scenePath = authoring.GetScenePath(current);
            }

            using (new EditorGUI.DisabledScope(scenePath == null || EditorApplication.isPlaying))
            {
                if (GUI.Button(open, new GUIContent("Open", scenePath), EditorStyles.miniButton) &&
                    EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(scenePath);
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUI.indentLevel = indentLevel;

            // custom reference types: draw their fields generically
            if (current != null && authoring == null)
            {
                EditorGUI.indentLevel++;
                DrawChildren(_sceneProperty);
                EditorGUI.indentLevel--;
            }
        }

        private static void DrawChildren(SerializedProperty property)
        {
            SerializedProperty child = property.Copy();
            SerializedProperty end = property.GetEndProperty();
            bool enterChildren = true;
            while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
            {
                enterChildren = false;
                EditorGUILayout.PropertyField(child, true);
            }
        }

        private void DrawManager(Rect rect, int index, bool isActive, bool isFocused)
        {
            SerializedProperty element = _managersProperty.GetArrayElementAtIndex(index);
            Rect header = new(rect.x, rect.y + Spacing, rect.width, EditorGUIUtility.singleLineHeight);

            if (element.managedReferenceValue is not Manager manager)
            {
                string unresolved = index < _unresolvedTypes.Length ? _unresolvedTypes[index] : null;
                string text = unresolved != null
                    ? $"{index + 1}  Missing type: {unresolved}"
                    : $"{index + 1}  Empty";
                EditorGUI.LabelField(header, new GUIContent(text,
                    "The stored type could not be resolved. Restore the class, or keep it loadable with " +
                    "[MovedFrom]; removing the entry deletes its data."), MissingStyle);
                return;
            }

            Type type = manager.GetType();
            GUIContent title = new($"{index + 1}  {ObjectNames.NicifyVariableName(type.Name)}", type.FullName);

            Rect foldout = new(header.x + Indent, header.y, header.width - Indent, header.height);
            if (element.hasVisibleChildren)
            {
                element.isExpanded = EditorGUI.Foldout(foldout, element.isExpanded, title, true);
            }
            else
            {
                EditorGUI.LabelField(foldout, title);
            }

            float y = header.yMax + Spacing;
            string detail = GetDetail(index);
            if (detail != null)
            {
                Rect detailRect = new(rect.x + 2 * Indent, y, rect.width - 2 * Indent,
                    EditorGUIUtility.singleLineHeight);
                EditorGUI.LabelField(detailRect, new GUIContent(detail, detail), DetailStyle);
                y = detailRect.yMax + Spacing;
            }

            if (!element.hasVisibleChildren || !element.isExpanded)
            {
                return;
            }

            SerializedProperty child = element.Copy();
            SerializedProperty end = element.GetEndProperty();
            bool enterChildren = true;
            while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
            {
                enterChildren = false;
                float height = EditorGUI.GetPropertyHeight(child, true);
                EditorGUI.PropertyField(new Rect(rect.x + Indent, y, rect.width - Indent, height), child, true);
                y += height + Spacing;
            }
        }

        private float GetManagerHeight(int index)
        {
            SerializedProperty element = _managersProperty.GetArrayElementAtIndex(index);
            float height = EditorGUIUtility.singleLineHeight + 2 * Spacing;
            if (element.managedReferenceValue == null)
            {
                return height;
            }

            if (GetDetail(index) != null)
            {
                height += EditorGUIUtility.singleLineHeight + Spacing;
            }

            if (!element.hasVisibleChildren || !element.isExpanded)
            {
                return height;
            }

            SerializedProperty child = element.Copy();
            SerializedProperty end = element.GetEndProperty();
            bool enterChildren = true;
            while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
            {
                enterChildren = false;
                height += EditorGUI.GetPropertyHeight(child, true) + Spacing;
            }

            return height;
        }

        private string GetDetail(int index)
        {
            return index < _managerDetails.Length ? _managerDetails[index] : null;
        }

        private void AddManager(Type type)
        {
            serializedObject.Update();
            int index = _managersProperty.arraySize;
            _managersProperty.InsertArrayElementAtIndex(index);
            SerializedProperty element = _managersProperty.GetArrayElementAtIndex(index);
            element.managedReferenceValue = Activator.CreateInstance(type);
            element.isExpanded = true;
            serializedObject.ApplyModifiedProperties();

            Revalidate(false);
            Repaint();
        }

        private void DrawInitializationOrder()
        {
            IReadOnlyList<Manager> managers = Definition.Managers;
            if (_validation == null || _validation.InitializationOrder.Count < 2)
            {
                return;
            }

            string order = string.Join(" → ", _validation.InitializationOrder
                .Where(index => index < managers.Count && managers[index] != null)
                .Select(index => $"{index + 1} {ObjectNames.NicifyVariableName(managers[index].GetType().Name)}"));
            EditorGUILayout.LabelField(new GUIContent("Initialization order:  " + order,
                    "Managers initialize after the managers of this scope they inject, otherwise in list order. " +
                    "They uninitialize in reverse."),
                EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawHealth()
        {
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Health", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                {
                    if (GUILayout.Button(new GUIContent("Check Scene",
                                "Also open the scene (additively, if it isn't open) and check its SceneRoot and SceneLinks."),
                            EditorStyles.miniButton, GUILayout.Width(90f)))
                    {
                        Revalidate(true);
                    }
                }
            }

            if (_validation == null)
            {
                return;
            }

            foreach (ContractRequirement requirement in _validation.Requirements)
            {
                string requiredBy =
                    "injected by " + string.Join(", ", requirement.RequiredBy.Select(type => type.Name));
                GUIContent content;
                if (requirement.IsProvided)
                {
                    content = new GUIContent($"{requirement.Type.Name}  ← {requirement.ProvidedBy.name}",
                        EditorGUIUtility.IconContent("TestPassed").image, requiredBy);
                }
                else if (requirement.IsOptional)
                {
                    content = new GUIContent(
                        $"{requirement.Type.Name}  (optional) not provided by this scope or its declared parents",
                        EditorGUIUtility.IconContent("console.infoicon.sml").image, requiredBy);
                }
                else
                {
                    string icon = Definition.DeclaredParent != null ? "console.erroricon.sml" : "console.warnicon.sml";
                    content = new GUIContent(
                        $"{requirement.Type.Name}  not provided by this scope or its declared parents",
                        EditorGUIUtility.IconContent(icon).image, requiredBy);
                }

                EditorGUILayout.LabelField(content);
            }

            int shown = 0;
            foreach (ScopeIssue issue in _validation.Issues)
            {
                if (issue.Kind == ScopeIssueKind.MissingContract)
                {
                    continue; // listed with the requirements
                }

                EditorGUILayout.HelpBox(issue.Message,
                    issue.Severity == ScopeIssueSeverity.Error ? MessageType.Error : MessageType.Warning);
                if (issue.CanFix && GUILayout.Button(issue.FixLabel, EditorStyles.miniButton))
                {
                    issue.Fix();
                    serializedObject.Update();
                    Revalidate(false);
                    GUIUtility.ExitGUI();
                }

                shown++;
            }

            if (shown == 0 &&
                _validation.Requirements.All(requirement => requirement.IsProvided || requirement.IsOptional))
            {
                EditorGUILayout.LabelField(_validation.SceneInspected
                    ? "No issues."
                    : "No issues (scene content not checked).", EditorStyles.miniLabel);
            }
        }
    }
}
