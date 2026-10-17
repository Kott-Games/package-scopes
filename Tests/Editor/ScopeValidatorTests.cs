using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Kott.Scopes.Tests
{
    [Serializable]
    internal class FooConsumer : Manager
    {
        public Singleton<IFooService> Foo;
    }

    /// <summary>Inherits the IFooService requirement and adds a private one.</summary>
    [Serializable]
    internal class DerivedConsumer : FooConsumer
    {
        private Singleton<IBarService> _bar;

        public bool HasBar => _bar.IsAvailable;
    }

    [Serializable]
    [RequiresSingleton(typeof(IBarService))]
    internal class AttributeConsumer : Manager
    { }

    [Serializable]
    [RequiresSingleton(typeof(string))]
    internal class NonSingletonConsumer : Manager
    { }

    [Serializable]
    internal class PlainInterfaceConsumer : Manager
    {
        public Singleton<IPlainService> Plain;
    }

    public class ScopeValidatorTests
    {
        private const string TemplateScenePath = "Packages/com.kott.scopes/Runtime/ScopeScene.unity";
        private const string TempFolder = "Assets/KottScopesTestsTemp";
        private const string ScopeScriptGuid = "1384fbd774101c841b7b09226fa6ddf0"; // Runtime/Scope.cs.meta

        private readonly List<Object> _createdObjects = new();
        private EditorBuildSettingsScene[] _buildScenes;

        [SetUp]
        public void SetUp()
        {
            _buildScenes = EditorBuildSettings.scenes;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object createdObject in _createdObjects)
            {
                Object.DestroyImmediate(createdObject);
            }

            _createdObjects.Clear();
            EditorBuildSettings.scenes = _buildScenes;
            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }
        }

        [Test]
        public void GetRequiredSingletons_DerivedManager_IncludesInheritedAndPrivateFields()
        {
            Assert.That(ScopeValidator.GetRequiredSingletons(typeof(DerivedConsumer)),
                Is.EquivalentTo(new[] { typeof(IFooService), typeof(IBarService) }));
        }

        [Test]
        public void GetRequiredSingletons_Attribute_Included()
        {
            Assert.That(ScopeValidator.GetRequiredSingletons(typeof(AttributeConsumer)),
                Is.EqualTo(new[] { typeof(IBarService) }));
        }

        [Test]
        public void GetProvidedSingletons_ContractManager_ConcreteTypeAndAllContracts()
        {
            Assert.That(ScopeValidator.GetProvidedSingletons(typeof(FooBarManager)),
                Is.EquivalentTo(new[] { typeof(FooBarManager), typeof(IFooService), typeof(IBarService) }));
        }

        [Test]
        public void GetProvidedSingletons_NonSingletonManager_Empty()
        {
            Assert.That(ScopeValidator.GetProvidedSingletons(typeof(RecordingManager)), Is.Empty);
        }

        [Test]
        public void Validate_RequirementProvidedBySameScope_NoIssues()
        {
            Scope scope = CreateScope("S", new FooConsumer(), new FooManager());

            ScopeValidationResult result = ScopeValidator.Validate(scope);

            Assert.That(result.Issues, Is.Empty);
            SingletonRequirement requirement = result.Requirements.Single();
            Assert.That(requirement.Type, Is.EqualTo(typeof(IFooService)));
            Assert.That(requirement.ProvidedBy, Is.SameAs(scope));
            Assert.That(requirement.Provider, Is.EqualTo(typeof(FooManager)));
            Assert.That(requirement.RequiredBy, Is.EqualTo(new[] { typeof(FooConsumer) }));
        }

        [Test]
        public void Validate_RequirementProvidedByDeclaredGrandparent_ResolvedToGrandparent()
        {
            Scope grandparent = CreateScope("Core", new FooManager());
            Scope parent = CreateScope("Game", grandparent);
            Scope scope = CreateScope("Hub", parent, new FooConsumer());

            ScopeValidationResult result = ScopeValidator.Validate(scope);

            Assert.That(result.Issues, Is.Empty);
            Assert.That(result.Requirements.Single().ProvidedBy, Is.SameAs(grandparent));
        }

        [Test]
        public void Validate_RequirementNotProvided_WarnsNamingTypeAndManager()
        {
            Scope scope = CreateScope("Hub", new FooConsumer());

            ScopeIssue issue = ScopeValidator.Validate(scope).Issues.Single();

            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.MissingSingleton));
            Assert.That(issue.Severity, Is.EqualTo(ScopeIssueSeverity.Warning));
            Assert.That(issue.Message,
                Does.StartWith("'IFooService' (required by FooConsumer) is not provided by 'Hub'."));
            Assert.That(issue.Message, Does.Contain("Declare a parent scope"));
        }

        [Test]
        public void Validate_RequirementNotProvidedByDeclaredParents_MessageMentionsParents()
        {
            Scope parent = CreateScope("Game", new BarManager());
            Scope scope = CreateScope("Hub", parent, new FooConsumer());

            ScopeIssue issue = ScopeValidator.Validate(scope).Issues.Single();

            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.MissingSingleton));
            Assert.That(issue.Message, Does.Contain("is not provided by 'Hub' or its declared parents."));
            Assert.That(ScopeValidator.Validate(scope).Requirements.Single().IsProvided, Is.False);
        }

        [Test]
        public void Validate_RequiresSingletonAttribute_IsChecked()
        {
            Scope scope = CreateScope("S", new AttributeConsumer());

            ScopeIssue issue = ScopeValidator.Validate(scope).Issues.Single();

            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.MissingSingleton));
            Assert.That(issue.Message, Does.StartWith("'IBarService' (required by AttributeConsumer)"));
        }

        [Test]
        public void Validate_RequirementOfNonSingletonType_Error()
        {
            Scope scope = CreateScope("S", new NonSingletonConsumer());

            ScopeIssue issue = ScopeValidator.Validate(scope).Issues.Single();

            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.InvalidRequirement));
            Assert.That(issue.Severity, Is.EqualTo(ScopeIssueSeverity.Error));
            Assert.That(issue.Message, Is.EqualTo(
                "NonSingletonConsumer requires 'String', which is not a singleton type. Implement ISingleton or a " +
                "singleton contract."));
        }

        [Test]
        public void Validate_RequirementOfNonContractInterface_Error()
        {
            Scope scope = CreateScope("S", new PlainInterfaceConsumer(), new PlainManager());

            ScopeIssue issue = ScopeValidator.Validate(scope).Issues.Single();

            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.InvalidRequirement));
            Assert.That(issue.Message,
                Does.Contain("'IPlainService', which is an interface but not a singleton contract"));
        }

        [Test]
        public void Validate_ContractProvidedTwiceInScope_Error()
        {
            Scope scope = CreateScope("S", new FooManager(), new FooBarManager());

            ScopeIssue issue = ScopeValidator.Validate(scope).Issues.Single();

            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.DuplicateSingleton));
            Assert.That(issue.Severity, Is.EqualTo(ScopeIssueSeverity.Error));
            Assert.That(issue.Message,
                Does.StartWith("'IFooService' is provided twice: by FooManager in 'S' and by FooBarManager in 'S'."));
        }

        [Test]
        public void Validate_ContractProvidedByScopeAndDeclaredParent_Error()
        {
            Scope parent = CreateScope("Game", new FooManager());
            Scope scope = CreateScope("Hub", parent, new FooBarManager());

            ScopeIssue issue = ScopeValidator.Validate(scope).Issues.Single();

            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.DuplicateSingleton));
            Assert.That(issue.Message, Does.Contain("by FooManager in 'Game' and by FooBarManager in 'Hub'"));
        }

        [Test]
        public void Validate_DuplicateAmongDeclaredParentsOnly_NotReportedForChild()
        {
            Scope parent = CreateScope("Game", new FooManager(), new FooBarManager());
            Scope scope = CreateScope("Hub", parent, new RecordingManager());

            Assert.That(ScopeValidator.Validate(scope).Issues, Is.Empty);
            Assert.That(ScopeValidator.Validate(parent).Issues.Single().Kind,
                Is.EqualTo(ScopeIssueKind.DuplicateSingleton));
        }

        [Test]
        public void Validate_SameNonSingletonManagerTwice_NoIssues()
        {
            Scope scope = CreateScope("S", new RecordingManager(), new RecordingManager());

            Assert.That(ScopeValidator.Validate(scope).Issues, Is.Empty);
        }

        [Test]
        public void Validate_DeclaredParentIsSelf_CycleError()
        {
            Scope scope = CreateScope("A");
            scope.SetDeclaredParent(scope);

            ScopeIssue issue = ScopeValidator.Validate(scope).Issues.Single();

            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.DeclaredParentCycle));
            Assert.That(issue.Message, Is.EqualTo("The declared parents form a cycle: A → A."));
        }

        [Test]
        public void Validate_DeclaredParentCycle_ErrorNamesChain()
        {
            Scope a = CreateScope("A");
            Scope b = CreateScope("B", a);
            a.SetDeclaredParent(b);

            ScopeIssue issue = ScopeValidator.Validate(a).Issues.Single();

            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.DeclaredParentCycle));
            Assert.That(issue.Message, Is.EqualTo("The declared parents form a cycle: A → B → A."));
        }

        [Test]
        public void Validate_EmptyManagerSlot_Error()
        {
            Scope scope = CreateScope("S", new RecordingManager(), null);

            ScopeIssue issue = ScopeValidator.Validate(scope).Issues.Single();

            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.EmptyManagerSlot));
            Assert.That(issue.Message, Does.StartWith("Manager 2 is empty."));
        }

        [Test]
        public void Validate_UnresolvedManagerType_ErrorNamesStoredType()
        {
            Scope scope = ImportScopeWithMissingManagerType();

            ScopeValidationResult result = ScopeValidator.Validate(scope);

            Assert.That(SerializationUtility.HasManagedReferencesWithMissingTypes(scope), Is.True,
                "Unity did not register the missing type; the test asset is wrong, not the validator.");
            Assert.That(scope.Managers[0], Is.Null);
            Assert.That(scope.Managers[1], Is.InstanceOf<RecordingManager>());
            ScopeIssue issue = result.Issues.Single();
            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.UnresolvedManagerType));
            Assert.That(issue.Message, Does.StartWith(
                "Manager 1 has the unresolved type 'Kott.Scopes.Tests.DeletedManager, Kott.Scopes.Tests.Editor'."));
        }

        [Test]
        public void Validate_SceneDoesNotExist_SceneNotFound()
        {
            Scope scope = CreateScope("S");
            scope.SetScene(new BuildSceneReference("Assets/DoesNotExist.unity"));

            ScopeIssue issue = ScopeValidator.Validate(scope).Issues.Single();

            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.SceneNotFound));
            Assert.That(issue.Message, Does.StartWith("The scene 'DoesNotExist' does not exist."));
        }

        [Test]
        public void Validate_SceneNotInBuildProfile_SceneNotRegistered()
        {
            EditorBuildSettings.scenes = Array.Empty<EditorBuildSettingsScene>();
            Scope scope = CreateScope("S");
            scope.SetScene(new BuildSceneReference(TemplateScenePath));

            ScopeIssue issue = ScopeValidator.Validate(scope, false).Issues.Single();

            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.SceneNotRegistered));
            Assert.That(issue.Message,
                Does.StartWith($"The scene '{TemplateScenePath}' is not in the scene list of the build profile."));
            Assert.That(issue.Context, Is.SameAs(AssetDatabase.LoadAssetAtPath<SceneAsset>(TemplateScenePath)));
        }

        [Test]
        public void Validate_SceneDisabledInBuildProfile_SceneNotRegistered()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(TemplateScenePath, false) };
            Scope scope = CreateScope("S");
            scope.SetScene(new BuildSceneReference(TemplateScenePath));

            ScopeIssue issue = ScopeValidator.Validate(scope, false).Issues.Single();

            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.SceneNotRegistered));
            Assert.That(issue.Message, Does.Contain("is disabled in the scene list"));
        }

        [Test]
        public void Validate_InspectRegisteredTemplateScene_NoIssuesAndSceneClosedAgain()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(TemplateScenePath, true) };
            Scope scope = CreateScope("S");
            scope.SetScene(new BuildSceneReference(TemplateScenePath));

            ScopeValidationResult result = ScopeValidator.Validate(scope, true);

            Assert.That(result.Issues, Is.Empty);
            Assert.That(result.SceneInspected, Is.True);
            Assert.That(SceneManager.GetSceneByPath(TemplateScenePath).IsValid(), Is.False);
        }

        [Test]
        public void ValidateSceneContent_OneSceneRoot_NoIssues()
        {
            GameObject root = CreateGameObject("Root");
            root.AddComponent<SceneRoot>();
            AddLink(CreateGameObject("A", root), "a");
            AddLink(CreateGameObject("B", root), "b");

            Assert.That(ScopeValidator.ValidateSceneContent(CreateScope("S"), "Level", new[] { root }), Is.Empty);
        }

        [Test]
        public void ValidateSceneContent_NoSceneRoot_Error()
        {
            GameObject root = CreateGameObject("Root");

            ScopeIssue issue = ScopeValidator.ValidateSceneContent(CreateScope("S"), "Level", new[] { root }).Single();

            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.SceneWithoutSceneRoot));
            Assert.That(issue.Severity, Is.EqualTo(ScopeIssueSeverity.Error));
            Assert.That(issue.Message, Does.StartWith("The scene 'Level' has no SceneRoot"));
        }

        [Test]
        public void ValidateSceneContent_TwoSceneRoots_ErrorNamesBoth()
        {
            GameObject first = CreateGameObject("First");
            first.AddComponent<SceneRoot>();
            GameObject second = CreateGameObject("Second");
            second.AddComponent<SceneRoot>();

            ScopeIssue issue = ScopeValidator
                .ValidateSceneContent(CreateScope("S"), "Level", new[] { first, second }).Single();

            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.MultipleSceneRoots));
            Assert.That(issue.Message, Does.StartWith("The scene 'Level' has 2 SceneRoots ('First', 'Second')"));
        }

        [Test]
        public void ValidateSceneContent_DuplicateLinkKeyInChildren_ErrorNamesKeyAndObjects()
        {
            GameObject root = CreateGameObject("Root");
            root.AddComponent<SceneRoot>();
            AddLink(CreateGameObject("A", root), "door");
            AddLink(CreateGameObject("B", CreateGameObject("Nested", root)), "door");

            ScopeIssue issue = ScopeValidator.ValidateSceneContent(CreateScope("S"), "Level", new[] { root }).Single();

            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.DuplicateSceneLinkKey));
            Assert.That(issue.Message,
                Does.StartWith("Duplicate SceneLink key 'door' in scene 'Level': on 'A' and 'B'."));
        }

        [Test]
        public void ValidateSceneContent_EmptyLinkKey_Warning()
        {
            GameObject root = CreateGameObject("Root");
            root.AddComponent<SceneRoot>();
            AddLink(CreateGameObject("A", root), "");

            ScopeIssue issue = ScopeValidator.ValidateSceneContent(CreateScope("S"), "Level", new[] { root }).Single();

            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.EmptySceneLinkKey));
            Assert.That(issue.Severity, Is.EqualTo(ScopeIssueSeverity.Warning));
        }

        private Scope CreateScope(string name, params Manager[] managers)
        {
            return CreateScope(name, null, managers);
        }

        private Scope CreateScope(string name, Scope declaredParent, params Manager[] managers)
        {
            Scope scope = ScriptableObject.CreateInstance<Scope>();
            scope.name = name;
            scope.SetDeclaredParent(declaredParent);
            scope.SetManagers(managers);
            _createdObjects.Add(scope);
            return scope;
        }

        private GameObject CreateGameObject(string name, GameObject parent = null)
        {
            GameObject gameObject = new(name);
            if (parent != null)
            {
                gameObject.transform.SetParent(parent.transform);
            }
            else
            {
                _createdObjects.Add(gameObject);
            }

            return gameObject;
        }

        private static void AddLink(GameObject gameObject, string key)
        {
            SceneLink link = gameObject.AddComponent<SceneLink>();
            using SerializedObject serializedLink = new(link);
            serializedLink.FindProperty("_key").stringValue = key;
            serializedLink.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        ///     Imports a Scope asset whose first manager has a type that does not exist, as after renaming a class
        ///     without [MovedFrom].
        /// </summary>
        private static Scope ImportScopeWithMissingManagerType()
        {
            string yaml = $@"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {ScopeScriptGuid}, type: 3}}
  m_Name: Broken
  m_EditorClassIdentifier:
  _scene:
    rid: -2
  _declaredParent: {{fileID: 0}}
  _managers:
  - rid: 1000
  - rid: 1001
  references:
    version: 2
    RefIds:
    - rid: -2
      type: {{class: , ns: , asm: }}
    - rid: 1000
      type: {{class: DeletedManager, ns: Kott.Scopes.Tests, asm: Kott.Scopes.Tests.Editor}}
      data:
        Value: 1
    - rid: 1001
      type: {{class: RecordingManager, ns: Kott.Scopes.Tests, asm: Kott.Scopes.Tests.Editor}}
      data:
        Id: B
        InitializeFrames: 0
        ThrowOnInitialize: 0
        ThrowOnUninitialize: 0
";
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(TempFolder));
            }

            string path = $"{TempFolder}/Broken.asset";
            File.WriteAllText(path, yaml);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Scope>(path);
        }
    }
}
