using System;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Kott.Scopes
{
    /// <summary>Authoring for <see cref="AddressableSceneReference" />: scenes loaded through Addressables.</summary>
    internal sealed class AddressableSceneReferenceAuthoring : SceneReferenceAuthoring
    {
        private const string SceneField = "_scene";

        public override Type ReferenceType => typeof(AddressableSceneReference);
        public override string DisplayName => "Addressables";
        public override int Order => 10;

        [MenuItem("Assets/Create/Scopes/Scope with Scene (Addressables)", priority = 2)]
        private static void CreateWithAddressableScene()
        {
            ScopeCreation.CreateInteractive(For(typeof(AddressableSceneReference)));
        }

        public override string GetScenePath(SceneReference reference)
        {
            string guid = ((AddressableSceneReference)reference).Scene?.AssetGUID;
            string path = string.IsNullOrEmpty(guid) ? null : AssetDatabase.GUIDToAssetPath(guid);
            return IsSceneAsset(path) ? path : null;
        }

        public override string GetRegistrationProblem(SceneReference reference, string scenePath)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                return "Addressables is not set up in this project (Window > Asset Management > Addressables > " +
                       "Groups > Create Addressables Settings).";
            }

            string guid = ((AddressableSceneReference)reference).Scene.AssetGUID;
            return settings.FindAssetEntry(guid) != null
                ? null
                : $"The scene '{scenePath}' is not addressable. Mark it as Addressable, or load it through the " +
                  "build profile's scene list.";
        }

        public override SceneReference CreateAndRegister(string scenePath)
        {
            string guid = AssetDatabase.AssetPathToGUID(scenePath);
            if (!IsSceneAsset(scenePath) || string.IsNullOrEmpty(guid))
            {
                throw new ArgumentException($"There is no scene at '{scenePath}'.", nameof(scenePath));
            }

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings.FindAssetEntry(guid) == null)
            {
                settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
            }

            return new AddressableSceneReference(new AssetReference(guid));
        }

        public override void DrawField(Rect position, SerializedProperty property)
        {
            EditorGUI.PropertyField(position, property.FindPropertyRelative(SceneField), GUIContent.none);
        }
    }

    /// <summary>Draws an <see cref="AddressableSceneReference" /> as its asset reference field in one line.</summary>
    [CustomPropertyDrawer(typeof(AddressableSceneReference))]
    internal sealed class AddressableSceneReferenceDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.PropertyField(position, property.FindPropertyRelative("_scene"), label);
        }
    }
}
