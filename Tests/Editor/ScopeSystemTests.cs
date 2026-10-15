using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Kott.Scopes.Tests
{
    /// <summary>Lifecycle of <see cref="ScopeSystem" /> with fake scene loaders, pumped synchronously.</summary>
    public class ScopeSystemTests
    {
        private readonly List<IEnumerator> _routines = new();
        private readonly List<Object> _createdObjects = new();
        private SingletonRegistry _registry;
        private RecordingLogHandler _log;
        private ScopeSystem _system;

        [SetUp]
        public void SetUp()
        {
            TestLog.Clear();
            _registry = new SingletonRegistry();
            TestLog.Registry = _registry;
            _log = new RecordingLogHandler();
            _system = new ScopeSystem(_registry, _routines.Add, rootParent: null, _log.Logger);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (RuntimeScope runtimeScope in new List<RuntimeScope>(_system.Scopes))
            {
                runtimeScope.DestroyRoot();
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
            Scope scope = CreateScope("S", null, Recording("A"), Recording("B"), Recording("C"));

            ScopeOperation load = _system.LoadScope(scope, null, null);
            RunAll();

            Assert.That(load.Succeeded, Is.True);
            Assert.That(load.RuntimeScope.State, Is.EqualTo(ScopeState.Loaded));
            Assert.That(TestLog.Entries, Is.EqualTo(new[]
            {
                "A.Initialize", "A.Initialized", "B.Initialize", "B.Initialized", "C.Initialize", "C.Initialized",
            }));
        }

        [Test]
        public void Load_SingletonManager_RegisteredBeforeFirstInitialize()
        {
            Scope scope = CreateScope("S", null, new RegistryProbeManager(), new FooManager { Id = "Foo" });

            _system.LoadScope(scope, null, null);
            RunAll();

            Assert.That(TestLog.Entries[0], Is.EqualTo("Probe.Initialize:Foo"));
            Assert.That(_registry.Get<IFooService>(), Is.InstanceOf<FooManager>());
        }

        [Test]
        public void Load_WithScene_InitializesManagersBeforeSceneLoad()
        {
            Scope scope = CreateScope("S", new FakeSceneReference("Scene"), Recording("A"));

            _system.LoadScope(scope, null, null);
            RunAll();

            Assert.That(TestLog.IndexOf("A.Initialized"), Is.LessThan(TestLog.IndexOf("Scene.Load")));
        }

        [Test]
        public void Load_Managers_AreCopiesOfTheAuthoredOnes()
        {
            RecordingManager authored = Recording("A");
            Scope scope = CreateScope("S", null, authored);

            ScopeOperation load = _system.LoadScope(scope, null, null);
            RunAll();

            Manager runtimeManager = load.RuntimeScope.Managers[0];
            Assert.That(runtimeManager, Is.Not.SameAs(authored));
            Assert.That(runtimeManager.RuntimeScope, Is.SameAs(load.RuntimeScope));
            Assert.That(authored.RuntimeScope, Is.Null);
        }

        [Test]
        public void Load_NullParent_FirstBecomesRootThenChildrenAttachToRoot()
        {
            ScopeOperation rootLoad = _system.LoadScope(CreateScope("Root", null), null, null);
            ScopeOperation childLoad = _system.LoadScope(CreateScope("Child", null), null, null);
            RunAll();

            Assert.That(_system.RootScope, Is.SameAs(rootLoad.RuntimeScope));
            Assert.That(rootLoad.RuntimeScope.Parent, Is.Null);
            Assert.That(childLoad.RuntimeScope.Parent, Is.SameAs(rootLoad.RuntimeScope));
            Assert.That(rootLoad.RuntimeScope.Children, Is.EqualTo(new[] { childLoad.RuntimeScope }));
        }

        [Test]
        public void Load_ExplicitParent_LinksUnderParent()
        {
            RuntimeScope root = LoadAndRun(CreateScope("Root", null));
            RuntimeScope middle = LoadAndRun(CreateScope("Middle", null), root);
            RuntimeScope leaf = LoadAndRun(CreateScope("Leaf", null), middle);

            Assert.That(leaf.Parent, Is.SameAs(middle));
            Assert.That(middle.Children, Is.EqualTo(new[] { leaf }));
            Assert.That(leaf.Root.transform.parent, Is.SameAs(middle.Root.transform));
        }

        [Test]
        public void Load_Progress_IsMonotonicFromZeroToOne()
        {
            RecordingManager slow = Recording("A");
            slow.InitializeFrames = 4;
            Scope scope = CreateScope("S", new FakeSceneReference("Scene"), slow, Recording("B"));
            RecordingProgress progress = new();

            _system.LoadScope(scope, null, progress);
            RunAll();

            AssertMonotonicZeroToOne(progress.Values);
        }

        [Test]
        public void Unload_ScopeWithChild_IsDepthFirst()
        {
            RuntimeScope root = LoadAndRun(CreateScope("Root", new FakeSceneReference("RootScene"), Recording("R")));
            LoadAndRun(CreateScope("Child", new FakeSceneReference("ChildScene"), Recording("C")), root);
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
            RuntimeScope runtimeScope = LoadAndRun(CreateScope("S", null, Recording("A"), Recording("B")));
            TestLog.Entries.Clear();

            _system.UnloadScope(runtimeScope, null);
            RunAll();

            Assert.That(TestLog.Entries, Is.EqualTo(new[] { "B.Uninitialize", "A.Uninitialize" }));
        }

        [Test]
        public void Unload_Singletons_RemovedAfterUninitialize()
        {
            RuntimeScope runtimeScope = LoadAndRun(CreateScope("S", null, new RegistryProbeManager(), new FooManager()));

            _system.UnloadScope(runtimeScope, null);
            RunAll();

            Assert.That(TestLog.Entries, Does.Contain("Probe.Uninitialize:Foo"));
            Assert.That(_registry.IsRegistered(typeof(IFooService)), Is.False);
            Assert.That(_registry.IsRegistered(typeof(FooManager)), Is.False);
        }

        [Test]
        public void Unload_Scope_DestroysRootAndDetaches()
        {
            RuntimeScope root = LoadAndRun(CreateScope("Root", null));
            RuntimeScope child = LoadAndRun(CreateScope("Child", null), root);
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
            RuntimeScope root = LoadAndRun(CreateScope("Root", null));

            _system.UnloadScope(root, null);
            RunAll();

            Assert.That(_system.RootScope, Is.Null);
            Assert.That(_system.Scopes, Is.Empty);
        }

        [Test]
        public void Unload_Progress_IsMonotonicFromZeroToOne()
        {
            RuntimeScope root = LoadAndRun(CreateScope("Root", new FakeSceneReference("RootScene"), Recording("R")));
            LoadAndRun(CreateScope("Child", null, Recording("C1"), Recording("C2")), root);
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
            Scope first = CreateScope("First", null, slow);
            Scope second = CreateScope("Second", null, Recording("B"));

            ScopeOperation firstLoad = _system.LoadScope(first, null, null);
            ScopeOperation secondLoad = _system.LoadScope(second, null, null);
            RunAll();

            Assert.That(firstLoad.Succeeded && secondLoad.Succeeded, Is.True);
            Assert.That(TestLog.IndexOf("A.Initialized"), Is.LessThan(TestLog.IndexOf("B.Initialize")));
            Assert.That(secondLoad.RuntimeScope.Parent, Is.SameAs(firstLoad.RuntimeScope));
        }

        [Test]
        public void Load_InitializeThrows_FailsAndCleansUpPartialScope()
        {
            RecordingManager failing = Recording("Bad");
            failing.ThrowOnInitialize = true;
            Scope scope = CreateScope("S", null, new FooManager { Id = "Foo" }, failing, Recording("After"));
            _log.Expect(LogType.Error, "Load of scope 'S' failed.*Bad failed to initialize");

            ScopeOperation load = _system.LoadScope(scope, null, null);
            RunAll();

            Assert.That(load.IsDone, Is.True);
            Assert.That(load.Succeeded, Is.False);
            Assert.That(load.Exception.Message, Is.EqualTo("Bad failed to initialize"));
            Assert.That(load.RuntimeScope.State, Is.EqualTo(ScopeState.Failed));
            Assert.That(TestLog.Entries, Does.Contain("Foo.Uninitialize"));
            Assert.That(TestLog.Entries, Does.Not.Contain("Bad.Uninitialize"));
            Assert.That(TestLog.Entries, Does.Not.Contain("After.Initialize"));
            Assert.That(_registry.IsRegistered(typeof(IFooService)), Is.False);
            Assert.That(load.RuntimeScope.Root == null, Is.True);
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
            _system.LoadScope(CreateScope("Broken", null, failing), null, null);
            RunAll();

            ScopeOperation load = _system.LoadScope(CreateScope("Working", null, Recording("A")), null, null);
            RunAll();

            Assert.That(load.Succeeded, Is.True);
            Assert.That(_system.RootScope, Is.SameAs(load.RuntimeScope));
        }

        [Test]
        public void Load_InitializeThrowsSynchronously_FailsLoad()
        {
            _log.Expect(LogType.Error, "thrown synchronously");

            ScopeOperation load = _system.LoadScope(CreateScope("S", null, new ThrowImmediatelyManager()), null, null);
            RunAll();

            Assert.That(load.Succeeded, Is.False);
            Assert.That(load.Exception.Message, Is.EqualTo("thrown synchronously"));
        }

        [Test]
        public void Load_SceneLoadFails_UninitializesAllManagersAndUnloadsScene()
        {
            Scope scope = CreateScope("S", new FakeSceneReference("Scene", failLoad: true), Recording("A"), Recording("B"));
            _log.Expect(LogType.Error, "Scene failed to load");

            ScopeOperation load = _system.LoadScope(scope, null, null);
            RunAll();

            Assert.That(load.Succeeded, Is.False);
            Assert.That(TestLog.Entries, Is.EqualTo(new[]
            {
                "A.Initialize", "A.Initialized", "B.Initialize", "B.Initialized",
                "Scene.Load", "Scene.Unload", "B.Uninitialize", "A.Uninitialize",
            }));
        }

        [Test]
        public void Load_DuplicateSingleton_FailsNamingTypeAndBothScopes()
        {
            RuntimeScope root = LoadAndRun(CreateScope("Root", null, new FooManager()));
            _log.Expect(LogType.Error, "IFooService");

            ScopeOperation load = _system.LoadScope(CreateScope("Child", null, new FooBarManager()), root, null);
            RunAll();

            Assert.That(load.Succeeded, Is.False);
            Assert.That(load.Exception.Message, Does.Contain("IFooService").And.Contain("'Root'").And.Contain("'Child'"));
            Assert.That(_registry.Get<IFooService>(), Is.SameAs(root.Managers[0]));
            Assert.That(_registry.IsRegistered(typeof(IBarService)), Is.False, "registration is all or nothing");
        }

        [Test]
        public void Load_AlreadyLoadedScope_Fails()
        {
            Scope scope = CreateScope("S", null);
            LoadAndRun(scope);
            _log.Expect(LogType.Error, "already loaded");

            ScopeOperation second = _system.LoadScope(scope, null, null);
            RunAll();

            Assert.That(second.Succeeded, Is.False);
            Assert.That(_system.Scopes.Count, Is.EqualTo(1));
        }

        [Test]
        public void Load_MissingManagerEntry_Fails()
        {
            Scope scope = CreateScope("S", null, Recording("A"), null);
            _log.Expect(LogType.Error, "missing manager at index 1");

            ScopeOperation load = _system.LoadScope(scope, null, null);
            RunAll();

            Assert.That(load.Succeeded, Is.False);
            Assert.That(TestLog.Entries, Is.Empty);
        }

        [Test]
        public void Unload_UninitializeThrows_StillUnloadsAndReportsFailure()
        {
            RecordingManager failing = Recording("Bad");
            failing.ThrowOnUninitialize = true;
            RuntimeScope runtimeScope = LoadAndRun(CreateScope("S", null, Recording("A"), failing));
            _log.Expect(LogType.Exception, "Bad failed to uninitialize");

            ScopeOperation unload = _system.UnloadScope(runtimeScope, null);
            RunAll();

            Assert.That(unload.Succeeded, Is.False);
            Assert.That(TestLog.Entries, Does.Contain("A.Uninitialize"));
            Assert.That(runtimeScope.State, Is.EqualTo(ScopeState.Unloaded));
            Assert.That(_system.Scopes, Is.Empty);
        }

        [Test]
        public void Unload_AlreadyUnloadedScope_Fails()
        {
            RuntimeScope runtimeScope = LoadAndRun(CreateScope("S", null));
            _system.UnloadScope(runtimeScope, null);
            RunAll();
            _log.Expect(LogType.Error, "Cannot unload scope 'S'");

            ScopeOperation second = _system.UnloadScope(runtimeScope, null);
            RunAll();

            Assert.That(second.Succeeded, Is.False);
        }

        [Test]
        public void IsLoaded_FollowsLoadAndUnload()
        {
            Scope scope = CreateScope("S", null);
            Assert.That(_system.IsLoaded(scope), Is.False);

            RuntimeScope runtimeScope = LoadAndRun(scope);
            Assert.That(_system.IsLoaded(scope), Is.True);
            Assert.That(_system.TryGetRuntimeScope(scope, out RuntimeScope found) && found == runtimeScope, Is.True);

            _system.UnloadScope(runtimeScope, null);
            RunAll();
            Assert.That(_system.IsLoaded(scope), Is.False);
            Assert.That(_system.TryGetRuntimeScope(scope, out _), Is.False);
        }

        [Test]
        public void Events_RaisedOnLoadAndUnload_ChildrenFirst()
        {
            List<string> events = new();
            _system.ScopeLoaded += s => events.Add($"Loaded {s.Name}");
            _system.ScopeUnloaded += s => events.Add($"Unloaded {s.Name}");

            RuntimeScope root = LoadAndRun(CreateScope("Root", null));
            LoadAndRun(CreateScope("Child", null), root);
            _system.UnloadScope(root, null);
            RunAll();

            Assert.That(events, Is.EqualTo(new[] { "Loaded Root", "Loaded Child", "Unloaded Child", "Unloaded Root" }));
        }

        [Test]
        public void Operation_CompletedSubscribedAfterCompletion_InvokedImmediately()
        {
            ScopeOperation load = _system.LoadScope(CreateScope("S", null), null, null);
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
            RuntimeScope root = LoadAndRun(CreateScope("Root", new FakeSceneReference("RootScene"), Recording("R")));
            LoadAndRun(CreateScope("Child", new FakeSceneReference("ChildScene"), Recording("C")), root);
            LoadAndRun(CreateScope("NoScene", null, Recording("N")), root);
            TestLog.Entries.Clear();

            _system.NotifyApplicationQuit();

            Assert.That(TestLog.Entries, Is.EqualTo(new[] { "C.SceneExit", "R.SceneExit" }));
        }

        [Test]
        public void GetSceneGameObject_UnknownKey_ThrowsWithKeyAndScope()
        {
            RuntimeScope runtimeScope = LoadAndRun(CreateScope("S", null));

            KeyNotFoundException exception = Assert.Throws<KeyNotFoundException>(() => runtimeScope.GetSceneGameObject("Door"));

            Assert.That(exception.Message, Does.Contain("'Door'").And.Contain("'S'"));
            Assert.That(runtimeScope.TryGetSceneGameObject("Door", out _), Is.False);
        }

        private static RecordingManager Recording(string id)
        {
            return new RecordingManager { Id = id };
        }

        private Scope CreateScope(string name, SceneReference scene, params Manager[] managers)
        {
            Scope scope = ScriptableObject.CreateInstance<Scope>();
            scope.name = name;
            scope.SetScene(scene);
            scope.SetManagers(managers);
            _createdObjects.Add(scope);
            return scope;
        }

        private RuntimeScope LoadAndRun(Scope scope, RuntimeScope parent = null)
        {
            ScopeOperation load = _system.LoadScope(scope, parent, null);
            RunAll();
            Assert.That(load.Succeeded, Is.True, $"Loading '{scope.name}' failed: {load.Exception}");
            return load.RuntimeScope;
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
