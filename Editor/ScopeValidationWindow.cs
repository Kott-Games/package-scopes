using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Kott.Scopes
{
    /// <summary>Lists the <see cref="ScopeValidator" /> issues of every scope definition; click an entry to select it.</summary>
    internal sealed class ScopeValidationWindow : EditorWindow
    {
        private IReadOnlyList<ScopeValidationResult> _results;
        private Vector2 _scroll;
        private GUIStyle _messageStyle;

        [MenuItem("Tools/Scopes/Validate All")]
        private static void Open()
        {
            ScopeValidationWindow window = GetWindow<ScopeValidationWindow>("Scope Validation");
            window.Validate();
        }

        private void Validate()
        {
            _results = ScopeValidator.ValidateAll(!EditorApplication.isPlaying);
        }

        private void OnGUI()
        {
            _messageStyle ??= new GUIStyle(EditorStyles.label) { wordWrap = true };

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Validate", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                {
                    Validate();
                }

                GUILayout.FlexibleSpace();
                if (_results != null)
                {
                    GUILayout.Label(Summarize(), EditorStyles.miniLabel);
                }
            }

            if (_results == null)
            {
                EditorGUILayout.HelpBox("Press Validate to check all scope definitions.", MessageType.Info);
                return;
            }

            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Scene content is not checked in play mode.", MessageType.Info);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (ScopeValidationResult result in _results)
            {
                if (result.Issues.Count == 0 || result.Definition == null)
                {
                    continue;
                }

                if (GUILayout.Button(result.Definition.name, EditorStyles.boldLabel))
                {
                    Select(result.Definition);
                }

                foreach (ScopeIssue issue in result.Issues)
                {
                    Texture icon = EditorGUIUtility.IconContent(issue.Severity == ScopeIssueSeverity.Error
                        ? "console.erroricon.sml"
                        : "console.warnicon.sml").image;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(12f);
                        GUILayout.Label(icon, GUILayout.Width(18f), GUILayout.Height(18f));
                        if (GUILayout.Button(issue.Message, _messageStyle))
                        {
                            Select(issue.Context);
                        }

                        if (issue.CanFix && GUILayout.Button(issue.FixLabel, EditorStyles.miniButton,
                                GUILayout.ExpandWidth(false)))
                        {
                            issue.Fix();
                            Validate();
                            GUIUtility.ExitGUI();
                        }
                    }
                }

                EditorGUILayout.Space();
            }

            EditorGUILayout.EndScrollView();
        }

        private string Summarize()
        {
            int errors = 0;
            int warnings = 0;
            foreach (ScopeValidationResult result in _results)
            {
                foreach (ScopeIssue issue in result.Issues)
                {
                    if (issue.Severity == ScopeIssueSeverity.Error)
                    {
                        errors++;
                    }
                    else
                    {
                        warnings++;
                    }
                }
            }

            return $"{_results.Count} scopes · {errors} errors · {warnings} warnings";
        }

        private static void Select(Object target)
        {
            if (target == null)
            {
                return;
            }

            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }
    }
}
