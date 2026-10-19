using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneTemplate;
using UnityEngine;

namespace Kott.Scopes
{
    /// <summary>Creates a scope definition together with its scene (from the Scope Scene template) in one step.</summary>
    internal static class ScopeCreation
    {
        internal const string TemplatePath = "Packages/com.kott.scopes/Runtime/ScopeScene.scenetemplate";

        [MenuItem("Assets/Create/Scopes/Scope with Scene (Build Profile)", priority = 1)]
        private static void CreateWithBuildScene()
        {
            CreateInteractive(SceneReferenceAuthoring.For(typeof(BuildSceneReference)));
        }

        /// <summary>Asks for the definition path, then creates definition and scene and selects the definition.</summary>
        internal static void CreateInteractive(SceneReferenceAuthoring authoring)
        {
            string scopePath = EditorUtility.SaveFilePanelInProject("Create Scope with Scene", "NewScope", "asset",
                $"The scene is created next to the definition (same name) and loaded through {authoring.DisplayName}.",
                GetSelectedFolder());
            if (string.IsNullOrEmpty(scopePath))
            {
                return;
            }

            try
            {
                ScopeDefinition definition = CreateWithScene(scopePath, authoring);
                Selection.activeObject = definition;
                EditorGUIUtility.PingObject(definition);
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Create Scope with Scene", exception.Message, "OK");
            }
        }

        /// <summary>
        ///     Creates a scope definition at <paramref name="scopePath" /> and a scene with the same name next to it, copied from
        ///     the Scope Scene template and registered through <paramref name="authoring" />.
        /// </summary>
        /// <exception cref="InvalidOperationException">An asset already exists at either path, or the template is missing.</exception>
        internal static ScopeDefinition CreateWithScene(string scopePath, SceneReferenceAuthoring authoring)
        {
            if (authoring == null)
            {
                throw new ArgumentNullException(nameof(authoring));
            }

            string scenePath = Path.ChangeExtension(scopePath, ".unity").Replace('\\', '/');
            foreach (string path in new[] { scopePath, scenePath })
            {
                if (AssetDatabase.GetMainAssetTypeAtPath(path) != null)
                {
                    throw new InvalidOperationException($"'{path}' already exists.");
                }
            }

            SceneTemplateAsset template = AssetDatabase.LoadAssetAtPath<SceneTemplateAsset>(TemplatePath);
            if (template == null || template.templateScene == null)
            {
                throw new InvalidOperationException($"The scene template '{TemplatePath}' is missing.");
            }

            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(template.templateScene), scenePath))
            {
                throw new InvalidOperationException($"Could not create the scene '{scenePath}'.");
            }

            ScopeDefinition definition = ScriptableObject.CreateInstance<ScopeDefinition>();
            definition.SetScene(authoring.CreateAndRegister(scenePath));
            AssetDatabase.CreateAsset(definition, scopePath);
            return definition;
        }

        private static string GetSelectedFolder()
        {
            string path = Selection.activeObject != null ? AssetDatabase.GetAssetPath(Selection.activeObject) : null;
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets", StringComparison.Ordinal))
            {
                return "Assets";
            }

            return AssetDatabase.IsValidFolder(path) ? path : Path.GetDirectoryName(path)?.Replace('\\', '/');
        }
    }
}
