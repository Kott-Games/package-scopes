using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Kott.Scopes
{
    /// <summary>
    ///     Editor support for one <see cref="SceneReference" /> type: drawing it in the Scope inspector, finding its
    ///     scene asset, validating its registration and creating it for a new scene. Implementations are found
    ///     through <see cref="TypeCache" /> and need a parameterless constructor.
    /// </summary>
    internal abstract class SceneReferenceAuthoring
    {
        private static List<SceneReferenceAuthoring> _all;

        /// <summary>The handled <see cref="SceneReference" /> type.</summary>
        public abstract Type ReferenceType { get; }

        /// <summary>Name of the loader in menus, e.g. "Build Profile".</summary>
        public abstract string DisplayName { get; }

        /// <summary>Sort order in menus.</summary>
        public virtual int Order => 100;

        /// <summary>All available implementations, sorted by <see cref="Order" />.</summary>
        public static IReadOnlyList<SceneReferenceAuthoring> All
        {
            get
            {
                if (_all == null)
                {
                    _all = new List<SceneReferenceAuthoring>();
                    foreach (Type type in TypeCache.GetTypesDerivedFrom<SceneReferenceAuthoring>())
                    {
                        if (!type.IsAbstract)
                        {
                            _all.Add((SceneReferenceAuthoring)Activator.CreateInstance(type, true));
                        }
                    }

                    _all.Sort((a, b) => a.Order.CompareTo(b.Order));
                }

                return _all;
            }
        }

        /// <summary>The implementation for <paramref name="referenceType" />, or <c>null</c> (custom reference types).</summary>
        public static SceneReferenceAuthoring For(Type referenceType)
        {
            foreach (SceneReferenceAuthoring authoring in All)
            {
                if (authoring.ReferenceType == referenceType)
                {
                    return authoring;
                }
            }

            return null;
        }

        /// <summary>Path of the referenced scene asset, or <c>null</c> if it does not exist.</summary>
        public abstract string GetScenePath(SceneReference reference);

        /// <summary>
        ///     Why the existing scene at <paramref name="scenePath" /> cannot be loaded through
        ///     <paramref name="reference" />, or <c>null</c> if it can.
        /// </summary>
        public abstract string GetRegistrationProblem(SceneReference reference, string scenePath);

        /// <summary>Registers the scene at <paramref name="scenePath" /> with the loader and returns a reference to it.</summary>
        public abstract SceneReference CreateAndRegister(string scenePath);

        /// <summary>Draws the reference's scene field in a single line.</summary>
        public abstract void DrawField(Rect position, SerializedProperty property);

        /// <summary>Whether <paramref name="path" /> is an existing scene asset.</summary>
        protected static bool IsSceneAsset(string path)
        {
            return !string.IsNullOrEmpty(path) && AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(SceneAsset);
        }
    }

    /// <summary>Authoring for <see cref="BuildSceneReference" />: the scene list of the build profile.</summary>
    internal sealed class BuildSceneReferenceAuthoring : SceneReferenceAuthoring
    {
        public override Type ReferenceType => typeof(BuildSceneReference);
        public override string DisplayName => "Build Profile";
        public override int Order => 0;

        public override string GetScenePath(SceneReference reference)
        {
            BuildSceneReference buildReference = (BuildSceneReference)reference;

            // the GUID survives moves; the stored path may be outdated
            string path = string.IsNullOrEmpty(buildReference.SceneGuid)
                ? null
                : AssetDatabase.GUIDToAssetPath(buildReference.SceneGuid);
            if (!IsSceneAsset(path))
            {
                path = buildReference.ScenePath;
            }

            return IsSceneAsset(path) ? path : null;
        }

        public override string GetRegistrationProblem(SceneReference reference, string scenePath)
        {
            BuildSceneReference buildReference = (BuildSceneReference)reference;
            if (buildReference.ScenePath != scenePath)
            {
                return $"The scene was moved to '{scenePath}', but the scope still loads it from " +
                       $"'{buildReference.ScenePath}'. Select the scope in the inspector to update the reference.";
            }

            foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
            {
                if (buildScene.path == scenePath)
                {
                    return buildScene.enabled
                        ? null
                        : $"The scene '{scenePath}' is disabled in the scene list of the build profile.";
                }
            }

            return $"The scene '{scenePath}' is not in the scene list of the build profile. Add it there, or load " +
                   "it through Addressables.";
        }

        public override SceneReference CreateAndRegister(string scenePath)
        {
            string guid = AssetDatabase.AssetPathToGUID(scenePath);
            if (!IsSceneAsset(scenePath) || string.IsNullOrEmpty(guid))
            {
                throw new ArgumentException($"There is no scene at '{scenePath}'.", nameof(scenePath));
            }

            List<EditorBuildSettingsScene> scenes = new(EditorBuildSettings.scenes);
            int index = scenes.FindIndex(scene => scene.path == scenePath);
            if (index < 0)
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            }
            else
            {
                scenes[index].enabled = true;
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            return new BuildSceneReference(scenePath, guid);
        }

        public override void DrawField(Rect position, SerializedProperty property)
        {
            BuildSceneReferenceDrawer.Draw(position, property, GUIContent.none);
        }
    }
}
