using UnityEditor;
using UnityEngine;

namespace Kott.Scopes
{
    /// <summary>Scene asset picker for <see cref="BuildSceneReference" />; follows the scene when it is moved.</summary>
    [CustomPropertyDrawer(typeof(BuildSceneReference))]
    internal sealed class BuildSceneReferenceDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty pathProperty = property.FindPropertyRelative("_scenePath");
            SerializedProperty guidProperty = property.FindPropertyRelative("_sceneGuid");

            // the GUID survives moves and renames; the path is what the runtime loads
            string path = AssetDatabase.GUIDToAssetPath(guidProperty.stringValue);
            if (string.IsNullOrEmpty(path))
            {
                path = pathProperty.stringValue;
            }

            SceneAsset current = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            SceneAsset selected = (SceneAsset)EditorGUI.ObjectField(position, label, current, typeof(SceneAsset), false);
            bool changed = EditorGUI.EndChangeCheck();
            EditorGUI.EndProperty();

            if (!changed && (current == null || pathProperty.stringValue == path))
            {
                return;
            }

            string newPath = selected != null ? AssetDatabase.GetAssetPath(selected) : string.Empty;
            pathProperty.stringValue = newPath;
            guidProperty.stringValue = selected != null ? AssetDatabase.AssetPathToGUID(newPath) : string.Empty;
        }
    }
}
