using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Kott.Scopes.Tests
{
    /// <summary>A non-manager with an [InjectScene] field, which only managers support.</summary>
    internal sealed class SceneFieldHolder
    {
        [InjectScene("door")]
        public GameObject Door;
    }

    /// <summary>Lifecycle of <see cref="ScopeSystem" /> with fake scene loaders, pumped synchronously.</summary>
    public class ScopeSystemTests
    {
        private readonly List<IEnumerator> _routines = new();
        private readonly List<Object> _createdObjects = new();
        private RecordingLogHandler _log;
        private ScopeSystem _system;

        [SetUp]
        public void SetUp()
        {
            TestLog.Clear();
            _log = new RecordingLogHandler();
            _system = new ScopeSystem(_routines.Add, rootParent: null, _log.Logger);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Scope scope in new List<Scope>(_system.Scopes))
            {
                scope.DestroyRoot();
            }

            foreach (Object createdObject in _createdObjects)
            {
                Object.DestroyImmediate(createdObject);
            }

            _createdObjects.Clear();
            _routines.Clear();
            TestLog.Clear();

            _log.Verify();
        }

        [Test]
        public void Load_Managers_InitializeInListOrder()
        {
            ScopeDefinition definition = CreateDefinition("S", null, Recording("A"), Recording("B"), Recording("C"));

            ScopeOperation load = _system.LoadScope(definition, null, null);
            RunAll();

            Assert.That(load.Succeeded, Is.True);
            Assert.That(load.Scope.State, Is.EqualTo(ScopeState.Loaded));
            Assert.That(TestLog.Entries, Is.EqualTo(new[]
            {
                "A.Initialize", "A.Initialized", "B.Initialize", "B.Initialized", "C.Initialize", "C.Initialized",
            }));
        }

        [Test]
        public void Load_ConsumerListedBeforeProvider_ProviderInitializesFirst()
        {
            ScopeDefinition definition = CreateDefinition("S", null, new FooConsumer { Id = "C" },
                Recording("A"), new FooManager { Id = "F" });

            ScopeOperation load = _system.LoadScope(definition, null, null);
            RunAll();

            Assert.That(TestLog.Entries, Is.EqualTo(new[]
            {
                "A.Initialize", "A.Initialized", "F.Initialize", "F.Initialized",
                "C.Initialize:Foo", "C.Initialize", "C.Initialized",
            }));
            Assert.That(load.Scope.Managers.Select(manager => ((RecordingManager)manager).Id),
                Is.EqualTo(new[] { "A", "F", "C" }));
        }

        [Test]
        public void Load_SameScopeContract_InjectedWithTheScopesManager()
        {
            Scope scope = LoadAndRun(CreateDefinition("S", null, new FooConsumer(), new FooManager()));

            FooConsumer consumer = scope.Managers.OfType<FooConsumer>().Single();
            Assert.That(consumer.Foo, Is.SameAs(scope.Managers.OfType<FooManager>().Single()));
            Assert.That(scope.Resolve<IFooService>(), Is.SameAs(consumer.Foo));
        }

        [Test]
        public void Load_ContractOfGrandparent_Injected()
        {
            Scope root = LoadAndRun(CreateDefinition("Root", null, new FooManager()));
            Scope middle = LoadAndRun(CreateDefinition("Middle", null), root);
            Scope leaf = LoadAndRun(CreateDefinition("Leaf", null, new FooConsumer { Id = "C" }), middle);

            Assert.That(((FooConsumer)leaf.Managers[0]).Foo, Is.SameAs(root.Managers[0]));
            Assert.That(TestLog.Entries, Does.Contain("C.Initialize:Foo"));
        }

        [Test]
        public void Load_ExtendedContract_ResolvableAsBaseContract()
        {
            Scope scope = LoadAndRun(CreateDefinition("S", null, new ExtendedFooManager()));

            Assert.That(scope.Resolve<IFooService>(), Is.SameAs(scope.Managers[0]));
            Assert.That(scope.Resolve<IExtendedFooService>(), Is.SameAs(scope.Managers[0]));
        }

        [Test]
        public void Load_ContractMissing_FailsBeforeAnyInitialize()
        {
            _log.Expect(LogType.Error, "Load of scope 'S' failed");

            ScopeOperation load = _system.LoadScope(
                CreateDefinition("S", null, Recording("A"), new FooConsumer { Id = "C" }), null, null);
            RunAll();

            Assert.That(load.Succeeded, Is.False);
            Assert.That(load.Exception.Message, Is.EqualTo(
                "Cannot inject FooConsumer: Foo: 'IFooService' is not provided by scope 'S' or its parents."));
            Assert.That(TestLog.Entries, Is.Empty);
            Assert.That(_system.Scopes, Is.Empty);
        }

        [Test]
        public void Load_ContractMissingInSeveralManagers_FailureListsAll()
        {
            _log.Expect(LogType.Error, "Load of scope 'S' failed");

            ScopeOperation load = _system.LoadScope(
                CreateDefinition("S", null, new FooConsumer(), new PlainInterfaceConsumer()), null, null);
            RunAll();

            Assert.That(load.Exception.Message, Does.Contain("Cannot inject FooConsumer").And
                .Contain("Cannot inject PlainInterfaceConsumer: Plain: 'IPlainService' is not a manager contract."));
        }

        [Test]
        public void Load_OptionalContractMissing_LoadsWithNullField()
        {
            Scope scope = LoadAndRun(CreateDefinition("S", null, new OptionalFooConsumer()));

            Assert.That(((OptionalFooConsumer)scope.Managers[0]).Foo, Is.Null);
        }

        [Test]
        public void Load_DependencyCycle_FailsNamingCycle()
        {
            _log.Expect(LogType.Error, "Load of scope 'S' failed");

            ScopeOperation load = _system.LoadScope(
                CreateDefinition("S", null, new CycleFoo { Id = "F" }, new CycleBar { Id = "B" }), null, null);
            RunAll();

            Assert.That(load.Exception.Message,
                Does.StartWith(
                    "The managers of scope 'S' depend on each other in a cycle: CycleFoo → CycleBar → CycleFoo."));
            Assert.That(TestLog.Entries, Is.Empty);
        }

        [Test]
        public void Load_RequiredSceneFieldWithoutScene_Fails()
        {
            _log.Expect(LogType.Error, "Load of scope 'S' failed");

            ScopeOperation load = _system.LoadScope(CreateDefinition("S", null, new SceneConsumer()), null, null);
            RunAll();

            Assert.That(load.Exception.Message,
                Does.StartWith("SceneConsumer.Door needs SceneLink 'door', but scope 'S' has no scene."));
        }

        [Test]
        public void Load_ContractProvidedByAncestor_FailsNamingTypeAndBothScopes()
        {
            Scope root = LoadAndRun(CreateDefinition("Root", null, new FooManager()));
            _log.Expect(LogType.Error, "IFooService");

            ScopeOperation load = _system.LoadScope(CreateDefinition("Child", null, new FooBarManager()), root, null);
            RunAll();

            Assert.That(load.Succeeded, Is.False);
            Assert.That(load.Exception.Message, Is.EqualTo(
                "'IFooService' is provided by FooBarManager in scope 'Child' and by FooManager in its ancestor scope " +
                "'Root'. A contract can only have one provider per scope chain; otherwise both scopes would resolve " +
                "it to different managers."));
            Assert.That(root.Resolve<IFooService>(), Is.SameAs(root.Managers[0]));
            Assert.That(root.TryResolve(out IBarService _), Is.False);
        }

        [Test]
        public void Load_ContractProvidedTwiceInScope_Fails()
        {
            _log.Expect(LogType.Error, "provided twice");

            ScopeOperation load = _system.LoadScope(
                CreateDefinition("S", null, new FooManager(), new FooBarManager()), null, null);
            RunAll();

            Assert.That(load.Exception.Message, Does.StartWith(
                "'IFooService' is provided twice in scope 'S': by FooManager and by FooBarManager."));
        }

        [Test]
        public void Load_SiblingsProvideSameContract_EachChainResolvesItsOwn()
        {
            Scope root = LoadAndRun(CreateDefinition("Root", null));
            Scope first = LoadAndRun(CreateDefinition("First", null, new FooManager()), root);
            Scope second = LoadAndRun(CreateDefinition("Second", null, new FooManager()), root);
            Scope firstChild = LoadAndRun(CreateDefinition("FirstChild", null, new FooConsumer()), first);

            Assert.That(((FooConsumer)firstChild.Managers[0]).Foo, Is.SameAs(first.Managers[0]));
            Assert.That(second.Resolve<IFooService>(), Is.SameAs(second.Managers[0]));
            Assert.That(root.TryResolve(out IFooService _), Is.False, "never resolved downwards");
        }

        [Test]
        public void TryResolve_UnloadedChildOfLoadedParent_False()
        {
            Scope root = LoadAndRun(CreateDefinition("Root", null, new FooManager()));
            Scope child = LoadAndRun(CreateDefinition("Child", null), root);

            _system.UnloadScope(child, null);
            RunAll();

            Assert.That(child.TryResolve(out IFooService _), Is.False);
            Assert.That(root.TryResolve(out IFooService _), Is.True);
        }

        [Test]
        public void Resolve_MissingContract_ThrowsNamingScope()
        {
            Scope scope = LoadAndRun(CreateDefinition("S", null));

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                scope.Resolve<IFooService>());

            Assert.That(exception.Message, Is.EqualTo("'IFooService' is not provided by scope 'S' or its parents."));
        }

        [Test]
        public void Inject_PlainObject_FillsFieldsFromChain()
        {
            Scope root = LoadAndRun(CreateDefinition("Root", null, new FooManager()));
            Scope child = LoadAndRun(CreateDefinition("Child", null), root);
            FooConsumer target = new();

            child.Inject(target);

            Assert.That(target.Foo, Is.SameAs(root.Managers[0]));
        }

        [Test]
        public void Inject_NonManagerWithSceneFields_Throws()
        {
            Scope scope = LoadAndRun(CreateDefinition("S", null));

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() => scope.Inject(new SceneFieldHolder()));

            Assert.That(exception.Message, Does.Contain("only supported on managers"));
        }

        [Test]
        public void Instantiate_Prefab_InjectsComponentsAndParentsUnderRoot()
        {
            Scope scope = LoadAndRun(CreateDefinition("S", null, new FooManager()));
            GameObject prefab = new("Prefab");
            _createdObjects.Add(prefab);
            prefab.AddComponent<InjectedComponent>();
            new GameObject("Child").transform.SetParent(prefab.transform);
            prefab.transform.GetChild(0).gameObject.AddComponent<InjectedComponent>();

            GameObject instance = scope.Instantiate(prefab);

            Assert.That(instance.transform.parent, Is.SameAs(scope.Root.transform));
            Assert.That(instance.GetComponentsInChildren<InjectedComponent>().Select(component => component.Foo),
                Is.EqualTo(new[] { scope.Managers[0], scope.Managers[0] }));
            Assert.That(scope.Root.transform.childCount, Is.EqualTo(1), "the temporary holder is gone");
        }

        [Test]
        public void Instantiate_ComponentWithExplicitParent_ReturnsInjectedCopy()
        {
            Scope scope = LoadAndRun(CreateDefinition("S", null, new FooManager()));
            GameObject parent = new("Parent");
            _createdObjects.Add(parent);
            InjectedComponent prefab = new GameObject("Prefab").AddComponent<InjectedComponent>();
            _createdObjects.Add(prefab.gameObject);

            InjectedComponent instance = scope.Instantiate(prefab, parent.transform);

            Assert.That(instance, Is.Not.SameAs(prefab));
            Assert.That(instance.Foo, Is.SameAs(scope.Managers[0]));
            Assert.That(instance.transform.parent, Is.SameAs(parent.transform));
            Assert.That(prefab.Foo, Is.Null);
        }

        [Test]
        public void Instantiate_MissingContract_ThrowsAndDestroysCopy()
        {
            Scope scope = LoadAndRun(CreateDefinition("S", null));
            GameObject prefab = new("Prefab");
            _createdObjects.Add(prefab);
            prefab.AddComponent<InjectedComponent>();

            Assert.Throws<InvalidOperationException>(() => scope.Instantiate(prefab));

            Assert.That(scope.Root.transform.childCount, Is.EqualTo(0));
        }

        [Test]
        public void BindScene_SceneFieldsAndComponents_InjectedAndClearedOnUnbind()
        {
            Scope scope = LoadAndRun(CreateDefinition("S", new FakeSceneReference("Scene"), new FooManager(),
                new SceneConsumer()));
            GameObject door = CreateSceneObject("Door");
            AddLink(door, "door");
            GameObject spawn = CreateSceneObject("Spawn");
            BoxCollider collider = spawn.AddComponent<BoxCollider>();
            AddLink(spawn, "spawn");
            InjectedComponent component = CreateSceneObject("Tile").AddComponent<InjectedComponent>();

            scope.BindScene(SceneManager.GetActiveScene());

            SceneConsumer consumer = scope.Managers.OfType<SceneConsumer>().Single();
            Assert.That(consumer.Door, Is.SameAs(door));
            Assert.That(consumer.Spawn, Is.SameAs(collider));
            Assert.That(component.Foo, Is.SameAs(scope.Managers.OfType<FooManager>().Single()));

            scope.UnbindScene();

            Assert.That(consumer.Door, Is.Null);
            Assert.That(consumer.Spawn, Is.Null);
        }

        [Test]
        public void BindScene_SceneLinkMissing_ThrowsNamingKey()
        {
            Scope scope = LoadAndRun(CreateDefinition("S", new FakeSceneReference("Scene"), new SceneConsumer()));
            AddLink(CreateSceneObject("Door"), "door");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                scope.BindScene(SceneManager.GetActiveScene()));

            Assert.That(exception.Message, Is.EqualTo(
                "Cannot inject scene objects into SceneConsumer: Spawn: no SceneLink with key 'spawn' in the scene " +
                "of scope 'S'."));
            Assert.That(((SceneConsumer)scope.Managers[0]).Door, Is.Null, "all or nothing");
        }

        [Test]
        public void Load_WithScene_InitializesManagersBeforeSceneLoad()
        {
            ScopeDefinition definition = CreateDefinition("S", new FakeSceneReference("Scene"), Recording("A"));

            _system.LoadScope(definition, null, null);
            RunAll();

            Assert.That(TestLog.IndexOf("A.Initialized"), Is.LessThan(TestLog.IndexOf("Scene.Load")));
        }

        [Test]
        public void Load_Managers_AreCopiesOfTheAuthoredOnes()
        {
            RecordingManager authored = Recording("A");
            ScopeDefinition definition = CreateDefinition("S", null, authored);

            ScopeOperation load = _system.LoadScope(definition, null, null);
            RunAll();

            Manager runtimeManager = load.Scope.Managers[0];
            Assert.That(runtimeManager, Is.Not.SameAs(authored));
            Assert.That(runtimeManager.Scope, Is.SameAs(load.Scope));
            Assert.That(authored.Scope, Is.Null);
        }

        [Test]
        public void Load_NullParent_FirstBecomesRootThenChildrenAttachToRoot()
        {
            ScopeOperation rootLoad = _system.LoadScope(CreateDefinition("Root", null), null, null);
            ScopeOperation childLoad = _system.LoadScope(CreateDefinition("Child", null), null, null);
            RunAll();

            Assert.That(_system.RootScope, Is.SameAs(rootLoad.Scope));
            Assert.That(rootLoad.Scope.Parent, Is.Null);
            Assert.That(childLoad.Scope.Parent, Is.SameAs(rootLoad.Scope));
            Assert.That(rootLoad.Scope.Children, Is.EqualTo(new[] { childLoad.Scope }));
        }

        [Test]
        public void Load_ExplicitParent_LinksUnderParent()
        {
            Scope root = LoadAndRun(CreateDefinition("Root", null));
            Scope middle = LoadAndRun(CreateDefinition("Middle", null), root);
            Scope leaf = LoadAndRun(CreateDefinition("Leaf", null), middle);

            Assert.That(leaf.Parent, Is.SameAs(middle));
            Assert.That(middle.Children, Is.EqualTo(new[] { leaf }));
            Assert.That(leaf.Root.transform.parent, Is.SameAs(middle.Root.transform));
        }

        [Test]
        public void Load_UnderDeclaredParent_DoesNotWarn()
        {
            ScopeDefinition parentScope = CreateDefinition("Parent", null);
            ScopeDefinition childScope = CreateDefinition("Child", null);
            childScope.SetDeclaredParent(parentScope);

            Scope parent = LoadAndRun(parentScope);
            LoadAndRun(childScope, parent);

            _log.AssertNotLogged(LogType.Warning, "declares parent");
        }

        [Test]
        public void Load_UnderOtherThanDeclaredParent_WarnsAndLoads()
        {
            ScopeDefinition declared = CreateDefinition("Declared", null);
            ScopeDefinition childScope = CreateDefinition("Child", null);
            childScope.SetDeclaredParent(declared);
            _log.Expect(LogType.Warning, "Scope 'Child' declares parent 'Declared' but is loaded under scope 'Other'");

            Scope other = LoadAndRun(CreateDefinition("Other", null));
            Scope child = LoadAndRun(childScope, other);

            Assert.That(child.Parent, Is.SameAs(other));
        }

        [Test]
        public void Load_AsRootWithDeclaredParent_Warns()
        {
            ScopeDefinition childScope = CreateDefinition("Child", null);
            childScope.SetDeclaredParent(CreateDefinition("Declared", null));
            _log.Expect(LogType.Warning, "Scope 'Child' declares parent 'Declared' but is loaded as the root scope");

            LoadAndRun(childScope);
        }

        [Test]
        public void Load_Progress_IsMonotonicFromZeroToOne()
        {
            RecordingManager slow = Recording("A");
            slow.InitializeFrames = 4;
            ScopeDefinition definition = CreateDefinition("S", new FakeSceneReference("Scene"), slow, Recording("B"));
            RecordingProgress progress = new();

            _system.LoadScope(definition, null, progress);
            RunAll();

            AssertMonotonicZeroToOne(progress.Values);
        }

        [Test]
        public void Unload_ScopeWithChild_IsDepthFirst()
        {
            Scope root = LoadAndRun(CreateDefinition("Root", new FakeSceneReference("RootScene"), Recording("R")));
            LoadAndRun(CreateDefinition("Child", new FakeSceneReference("ChildScene"), Recording("C")), root);
            TestLog.Entries.Clear();

            _system.UnloadScope(root, null);
            RunAll();

            Assert.That(TestLog.Entries, Is.EqualTo(new[]
            {
                "C.SceneExit", "ChildScene.Unload", "C.Uninitialize",
                "R.SceneExit", "RootScene.Unload", "R.Uninitialize",
            }));
        }

        [Test]
        public void Unload_Managers_UninitializeInReverseOrder()
        {
            Scope scope = LoadAndRun(CreateDefinition("S", null, Recording("A"), Recording("B")));
            TestLog.Entries.Clear();

            _system.UnloadScope(scope, null);
            RunAll();

            Assert.That(TestLog.Entries, Is.EqualTo(new[] { "B.Uninitialize", "A.Uninitialize" }));
        }

        [Test]
        public void Unload_Managers_UninitializeInReverseInitializationOrder()
        {
            Scope scope = LoadAndRun(CreateDefinition("S", null, new FooConsumer { Id = "C" },
                new FooManager { Id = "F" }));
            TestLog.Entries.Clear();

            _system.UnloadScope(scope, null);
            RunAll();

            Assert.That(TestLog.Entries,
                Is.EqualTo(new[] { "C.Uninitialize:Foo", "C.Uninitialize", "F.Uninitialize" }));
        }

        [Test]
        public void Unload_Contracts_AvailableDuringUninitializeThenCleared()
        {
            Scope scope = LoadAndRun(CreateDefinition("S", null, new FooConsumer { Id = "C" }, new FooManager()));
            FooConsumer consumer = (FooConsumer)scope.Managers[1];

            _system.UnloadScope(scope, null);
            RunAll();

            Assert.That(TestLog.Entries, Does.Contain("C.Uninitialize:Foo"));
            Assert.That(consumer.Foo, Is.Null);
            Assert.That(scope.TryResolve(out IFooService _), Is.False);
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                scope.Resolve<IFooService>());
            Assert.That(exception.Message, Is.EqualTo("scope 'S' is Unloaded; its contracts are no longer available."));
        }

        [Test]
        public void Unload_Scope_DestroysRootAndDetaches()
        {
            Scope root = LoadAndRun(CreateDefinition("Root", null));
            Scope child = LoadAndRun(CreateDefinition("Child", null), root);
            GameObject childRoot = child.Root;

            ScopeOperation unload = _system.UnloadScope(child, null);
            RunAll();

            Assert.That(unload.Succeeded, Is.True);
            Assert.That(child.State, Is.EqualTo(ScopeState.Unloaded));
            Assert.That(childRoot == null, Is.True, "root GameObject destroyed");
            Assert.That(root.Children, Is.Empty);
            Assert.That(_system.Scopes, Is.EqualTo(new[] { root }));
        }

        [Test]
        public void Unload_RootScope_ClearsRootScope()
        {
            Scope root = LoadAndRun(CreateDefinition("Root", null));

            _system.UnloadScope(root, null);
            RunAll();

            Assert.That(_system.RootScope, Is.Null);
            Assert.That(_system.Scopes, Is.Empty);
        }

        [Test]
        public void Unload_Progress_IsMonotonicFromZeroToOne()
        {
            Scope root = LoadAndRun(CreateDefinition("Root", new FakeSceneReference("RootScene"), Recording("R")));
            LoadAndRun(CreateDefinition("Child", null, Recording("C1"), Recording("C2")), root);
            RecordingProgress progress = new();

            _system.UnloadScope(root, progress);
            RunAll();

            AssertMonotonicZeroToOne(progress.Values);
        }

        [Test]
        public void Load_TwoLoadsRequestedTogether_RunSequentially()
        {
            RecordingManager slow = Recording("A");
            slow.InitializeFrames = 3;
            ScopeDefinition first = CreateDefinition("First", null, slow);
            ScopeDefinition second = CreateDefinition("Second", null, Recording("B"));

            ScopeOperation firstLoad = _system.LoadScope(first, null, null);
            ScopeOperation secondLoad = _system.LoadScope(second, null, null);
            RunAll();

            Assert.That(firstLoad.Succeeded && secondLoad.Succeeded, Is.True);
            Assert.That(TestLog.IndexOf("A.Initialized"), Is.LessThan(TestLog.IndexOf("B.Initialize")));
            Assert.That(secondLoad.Scope.Parent, Is.SameAs(firstLoad.Scope));
        }

        [Test]
        public void Load_InitializeThrows_FailsAndCleansUpPartialScope()
        {
            RecordingManager failing = Recording("Bad");
            failing.ThrowOnInitialize = true;
            ScopeDefinition definition =
                CreateDefinition("S", null, new FooManager { Id = "Foo" }, failing, Recording("After"));
            _log.Expect(LogType.Error, "Load of scope 'S' failed.*Bad failed to initialize");

            ScopeOperation load = _system.LoadScope(definition, null, null);
            RunAll();

            Assert.That(load.IsDone, Is.True);
            Assert.That(load.Succeeded, Is.False);
            Assert.That(load.Exception.Message, Is.EqualTo("Bad failed to initialize"));
            Assert.That(load.Scope.State, Is.EqualTo(ScopeState.Failed));
            Assert.That(TestLog.Entries, Does.Contain("Foo.Uninitialize"));
            Assert.That(TestLog.Entries, Does.Not.Contain("Bad.Uninitialize"));
            Assert.That(TestLog.Entries, Does.Not.Contain("After.Initialize"));
            Assert.That(load.Scope.TryResolve(out IFooService _), Is.False);
            Assert.That(load.Scope.Root == null, Is.True);
            Assert.That(_system.Scopes, Is.Empty);
            Assert.That(_system.RootScope, Is.Null);
        }

        [Test]
        public void Load_AfterFailedLoad_NextLoadWorks()
        {
            // regression: an exception in Initialize used to deadlock every later load
            RecordingManager failing = Recording("Bad");
            failing.ThrowOnInitialize = true;
            _log.Expect(LogType.Error, "Load of scope 'Broken' failed");
            _system.LoadScope(CreateDefinition("Broken", null, failing), null, null);
            RunAll();

            ScopeOperation load = _system.LoadScope(CreateDefinition("Working", null, Recording("A")), null, null);
            RunAll();

            Assert.That(load.Succeeded, Is.True);
            Assert.That(_system.RootScope, Is.SameAs(load.Scope));
        }

        [Test]
        public void Load_InitializeThrowsSynchronously_FailsLoad()
        {
            _log.Expect(LogType.Error, "thrown synchronously");

            ScopeOperation load =
                _system.LoadScope(CreateDefinition("S", null, new ThrowImmediatelyManager()), null, null);
            RunAll();

            Assert.That(load.Succeeded, Is.False);
            Assert.That(load.Exception.Message, Is.EqualTo("thrown synchronously"));
        }

        [Test]
        public void Load_SceneLoadFails_UninitializesAllManagersAndUnloadsScene()
        {
            ScopeDefinition definition = CreateDefinition("S", new FakeSceneReference("Scene", failLoad: true),
                Recording("A"),
                Recording("B"));
            _log.Expect(LogType.Error, "Scene failed to load");

            ScopeOperation load = _system.LoadScope(definition, null, null);
            RunAll();

            Assert.That(load.Succeeded, Is.False);
            Assert.That(TestLog.Entries, Is.EqualTo(new[]
            {
                "A.Initialize", "A.Initialized", "B.Initialize", "B.Initialized",
                "Scene.Load", "Scene.Unload", "B.Uninitialize", "A.Uninitialize",
            }));
        }

        [Test]
        public void Load_AlreadyLoadedScope_Fails()
        {
            ScopeDefinition definition = CreateDefinition("S", null);
            LoadAndRun(definition);
            _log.Expect(LogType.Error, "already loaded");

            ScopeOperation second = _system.LoadScope(definition, null, null);
            RunAll();

            Assert.That(second.Succeeded, Is.False);
            Assert.That(_system.Scopes.Count, Is.EqualTo(1));
        }

        [Test]
        public void Load_MissingManagerEntry_Fails()
        {
            ScopeDefinition definition = CreateDefinition("S", null, Recording("A"), null);
            _log.Expect(LogType.Error, "missing manager at index 1");

            ScopeOperation load = _system.LoadScope(definition, null, null);
            RunAll();

            Assert.That(load.Succeeded, Is.False);
            Assert.That(TestLog.Entries, Is.Empty);
        }

        [Test]
        public void Unload_UninitializeThrows_StillUnloadsAndReportsFailure()
        {
            RecordingManager failing = Recording("Bad");
            failing.ThrowOnUninitialize = true;
            Scope scope = LoadAndRun(CreateDefinition("S", null, Recording("A"), failing));
            _log.Expect(LogType.Exception, "Bad failed to uninitialize");

            ScopeOperation unload = _system.UnloadScope(scope, null);
            RunAll();

            Assert.That(unload.Succeeded, Is.False);
            Assert.That(TestLog.Entries, Does.Contain("A.Uninitialize"));
            Assert.That(scope.State, Is.EqualTo(ScopeState.Unloaded));
            Assert.That(_system.Scopes, Is.Empty);
        }

        [Test]
        public void Unload_AlreadyUnloadedScope_Fails()
        {
            Scope scope = LoadAndRun(CreateDefinition("S", null));
            _system.UnloadScope(scope, null);
            RunAll();
            _log.Expect(LogType.Error, "Cannot unload scope 'S'");

            ScopeOperation second = _system.UnloadScope(scope, null);
            RunAll();

            Assert.That(second.Succeeded, Is.False);
        }

        [Test]
        public void IsLoaded_FollowsLoadAndUnload()
        {
            ScopeDefinition definition = CreateDefinition("S", null);
            Assert.That(_system.IsLoaded(definition), Is.False);

            Scope scope = LoadAndRun(definition);
            Assert.That(_system.IsLoaded(definition), Is.True);
            Assert.That(_system.TryGetScope(definition, out Scope found) && found == scope, Is.True);

            _system.UnloadScope(scope, null);
            RunAll();
            Assert.That(_system.IsLoaded(definition), Is.False);
            Assert.That(_system.TryGetScope(definition, out _), Is.False);
        }

        [Test]
        public void Events_RaisedOnLoadAndUnload_ChildrenFirst()
        {
            List<string> events = new();
            _system.ScopeLoaded += s => events.Add($"Loaded {s.Name}");
            _system.ScopeUnloaded += s => events.Add($"Unloaded {s.Name}");

            Scope root = LoadAndRun(CreateDefinition("Root", null));
            LoadAndRun(CreateDefinition("Child", null), root);
            _system.UnloadScope(root, null);
            RunAll();

            Assert.That(events, Is.EqualTo(new[] { "Loaded Root", "Loaded Child", "Unloaded Child", "Unloaded Root" }));
        }

        [Test]
        public void Operation_CompletedSubscribedAfterCompletion_InvokedImmediately()
        {
            ScopeOperation load = _system.LoadScope(CreateDefinition("S", null), null, null);
            RunAll();

            bool invoked = false;
            load.Completed += _ => invoked = true;

            Assert.That(invoked, Is.True);
            Assert.That(load.keepWaiting, Is.False);
        }

        [Test]
        public void NotifyApplicationQuit_NothingLoaded_DoesNotThrow()
        {
            // regression: OnApplicationQuit threw if nothing was loaded
            Assert.DoesNotThrow(() => _system.NotifyApplicationQuit());
        }

        [Test]
        public void NotifyApplicationQuit_ScopesWithScene_SceneExitChildrenFirst()
        {
            Scope root = LoadAndRun(CreateDefinition("Root", new FakeSceneReference("RootScene"), Recording("R")));
            LoadAndRun(CreateDefinition("Child", new FakeSceneReference("ChildScene"), Recording("C")), root);
            LoadAndRun(CreateDefinition("NoScene", null, Recording("N")), root);
            TestLog.Entries.Clear();

            _system.NotifyApplicationQuit();

            Assert.That(TestLog.Entries, Is.EqualTo(new[] { "C.SceneExit", "R.SceneExit" }));
        }

        [Test]
        public void GetSceneGameObject_UnknownKey_ThrowsWithKeyAndScope()
        {
            Scope scope = LoadAndRun(CreateDefinition("S", null));

            KeyNotFoundException exception =
                Assert.Throws<KeyNotFoundException>(() => scope.GetSceneGameObject("Door"));

            Assert.That(exception.Message, Does.Contain("'Door'").And.Contain("'S'"));
            Assert.That(scope.TryGetSceneGameObject("Door", out _), Is.False);
        }

        private static RecordingManager Recording(string id)
        {
            return new RecordingManager { Id = id };
        }

        private GameObject CreateSceneObject(string name)
        {
            GameObject gameObject = new(name);
            _createdObjects.Add(gameObject);
            return gameObject;
        }

        private static void AddLink(GameObject gameObject, string key)
        {
            SceneLink link = gameObject.AddComponent<SceneLink>();
            using SerializedObject serializedLink = new(link);
            serializedLink.FindProperty("_key").stringValue = key;
            serializedLink.ApplyModifiedPropertiesWithoutUndo();
        }

        private ScopeDefinition CreateDefinition(string name, SceneReference scene, params Manager[] managers)
        {
            ScopeDefinition definition = ScriptableObject.CreateInstance<ScopeDefinition>();
            definition.name = name;
            definition.SetScene(scene);
            definition.SetManagers(managers);
            _createdObjects.Add(definition);
            return definition;
        }

        private Scope LoadAndRun(ScopeDefinition definition, Scope parent = null)
        {
            ScopeOperation load = _system.LoadScope(definition, parent, null);
            RunAll();
            Assert.That(load.Succeeded, Is.True, $"Loading '{definition.name}' failed: {load.Exception}");
            return load.Scope;
        }

        /// <summary>Runs all started routines to completion, without waiting for frames.</summary>
        private void RunAll()
        {
            int steps = 0;
            while (_routines.Count > 0)
            {
                IEnumerator routine = _routines[0];
                _routines.RemoveAt(0);
                while (routine.MoveNext())
                {
                    Assert.That(++steps, Is.LessThan(10000), "Routine did not finish.");
                }
            }
        }

        private static void AssertMonotonicZeroToOne(List<float> values)
        {
            Assert.That(values, Is.Not.Empty);
            Assert.That(values[0], Is.EqualTo(0f));
            Assert.That(values[^1], Is.EqualTo(1f).Within(1e-5f));
            for (int i = 1; i < values.Count; i++)
            {
                Assert.That(values[i], Is.GreaterThanOrEqualTo(values[i - 1] - 1e-5f),
                    $"Progress went backwards at report {i}: {string.Join(", ", values)}");
                Assert.That(values[i], Is.LessThanOrEqualTo(1f + 1e-5f));
            }
        }
    }
}
