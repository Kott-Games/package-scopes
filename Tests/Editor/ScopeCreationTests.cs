using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Kott.Scopes.Tests
{
    public class ScopeCreationTests
    {
        private const string Folder = "Assets/KottScopesCreationTemp";

        private EditorBuildSettingsScene[] _buildScenes;

        [SetUp]
        public void SetUp()
        {
            _buildScenes = EditorBuildSettings.scenes;
            AssetDatabase.CreateFolder("Assets", "KottScopesCreationTemp");
        }

        [TearDown]
        public void TearDown()
        {
            EditorBuildSettings.scenes = _buildScenes;
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void CreateWithScene_BuildProfile_CreatesScopeAndRegisteredSceneWithSceneRoot()
        {
            string scenePath = $"{Folder}/Hub.unity";

            Scope scope = ScopeCreation.CreateWithScene($"{Folder}/Hub.asset", BuildAuthoring());

            Assert.That(AssetDatabase.LoadAssetAtPath<Scope>($"{Folder}/Hub.asset"), Is.SameAs(scope));
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath), Is.Not.Null);

            BuildSceneReference reference = scope.Scene as BuildSceneReference;
            Assert.That(reference, Is.Not.Null);
            Assert.That(reference.ScenePath, Is.EqualTo(scenePath));
            Assert.That(reference.SceneGuid, Is.EqualTo(AssetDatabase.AssetPathToGUID(scenePath)));
            Assert.That(EditorBuildSettings.scenes.Count(scene => scene.path == scenePath && scene.enabled),
                Is.EqualTo(1));

            // the copied template scene has a SceneRoot
            ScopeValidationResult result = ScopeValidator.Validate(scope, true);
            Assert.That(result.SceneInspected, Is.True);
            Assert.That(result.Issues, Is.Empty, result.ToString());
        }

        [Test]
        public void CreateWithScene_SceneAlreadyExists_ThrowsAndCreatesNoScope()
        {
            AssetDatabase.CopyAsset("Packages/com.kott.scopes/Runtime/ScopeScene.unity", $"{Folder}/Hub.unity");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                ScopeCreation.CreateWithScene($"{Folder}/Hub.asset", BuildAuthoring()));

            Assert.That(exception.Message, Is.EqualTo($"'{Folder}/Hub.unity' already exists."));
            Assert.That(AssetDatabase.LoadAssetAtPath<Scope>($"{Folder}/Hub.asset"), Is.Null);
        }

        [Test]
        public void CreateWithScene_SceneAlreadyInBuildProfileButDisabled_EnablesIt()
        {
            string scenePath = $"{Folder}/Hub.unity";
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, false) };

            ScopeCreation.CreateWithScene($"{Folder}/Hub.asset", BuildAuthoring());

            Assert.That(EditorBuildSettings.scenes.Single(scene => scene.path == scenePath).enabled, Is.True);
        }

        private static SceneReferenceAuthoring BuildAuthoring()
        {
            return SceneReferenceAuthoring.For(typeof(BuildSceneReference));
        }
    }
}
