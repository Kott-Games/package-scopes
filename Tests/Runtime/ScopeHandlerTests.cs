using System;
using System.Collections;
using System.Collections.Generic;
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

            public override void Update()
            {
                Updates++;
                if (Scope.State != ScopeState.Loaded)
                {
                    UpdatesBeforeLoaded++;
                }
            }

            public override void FixedUpdate()
            {
                FixedUpdates++;
            }
        }

        [Serializable]
        private sealed class ProbeManager : Manager, IProbeService
        { }

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
            WarningRecorder recorder = new();
            first.LoggerFactory = new RecordingLoggerFactory(recorder);

            GameObject secondObject = new("Second");
            secondObject.AddComponent<ScopeHandler>();
            yield return null;

            Assert.That(ScopeHandler.Instance, Is.SameAs(first));
            Assert.That(secondObject.GetComponent<ScopeHandler>() == null, Is.True);
            Assert.That(recorder.Warnings, Has.Count.EqualTo(1));
            Assert.That(recorder.Warnings[0], Does.Contain("A ScopeHandler already exists"));
            Object.Destroy(secondObject);
        }

        [UnityTest]
        public IEnumerator Bootstrap_InEmptyScene_LoadsRootScope()
        {
            ScopeDefinition definition = CreateDefinition("Root");
            GameObject bootstrapObject = new("Bootstrap");
            bootstrapObject.SetActive(false);
            Bootstrap bootstrap = bootstrapObject.AddComponent<Bootstrap>();
            bootstrap.RootScope = definition;
            bootstrapObject.SetActive(true);

            yield return null; // Start
            Assert.That(bootstrap.Operation, Is.Not.Null);
            yield return bootstrap.Operation;

            Assert.That(bootstrap.Operation.Succeeded, Is.True);
            Assert.That(ScopeHandler.Instance.RootScope?.Definition, Is.SameAs(definition));
        }

        [UnityTest]
        public IEnumerator Update_SceneLessScope_IsCalledOnceLoaded()
        {
            // regression: updates were driven by the scene's SceneRoot, so scene-less scopes never got them
            ScopeOperation load = ScopeHandler.Instance.LoadScope(CreateDefinition("NoScene", new CountingManager()));
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
        public IEnumerator Update_AfterUnload_IsNoLongerCalled()
        {
            ScopeOperation load = ScopeHandler.Instance.LoadScope(CreateDefinition("NoScene", new CountingManager()));
            yield return load;
            yield return ScopeHandler.Instance.UnloadScope(load.Scope);
            int updates = CountingManager.Updates;

            yield return null;
            yield return null;

            Assert.That(CountingManager.Updates, Is.EqualTo(updates));
        }

        [UnityTest]
        public IEnumerator Instantiate_ActivePrefab_InjectsBeforeAwake()
        {
            ScopeOperation load = ScopeHandler.Instance.LoadScope(CreateDefinition("S", new ProbeManager()));
            yield return load;
            GameObject prefab = new("Prefab");
            prefab.SetActive(false);
            prefab.AddComponent<AwakeProbe>();
            prefab.SetActive(true);
            Object.Destroy(prefab);

            AwakeProbe instance = load.Scope.Instantiate(prefab).GetComponent<AwakeProbe>();

            Assert.That(instance.Awoken, Is.True);
            Assert.That(instance.InjectedInAwake, Is.True);
            Assert.That(instance.Probe, Is.SameAs(load.Scope.Managers[0]));
            Assert.That(instance.transform.parent, Is.SameAs(load.Scope.Root.transform));
        }

        [UnityTest]
        public IEnumerator StartCoroutine_ScopeUnloaded_StopsCoroutine()
        {
            ScopeOperation load = ScopeHandler.Instance.LoadScope(CreateDefinition("S"));
            yield return load;
            int ticks = 0;
            load.Scope.StartCoroutine(Tick());

            yield return null;
            yield return ScopeHandler.Instance.UnloadScope(load.Scope);
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
        public IEnumerator CoroutineHandler_RoutineThrows_LogsAndKeepsRunning()
        {
            // regression: a throwing routine broke later ones
            ExceptionRecorder recorder = new();
            // CoroutineHandler logs through the scope system's logger
            ScopeHandler.Instance.LoggerFactory = new RecordingLoggerFactory(recorder);
            CoroutineHandler handler = CoroutineHandler.Instance;
            bool secondRan = false;

            handler.Run(Throwing());
            yield return null;
            yield return null;
            handler.Run(Setting());
            yield return null;

            Assert.That(recorder.Exceptions, Has.Count.EqualTo(1));
            Assert.That(recorder.Exceptions[0].Message, Is.EqualTo("boom"));
            Assert.That(secondRan, Is.True);
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

        private sealed class RecordingLoggerFactory : ILoggerFactory
        {
            private readonly ILogHandler _handler;

            public RecordingLoggerFactory(ILogHandler handler)
            {
                _handler = handler;
            }

            public ILogger CreateLogger(string category)
            {
                return new Logger(_handler);
            }
        }

        private sealed class WarningRecorder : ILogHandler
        {
            public readonly List<string> Warnings = new();

            public void LogFormat(LogType logType, Object context, string format, params object[] args)
            {
                if (logType == LogType.Warning)
                {
                    Warnings.Add(string.Format(format, args));
                }
                else
                {
                    Debug.unityLogger.logHandler.LogFormat(logType, context, format, args);
                }
            }

            public void LogException(Exception exception, Object context)
            {
                Debug.unityLogger.logHandler.LogException(exception, context);
            }
        }

        private static ScopeDefinition CreateDefinition(string name, params Manager[] managers)
        {
            ScopeDefinition definition = ScriptableObject.CreateInstance<ScopeDefinition>();
            definition.name = name;
            definition.SetManagers(managers);
            return definition;
        }
    }
}
