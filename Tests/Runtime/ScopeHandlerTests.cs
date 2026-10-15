using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Kott.Utilities.Progress;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Kott.Scopes.Tests
{
    /// <summary>PlayMode smoke tests for the self-contained host and update driving.</summary>
    public class ScopeHandlerTests
    {
        [Serializable]
        private sealed class CountingManager : Manager
        {
            public static int Updates;
            public static int FixedUpdates;
            public static int UpdatesBeforeLoaded;

            public override IEnumerator Initialize(IProgress<ProgressReport> progress)
            {
                yield return null;
            }

            public override void SceneUpdate()
            {
                Updates++;
                if (RuntimeScope.State != ScopeState.Loaded)
                {
                    UpdatesBeforeLoaded++;
                }
            }

            public override void SceneFixedUpdate()
            {
                FixedUpdates++;
            }
        }

        [SetUp]
        public void SetUp()
        {
            CountingManager.Updates = 0;
            CountingManager.FixedUpdates = 0;
            CountingManager.UpdatesBeforeLoaded = 0;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (ScopeHandler.HasInstance)
            {
                ScopeHandler handler = ScopeHandler.Instance;
                if (handler.RootScope != null)
                {
                    yield return handler.UnloadScope(handler.RootScope);
                }

                Object.DestroyImmediate(handler.gameObject);
            }

            foreach (Bootstrap bootstrap in Object.FindObjectsByType<Bootstrap>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(bootstrap.gameObject);
            }
        }

        [Test]
        public void Instance_FirstAccess_CreatesSinglePersistentHost()
        {
            ScopeHandler first = ScopeHandler.Instance;
            ScopeHandler second = ScopeHandler.Instance;

            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.SameAs(first));
            Assert.That(Object.FindObjectsByType<ScopeHandler>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(first.gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));
        }

        [UnityTest]
        public IEnumerator Instance_HandPlacedHost_IsUsedAndNotDuplicated()
        {
            ScopeHandler handPlaced = new GameObject("Hand-placed").AddComponent<ScopeHandler>();
            yield return null;

            Assert.That(ScopeHandler.Instance, Is.SameAs(handPlaced));
            Assert.That(Object.FindObjectsByType<ScopeHandler>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Awake_SecondHost_RemovesItself()
        {
            ScopeHandler first = ScopeHandler.Instance;
            LogAssert.Expect(LogType.Warning, new Regex("A ScopeHandler already exists"));

            GameObject secondObject = new("Second");
            secondObject.AddComponent<ScopeHandler>();
            yield return null;

            Assert.That(ScopeHandler.Instance, Is.SameAs(first));
            Assert.That(secondObject.GetComponent<ScopeHandler>() == null, Is.True);
            Object.Destroy(secondObject);
        }

        [UnityTest]
        public IEnumerator Bootstrap_InEmptyScene_LoadsRootScope()
        {
            Scope scope = CreateScope("Root");
            GameObject bootstrapObject = new("Bootstrap");
            bootstrapObject.SetActive(false);
            Bootstrap bootstrap = bootstrapObject.AddComponent<Bootstrap>();
            bootstrap.RootScope = scope;
            bootstrapObject.SetActive(true);

            yield return null; // Start
            Assert.That(bootstrap.Operation, Is.Not.Null);
            yield return bootstrap.Operation;

            Assert.That(bootstrap.Operation.Succeeded, Is.True);
            Assert.That(ScopeHandler.Instance.RootScope?.Scope, Is.SameAs(scope));
        }

        [UnityTest]
        public IEnumerator SceneUpdate_SceneLessScope_IsCalledOnceLoaded()
        {
            // regression: updates were driven by the scene's SceneRoot, so scene-less scopes never got them
            ScopeOperation load = ScopeHandler.Instance.LoadScope(CreateScope("NoScene", new CountingManager()));
            yield return load;
            Assert.That(load.Succeeded, Is.True);

            yield return null;
            yield return null;
            yield return new WaitForFixedUpdate();

            Assert.That(CountingManager.Updates, Is.GreaterThan(0));
            Assert.That(CountingManager.FixedUpdates, Is.GreaterThan(0));
            Assert.That(CountingManager.UpdatesBeforeLoaded, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator SceneUpdate_AfterUnload_IsNoLongerCalled()
        {
            ScopeOperation load = ScopeHandler.Instance.LoadScope(CreateScope("NoScene", new CountingManager()));
            yield return load;
            yield return ScopeHandler.Instance.UnloadScope(load.RuntimeScope);
            int updates = CountingManager.Updates;

            yield return null;
            yield return null;

            Assert.That(CountingManager.Updates, Is.EqualTo(updates));
        }

        [UnityTest]
        public IEnumerator StartCoroutine_ScopeUnloaded_StopsCoroutine()
        {
            ScopeOperation load = ScopeHandler.Instance.LoadScope(CreateScope("S"));
            yield return load;
            int ticks = 0;
            load.RuntimeScope.StartCoroutine(Tick());

            yield return null;
            yield return ScopeHandler.Instance.UnloadScope(load.RuntimeScope);
            int ticksAtUnload = ticks;
            yield return null;
            yield return null;

            Assert.That(ticks, Is.GreaterThan(0));
            Assert.That(ticks, Is.EqualTo(ticksAtUnload));

            IEnumerator Tick()
            {
                while (true)
                {
                    ticks++;
                    yield return null;
                }
            }
        }

        [UnityTest]
        public IEnumerator CoroutineHandler_RoutineThrows_LogsAndReusesRunner()
        {
            // regression: a throwing routine kept its runner out of the pool forever
            ExceptionRecorder recorder = new();
            CoroutineHandler handler = CoroutineHandler.Instance;
            handler.Logger = new Logger(recorder);
            bool secondRan = false;

            handler.Run(Throwing());
            yield return null;
            yield return null;
            handler.Run(Setting());
            yield return null;

            Assert.That(recorder.Exceptions, Has.Count.EqualTo(1));
            Assert.That(recorder.Exceptions[0].Message, Is.EqualTo("boom"));
            Assert.That(secondRan, Is.True);
            Assert.That(handler.GetComponents<CoroutineRunner>().Length, Is.EqualTo(1),
                "runner returned to the pool and reused");
            Object.DestroyImmediate(handler.gameObject);

            IEnumerator Throwing()
            {
                yield return null;
                throw new InvalidOperationException("boom");
            }

            IEnumerator Setting()
            {
                secondRan = true;
                yield break;
            }
        }

        private sealed class ExceptionRecorder : ILogHandler
        {
            public readonly List<Exception> Exceptions = new();

            public void LogFormat(LogType logType, Object context, string format, params object[] args)
            {
                Debug.unityLogger.logHandler.LogFormat(logType, context, format, args);
            }

            public void LogException(Exception exception, Object context)
            {
                Exceptions.Add(exception);
            }
        }

        private static Scope CreateScope(string name, params Manager[] managers)
        {
            Scope scope = ScriptableObject.CreateInstance<Scope>();
            scope.name = name;
            scope.SetManagers(managers);
            return scope;
        }
    }
}
