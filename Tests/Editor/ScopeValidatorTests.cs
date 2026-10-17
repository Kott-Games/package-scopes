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
    /// <summary>Inherits the IFooService injection and adds a private one.</summary>
    [Serializable]
    internal class DerivedConsumer : FooConsumer
    {
        [Inject]
        private IBarService _bar;

        public bool HasBar => _bar != null;
    }

    [Serializable]
    internal class ConcreteTypeConsumer : Manager
    {
        [Inject]
        public FooManager Foo;
    }

    [Serializable]
    internal class InvalidSceneFieldConsumer : Manager
    {
        [InjectScene("count")]
        public int Count;

        [InjectScene("")]
        public GameObject NoKey;
    }

    public class ScopeValidatorTests
    {
        private const string TemplateScenePath = "Packages/com.kott.scopes/Runtime/ScopeScene.unity";
        private const string TempFolder = "Assets/KottScopesTestsTemp";
        // Runtime/ScopeDefinition.cs.meta
        private const string DefinitionScriptGuid = "1384fbd774101c841b7b09226fa6ddf0";

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
        public void GetInjectedContracts_DerivedManager_IncludesInheritedAndPrivateFields()
        {
            Assert.That(ScopeValidator.GetInjectedContracts(typeof(DerivedConsumer)),
                Is.EquivalentTo(new[] { typeof(IFooService), typeof(IBarService) }));
        }

        [Test]
        public void GetProvidedContracts_ManagerWithTwoContracts_ReturnsBoth()
        {
            Assert.That(ScopeValidator.GetProvidedContracts(typeof(FooBarManager)),
                Is.EquivalentTo(new[] { typeof(IFooService), typeof(IBarService) }));
        }

        [Test]
        public void GetProvidedContracts_ExtendedContract_IncludesBaseContract()
        {
            Assert.That(ScopeValidator.GetProvidedContracts(typeof(ExtendedFooManager)),
                Is.EquivalentTo(new[] { typeof(IFooService), typeof(IExtendedFooService) }));
        }

        [Test]
        public void GetProvidedContracts_ManagerWithoutContracts_Empty()
        {
            Assert.That(ScopeValidator.GetProvidedContracts(typeof(RecordingManager)), Is.Empty);
            Assert.That(ScopeValidator.GetProvidedContracts(typeof(PlainManager)), Is.Empty);
        }

        [Test]
        public void GetSceneKeys_SceneConsumer_ReturnsKeys()
        {
            Assert.That(ScopeValidator.GetSceneKeys(typeof(SceneConsumer)), Is.EqualTo(new[] { "door", "spawn" }));
        }

        [Test]
        public void Validate_ContractProvidedBySameScope_NoIssues()
        {
            ScopeDefinition definition = CreateDefinition("S", new FooConsumer(), new FooManager());

            ScopeValidationResult result = ScopeValidator.Validate(definition);

            Assert.That(result.Issues, Is.Empty);
            ContractRequirement requirement = result.Requirements.Single();
            Assert.That(requirement.Type, Is.EqualTo(typeof(IFooService)));
            Assert.That(requirement.ProvidedBy, Is.SameAs(definition));
            Assert.That(requirement.Provider, Is.EqualTo(typeof(FooManager)));
            Assert.That(requirement.RequiredBy, Is.EqualTo(new[] { typeof(FooConsumer) }));
            Assert.That(requirement.IsOptional, Is.False);
        }

        [Test]
        public void Validate_ContractProvidedByDeclaredGrandparent_ResolvedToGrandparent()
        {
            ScopeDefinition grandparent = CreateDefinition("Core", new FooManager());
            ScopeDefinition parent = CreateDefinition("Game", grandparent);
            ScopeDefinition definition = CreateDefinition("Hub", parent, new FooConsumer());

            ScopeValidationResult result = ScopeValidator.Validate(definition);

            Assert.That(result.Issues, Is.Empty);
            Assert.That(result.Requirements.Single().ProvidedBy, Is.SameAs(grandparent));
        }

        [Test]
        public void Validate_ContractNotProvidedWithoutDeclaredParent_WarnsNamingTypeAndManager()
        {
            ScopeDefinition definition = CreateDefinition("Hub", new FooConsumer());

            ScopeIssue issue = ScopeValidator.Validate(definition).Issues.Single();

            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.MissingContract));
            Assert.That(issue.Severity, Is.EqualTo(ScopeIssueSeverity.Warning));
            Assert.That(issue.Message,
                Does.StartWith("'IFooService' (injected by FooConsumer) is not provided by 'Hub'."));
            Assert.That(issue.Message, Does.Contain("Declare a parent scope"));
        }

        [Test]
        public void Validate_ContractNotProvidedByDeclaredParents_Error()
        {
            ScopeDefinition parent = CreateDefinition("Game", new BarManager());
            ScopeDefinition definition = CreateDefinition("Hub", parent, new FooConsumer());

            ScopeValidationResult result = ScopeValidator.Validate(definition);

            ScopeIssue issue = result.Issues.Single();
            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.MissingContract));
            Assert.That(issue.Severity, Is.EqualTo(ScopeIssueSeverity.Error));
            Assert.That(issue.Message, Does.Contain("is not provided by 'Hub' or its declared parents."));
            Assert.That(result.Requirements.Single().IsProvided, Is.False);
        }

        [Test]
        public void Validate_OptionalContractNotProvided_NoIssue()
        {
            ScopeDefinition definition = CreateDefinition("S", new OptionalFooConsumer());

            ScopeValidationResult result = ScopeValidator.Validate(definition);

            Assert.That(result.Issues, Is.Empty);
            Assert.That(result.Requirements.Single().IsOptional, Is.True);
        }

        [Test]
        public void Validate_InjectedNonContractInterface_Error()
        {
            ScopeDefinition definition = CreateDefinition("S", new PlainInterfaceConsumer(), new PlainManager());

            ScopeIssue issue = ScopeValidator.Validate(definition).Issues.Single();

            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.InvalidInjection));
            Assert.That(issue.Severity, Is.EqualTo(ScopeIssueSeverity.Error));
            Assert.That(issue.Message, Is.EqualTo(
                "[Inject] PlainInterfaceConsumer.Plain: 'IPlainService' is not a manager contract. Declare it as " +
                "'IPlainService : IManagerContract'."));
        }

        [Test]
        public void Validate_InjectedManagerClass_Error()
        {
            ScopeDefinition definition = CreateDefinition("S", new ConcreteTypeConsumer(), new FooManager());

            ScopeIssue issue = ScopeValidator.Validate(definition).Issues.Single();

            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.InvalidInjection));
            Assert.That(issue.Message, Does.Contain("Inject an interface extending IManagerContract"));
        }

        [Test]
        public void Validate_ContractProvidedTwiceInScope_Error()
        {
            ScopeDefinition definition = CreateDefinition("S", new FooManager(), new FooBarManager());

            ScopeIssue issue = ScopeValidator.Validate(definition).Issues.Single();

            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.DuplicateContract));
            Assert.That(issue.Severity, Is.EqualTo(ScopeIssueSeverity.Error));
            Assert.That(issue.Message,
                Does.StartWith("'IFooService' is provided twice: by FooManager in 'S' and by FooBarManager in 'S'."));
        }

        [Test]
        public void Validate_ContractProvidedByScopeAndDeclaredParent_Error()
        {
            ScopeDefinition parent = CreateDefinition("Game", new FooManager());
            ScopeDefinition definition = CreateDefinition("Hub", parent, new FooBarManager());

            ScopeIssue issue = ScopeValidator.Validate(definition).Issues.Single();

            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.DuplicateContract));
            Assert.That(issue.Message, Does.Contain("by FooManager in 'Game' and by FooBarManager in 'Hub'"));
        }

        [Test]
        public void Validate_DuplicateAmongDeclaredParentsOnly_NotReportedForChild()
        {
            ScopeDefinition parent = CreateDefinition("Game", new FooManager(), new FooBarManager());
            ScopeDefinition definition = CreateDefinition("Hub", parent, new RecordingManager());

            Assert.That(ScopeValidator.Validate(definition).Issues, Is.Empty);
            Assert.That(ScopeValidator.Validate(parent).Issues.Single().Kind,
                Is.EqualTo(ScopeIssueKind.DuplicateContract));
        }

        [Test]
        public void Validate_SameManagerWithoutContractsTwice_NoIssues()
        {
            ScopeDefinition definition = CreateDefinition("S", new RecordingManager(), new RecordingManager());

            Assert.That(ScopeValidator.Validate(definition).Issues, Is.Empty);
        }

        [Test]
        public void Validate_ConsumerListedBeforeProvider_OrderInitializesProviderFirst()
        {
            ScopeDefinition definition =
                CreateDefinition("S", new FooConsumer(), new RecordingManager(), new FooManager());

            ScopeValidationResult result = ScopeValidator.Validate(definition);

            Assert.That(result.Issues, Is.Empty);
            Assert.That(result.InitializationOrder, Is.EqualTo(new[] { 1, 2, 0 }));
        }

        [Test]
        public void Validate_DependencyCycle_ErrorNamesCycle()
        {
            ScopeDefinition definition = CreateDefinition("S", new CycleFoo(), new CycleBar());

            ScopeValidationResult result = ScopeValidator.Validate(definition);

            ScopeIssue issue = result.Issues.Single();
            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.DependencyCycle));
            Assert.That(issue.Message,
                Does.StartWith("The managers inject each other in a cycle: CycleFoo → CycleBar → CycleFoo."));
            Assert.That(result.InitializationOrder, Is.Empty);
        }

        [Test]
        public void Validate_SceneFieldsWithoutScene_ErrorPerField()
        {
            ScopeDefinition definition = CreateDefinition("S", new SceneConsumer());

            IReadOnlyList<ScopeIssue> issues = ScopeValidator.Validate(definition).Issues;

            Assert.That(issues.Select(issue => issue.Kind),
                Is.EqualTo(new[]
                    { ScopeIssueKind.SceneInjectionWithoutScene, ScopeIssueKind.SceneInjectionWithoutScene }));
            Assert.That(issues[0].Message,
                Does.StartWith("SceneConsumer.Door needs the SceneLink 'door', but 'S' has no scene."));
        }

        [Test]
        public void Validate_OptionalSceneFieldWithoutScene_NoIssue()
        {
            ScopeDefinition definition = CreateDefinition("S", new OptionalSceneConsumer());

            Assert.That(ScopeValidator.Validate(definition).Issues, Is.Empty);
        }

        [Test]
        public void Validate_InvalidSceneFields_ErrorPerField()
        {
            ScopeDefinition definition = CreateDefinition("S", new InvalidSceneFieldConsumer());

            IReadOnlyList<ScopeIssue> issues = ScopeValidator.Validate(definition).Issues;

            Assert.That(issues.Select(issue => issue.Kind),
                Is.EqualTo(new[] { ScopeIssueKind.InvalidInjection, ScopeIssueKind.InvalidInjection }));
            Assert.That(issues[0].Message, Does.Contain("'Int32' cannot come from a scene object"));
            Assert.That(issues[1].Message, Does.Contain("the [InjectScene] key is empty"));
        }

        [Test]
        public void Validate_SceneNotRegistered_FixRegistersScene()
        {
            EditorBuildSettings.scenes = Array.Empty<EditorBuildSettingsScene>();
            ScopeDefinition definition = CreateDefinition("S");
            definition.SetScene(new BuildSceneReference(TemplateScenePath));
            ScopeIssue issue = ScopeValidator.Validate(definition, false).Issues.Single();
            Assert.That(issue.CanFix, Is.True);
            Assert.That(issue.FixLabel, Is.EqualTo("Register with Build Profile"));

            issue.Fix();

            Assert.That(EditorBuildSettings.scenes.Select(scene => scene.path),
                Is.EqualTo(new[] { TemplateScenePath }));
            Assert.That(ScopeValidator.Validate(definition, false).Issues, Is.Empty);
        }

        [Test]
        public void ValidateSceneContent_InjectedSceneLinksPresent_NoIssues()
        {
            GameObject root = CreateGameObject("Root");
            root.AddComponent<SceneRoot>();
            AddLink(CreateGameObject("Door", root), "door");
            GameObject spawn = CreateGameObject("Spawn", root);
            spawn.AddComponent<BoxCollider>();
            AddLink(spawn, "spawn");

            Assert.That(ScopeValidator.ValidateSceneContent(CreateDefinition("S", new SceneConsumer()), "Level",
                new[] { root }), Is.Empty);
        }

        [Test]
        public void ValidateSceneContent_InjectedSceneLinkMissing_Error()
        {
            GameObject root = CreateGameObject("Root");
            root.AddComponent<SceneRoot>();
            GameObject spawn = CreateGameObject("Spawn", root);
            spawn.AddComponent<BoxCollider>();
            AddLink(spawn, "spawn");

            ScopeIssue issue = ScopeValidator.ValidateSceneContent(CreateDefinition("S", new SceneConsumer()),
                "Level", new[] { root }).Single();

            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.MissingSceneLink));
            Assert.That(issue.Message,
                Does.StartWith("SceneConsumer.Door needs the SceneLink 'door', which scene 'Level' does not have."));
        }

        [Test]
        public void ValidateSceneContent_InjectedSceneLinkWithoutComponent_Error()
        {
            GameObject root = CreateGameObject("Root");
            root.AddComponent<SceneRoot>();
            AddLink(CreateGameObject("Door", root), "door");
            AddLink(CreateGameObject("Spawn", root), "spawn");

            ScopeIssue issue = ScopeValidator.ValidateSceneContent(CreateDefinition("S", new SceneConsumer()),
                "Level", new[] { root }).Single();

            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.MissingSceneLinkComponent));
            Assert.That(issue.Message,
                Is.EqualTo("SceneConsumer.Spawn needs a BoxCollider on SceneLink 'spawn' ('Spawn' in scene 'Level'), " +
                           "which it does not have."));
        }

        [Test]
        public void Validate_DeclaredParentIsSelf_CycleError()
        {
            ScopeDefinition definition = CreateDefinition("A");
            definition.SetDeclaredParent(definition);

            ScopeIssue issue = ScopeValidator.Validate(definition).Issues.Single();

            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.DeclaredParentCycle));
            Assert.That(issue.Message, Is.EqualTo("The declared parents form a cycle: A → A."));
        }

        [Test]
        public void Validate_DeclaredParentCycle_ErrorNamesChain()
        {
            ScopeDefinition a = CreateDefinition("A");
            ScopeDefinition b = CreateDefinition("B", a);
            a.SetDeclaredParent(b);

            ScopeIssue issue = ScopeValidator.Validate(a).Issues.Single();

            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.DeclaredParentCycle));
            Assert.That(issue.Message, Is.EqualTo("The declared parents form a cycle: A → B → A."));
        }

        [Test]
        public void Validate_EmptyManagerSlot_Error()
        {
            ScopeDefinition definition = CreateDefinition("S", new RecordingManager(), null);

            ScopeIssue issue = ScopeValidator.Validate(definition).Issues.Single();

            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.EmptyManagerSlot));
            Assert.That(issue.Message, Does.StartWith("Manager 2 is empty."));
        }

        [Test]
        public void Validate_UnresolvedManagerType_ErrorNamesStoredType()
        {
            ScopeDefinition definition = ImportScopeWithMissingManagerType();

            ScopeValidationResult result = ScopeValidator.Validate(definition);

            Assert.That(SerializationUtility.HasManagedReferencesWithMissingTypes(definition), Is.True,
                "Unity did not register the missing type; the test asset is wrong, not the validator.");
            Assert.That(definition.Managers[0], Is.Null);
            Assert.That(definition.Managers[1], Is.InstanceOf<RecordingManager>());
            ScopeIssue issue = result.Issues.Single();
            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.UnresolvedManagerType));
            Assert.That(issue.Message, Does.StartWith(
                "Manager 1 has the unresolved type 'Kott.Scopes.Tests.DeletedManager, Kott.Scopes.Tests.Editor'."));
        }

        [Test]
        public void Validate_SceneDoesNotExist_SceneNotFound()
        {
            ScopeDefinition definition = CreateDefinition("S");
            definition.SetScene(new BuildSceneReference("Assets/DoesNotExist.unity"));

            ScopeIssue issue = ScopeValidator.Validate(definition).Issues.Single();

            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.SceneNotFound));
            Assert.That(issue.Message, Does.StartWith("The scene 'DoesNotExist' does not exist."));
        }

        [Test]
        public void Validate_SceneNotInBuildProfile_SceneNotRegistered()
        {
            EditorBuildSettings.scenes = Array.Empty<EditorBuildSettingsScene>();
            ScopeDefinition definition = CreateDefinition("S");
            definition.SetScene(new BuildSceneReference(TemplateScenePath));

            ScopeIssue issue = ScopeValidator.Validate(definition, false).Issues.Single();

            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.SceneNotRegistered));
            Assert.That(issue.Message,
                Does.StartWith($"The scene '{TemplateScenePath}' is not in the scene list of the build profile."));
            Assert.That(issue.Context, Is.SameAs(AssetDatabase.LoadAssetAtPath<SceneAsset>(TemplateScenePath)));
        }

        [Test]
        public void Validate_SceneDisabledInBuildProfile_SceneNotRegistered()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(TemplateScenePath, false) };
            ScopeDefinition definition = CreateDefinition("S");
            definition.SetScene(new BuildSceneReference(TemplateScenePath));

            ScopeIssue issue = ScopeValidator.Validate(definition, false).Issues.Single();

            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.SceneNotRegistered));
            Assert.That(issue.Message, Does.Contain("is disabled in the scene list"));
        }

        [Test]
        public void Validate_InspectRegisteredTemplateScene_NoIssuesAndSceneClosedAgain()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(TemplateScenePath, true) };
            ScopeDefinition definition = CreateDefinition("S");
            definition.SetScene(new BuildSceneReference(TemplateScenePath));

            ScopeValidationResult result = ScopeValidator.Validate(definition, true);

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

            Assert.That(ScopeValidator.ValidateSceneContent(CreateDefinition("S"), "Level", new[] { root }), Is.Empty);
        }

        [Test]
        public void ValidateSceneContent_NoSceneRoot_Error()
        {
            GameObject root = CreateGameObject("Root");

            ScopeIssue issue = ScopeValidator.ValidateSceneContent(CreateDefinition("S"), "Level", new[] { root })
                .Single();

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
                .ValidateSceneContent(CreateDefinition("S"), "Level", new[] { first, second }).Single();

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

            ScopeIssue issue = ScopeValidator.ValidateSceneContent(CreateDefinition("S"), "Level", new[] { root })
                .Single();

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

            ScopeIssue issue = ScopeValidator.ValidateSceneContent(CreateDefinition("S"), "Level", new[] { root })
                .Single();

            Assert.That(issue.Kind, Is.EqualTo(ScopeIssueKind.EmptySceneLinkKey));
            Assert.That(issue.Severity, Is.EqualTo(ScopeIssueSeverity.Warning));
        }

        private ScopeDefinition CreateDefinition(string name, params Manager[] managers)
        {
            return CreateDefinition(name, null, managers);
        }

        private ScopeDefinition CreateDefinition(string name, ScopeDefinition declaredParent, params Manager[] managers)
        {
            ScopeDefinition definition = ScriptableObject.CreateInstance<ScopeDefinition>();
            definition.name = name;
            definition.SetDeclaredParent(declaredParent);
            definition.SetManagers(managers);
            _createdObjects.Add(definition);
            return definition;
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
        ///     Imports a scope definition whose first manager has a type that does not exist, as after renaming a class
        ///     without [MovedFrom].
        /// </summary>
        private static ScopeDefinition ImportScopeWithMissingManagerType()
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
  m_Script: {{fileID: 11500000, guid: {DefinitionScriptGuid}, type: 3}}
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
            return AssetDatabase.LoadAssetAtPath<ScopeDefinition>(path);
        }
    }
}
