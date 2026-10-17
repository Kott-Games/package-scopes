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
    ///     Scope inspector: scene with loader picker and Open button, declared parent, ordered manager list with
    ///     type picker and provided contracts, and a Health section with the <see cref="ScopeValidator" /> results.
    /// </summary>
    [CustomEditor(typeof(Scope))]
    internal sealed class ScopeEditor : UnityEditor.Editor
    {
        private const float Indent = 12f;
        private const float Spacing = 2f;

        private static readonly GUIContent SceneLabel =
            new("Scene", "Loaded with the scope after its managers initialized. None = scene-less scope.");

        private static readonly GUIContent ParentLabel = new("Parent",
            "The scope this one is expected to be loaded under (optional). Validation resolves required singletons " +
            "through it; loading under a different parent logs a warning.");

        private static GUIStyle _missingStyle;
        private static GUIStyle _providesStyle;

        private readonly AdvancedDropdownState _dropdownState = new();

        private SerializedProperty _sceneProperty;
        private SerializedProperty _parentProperty;
        private SerializedProperty _managersProperty;
        private ReorderableList _managerList;
        private string[] _unresolvedTypes = Array.Empty<string>();
        private ScopeValidationResult _validation;

        private static GUIStyle MissingStyle => _missingStyle ??= new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = new Color(0.9f, 0.3f, 0.3f) },
        };

        private static GUIStyle ProvidesStyle => _providesStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleRight,
        };

        private Scope Scope => (Scope)target;

        private void OnEnable()
        {
            _sceneProperty = serializedObject.FindProperty(Scope.SceneField);
            _parentProperty = serializedObject.FindProperty(Scope.DeclaredParentField);
            _managersProperty = serializedObject.FindProperty(Scope.ManagersField);

            _managerList = new ReorderableList(serializedObject, _managersProperty, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Managers (initialization order)"),
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

            DrawHealth();
        }

        private void OnUndoRedo()
        {
            Revalidate(false);
            Repaint();
        }

        private void Revalidate(bool inspectScene)
        {
            if (Scope == null)
            {
                return;
            }

            _unresolvedTypes = ScopeValidator.GetUnresolvedManagerTypes(Scope);
            _validation = ScopeValidator.Validate(Scope, inspectScene);
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

            IReadOnlyList<Type> provided = ScopeValidator.GetProvidedSingletons(type);
            if (provided.Count > 0)
            {
                string provides = "provides " + string.Join(", ", provided.Select(providedType => providedType.Name));
                EditorGUI.LabelField(header, new GUIContent(provides, provides), ProvidesStyle);
            }

            Rect foldout = new(header.x + Indent, header.y, header.width - Indent, header.height);
            if (!element.hasVisibleChildren)
            {
                EditorGUI.LabelField(foldout, title);
                return;
            }

            element.isExpanded = EditorGUI.Foldout(foldout, element.isExpanded, title, true);
            if (!element.isExpanded)
            {
                return;
            }

            float y = header.yMax + Spacing;
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
            if (element.managedReferenceValue == null || !element.isExpanded)
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

            foreach (SingletonRequirement requirement in _validation.Requirements)
            {
                string requiredBy = "required by " + string.Join(", ", requirement.RequiredBy.Select(type => type.Name));
                GUIContent content = requirement.IsProvided
                    ? new GUIContent($"{requirement.Type.Name}  ← {requirement.ProvidedBy.name}",
                        EditorGUIUtility.IconContent("TestPassed").image, requiredBy)
                    : new GUIContent($"{requirement.Type.Name}  not provided by this scope or its declared parents",
                        EditorGUIUtility.IconContent("console.warnicon.sml").image, requiredBy);
                EditorGUILayout.LabelField(content);
            }

            int shown = 0;
            foreach (ScopeIssue issue in _validation.Issues)
            {
                if (issue.Kind == ScopeIssueKind.MissingSingleton)
                {
                    continue; // listed with the requirements
                }

                EditorGUILayout.HelpBox(issue.Message,
                    issue.Severity == ScopeIssueSeverity.Error ? MessageType.Error : MessageType.Warning);
                shown++;
            }

            if (shown == 0 && _validation.Requirements.All(requirement => requirement.IsProvided))
            {
                EditorGUILayout.LabelField(_validation.SceneInspected
                    ? "No issues."
                    : "No issues (scene content not checked).", EditorStyles.miniLabel);
            }
        }
    }
}
