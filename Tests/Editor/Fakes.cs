using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Kott.Utilities.Progress;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kott.Scopes.Tests
{
    /// <summary>Shared record of lifecycle calls. Static, because loaded scopes work on copies of the managers.</summary>
    internal static class TestLog
    {
        public static readonly List<string> Entries = new();

        public static void Add(string entry)
        {
            Entries.Add(entry);
        }

        public static void Clear()
        {
            Entries.Clear();
        }

        public static int IndexOf(string entry)
        {
            int index = Entries.IndexOf(entry);
            Assert.That(index, Is.GreaterThanOrEqualTo(0),
                $"'{entry}' was not logged. Log: {string.Join(", ", Entries)}");
            return index;
        }
    }

    internal interface IFooService : IManagerContract
    { }

    internal interface IBarService : IManagerContract
    { }

    /// <summary>A contract extending another one: providers provide both.</summary>
    internal interface IExtendedFooService : IFooService
    { }

    /// <summary>An interface that is not a manager contract.</summary>
    internal interface IPlainService
    { }

    [Serializable]
    internal class RecordingManager : Manager
    {
        public string Id = "M";
        public int InitializeFrames;
        public bool ThrowOnInitialize;
        public bool ThrowOnUninitialize;

        public override IEnumerator Initialize(IProgress<ProgressReport> progress)
        {
            TestLog.Add($"{Id}.Initialize");
            for (int i = 0; i < InitializeFrames; i++)
            {
                progress?.Report(new ProgressReport((float)i / InitializeFrames, null));
                yield return null;
            }

            if (ThrowOnInitialize)
            {
                throw new InvalidOperationException($"{Id} failed to initialize");
            }

            TestLog.Add($"{Id}.Initialized");
        }

        public override IEnumerator Uninitialize(IProgress<ProgressReport> progress)
        {
            TestLog.Add($"{Id}.Uninitialize");
            if (ThrowOnUninitialize)
            {
                throw new InvalidOperationException($"{Id} failed to uninitialize");
            }

            yield break;
        }

        public override void OnSceneAwake()
        {
            TestLog.Add($"{Id}.SceneAwake");
        }

        public override void OnSceneExit()
        {
            TestLog.Add($"{Id}.SceneExit");
        }
    }

    [Serializable]
    internal class FooManager : RecordingManager, IFooService
    { }

    [Serializable]
    internal class BarManager : RecordingManager, IBarService
    { }

    [Serializable]
    internal class FooBarManager : Manager, IFooService, IBarService
    { }

    [Serializable]
    internal class PlainManager : Manager, IPlainService
    { }

    /// <summary>Provides the contract of <see cref="AwakeProbe" /> (runtime test assembly, so Unity can attach it).</summary>
    [Serializable]
    internal class ProbeManager : Manager, IProbeService
    { }

    [Serializable]
    internal class ExtendedFooManager : Manager, IExtendedFooService
    { }

    /// <summary>Injects IFooService and logs whether it is set when (un)initializing.</summary>
    [Serializable]
    internal class FooConsumer : RecordingManager
    {
        [Inject]
        public IFooService Foo;

        public override IEnumerator Initialize(IProgress<ProgressReport> progress)
        {
            TestLog.Add(Foo != null ? $"{Id}.Initialize:Foo" : $"{Id}.Initialize:NoFoo");
            return base.Initialize(progress);
        }

        public override IEnumerator Uninitialize(IProgress<ProgressReport> progress)
        {
            TestLog.Add(Foo != null ? $"{Id}.Uninitialize:Foo" : $"{Id}.Uninitialize:NoFoo");
            return base.Uninitialize(progress);
        }
    }

    [Serializable]
    internal class OptionalFooConsumer : Manager
    {
        [Inject(Optional = true)]
        public IFooService Foo;
    }

    [Serializable]
    internal class PlainInterfaceConsumer : Manager
    {
        [Inject]
        public IPlainService Plain;
    }

    /// <summary>Provides IFooService and needs IBarService; with <see cref="CycleBar" /> a dependency cycle.</summary>
    [Serializable]
    internal class CycleFoo : RecordingManager, IFooService
    {
        [Inject]
        public IBarService Bar;
    }

    [Serializable]
    internal class CycleBar : RecordingManager, IBarService
    {
        [Inject]
        public IFooService Foo;
    }

    [Serializable]
    internal class SceneConsumer : RecordingManager
    {
        [InjectScene("door")]
        public GameObject Door;

        [InjectScene("spawn")]
        public BoxCollider Spawn;
    }

    [Serializable]
    internal class OptionalSceneConsumer : Manager
    {
        [InjectScene("door", Optional = true)]
        public GameObject Door;
    }

    /// <summary>Throws synchronously from a non-iterator override.</summary>
    [Serializable]
    internal class ThrowImmediatelyManager : Manager
    {
        public override IEnumerator Initialize(IProgress<ProgressReport> progress)
        {
            throw new InvalidOperationException("thrown synchronously");
        }
    }

    [Serializable]
    internal sealed class FakeSceneReference : SceneReference
    {
        public string Name = "Scene";
        public bool FailLoad;
        public bool HasSceneRoot = true;

        public FakeSceneReference()
        { }

        public FakeSceneReference(string name, bool failLoad = false)
        {
            Name = name;
            FailLoad = failLoad;
        }

        public override bool IsAssigned => true;
        public override string DisplayName => Name;

        public override ISceneLoader CreateLoader(ILogger logger)
        {
            return new FakeSceneLoader(Name, FailLoad, HasSceneRoot);
        }
    }

    /// <summary>
    ///     Completes instantly without loading a scene. Activates the active (test) scene like its SceneRoot would.
    /// </summary>
    internal sealed class FakeSceneLoader : ISceneLoader
    {
        private readonly string _name;
        private readonly bool _failLoad;
        private readonly bool _hasSceneRoot;

        public FakeSceneLoader(string name, bool failLoad, bool hasSceneRoot)
        {
            _name = name;
            _failLoad = failLoad;
            _hasSceneRoot = hasSceneRoot;
        }

        public Scene Scene => default;

        public IEnumerator Load(IProgress<ProgressReport> progress)
        {
            TestLog.Add($"{_name}.Load");
            progress?.Report(new ProgressReport(0.5f, null));
            yield return null;

            if (_failLoad)
            {
                throw new InvalidOperationException($"{_name} failed to load");
            }

            if (_hasSceneRoot)
            {
                ScopeSystem.ActivatingScope.ActivateScene(SceneManager.GetActiveScene());
            }

            progress?.Report(new ProgressReport(1f, null));
        }

        public IEnumerator Unload(IProgress<ProgressReport> progress)
        {
            TestLog.Add($"{_name}.Unload");
            yield break;
        }
    }

    internal sealed class RecordingProgress : IProgress<ProgressReport>
    {
        public readonly List<float> Values = new();

        public void Report(ProgressReport value)
        {
            Values.Add(value.Progress);
        }
    }

    internal sealed class DelegateLoggerFactory : ILoggerFactory
    {
        private readonly Func<string, ILogger> _create;

        public DelegateLoggerFactory(Func<string, ILogger> create)
        {
            _create = create;
        }

        public ILogger CreateLogger(string category)
        {
            return _create(category);
        }
    }

    /// <summary>
    ///     Records log output instead of printing it, so expected failures don't show up as console errors.
    ///     Expectations are checked by <see cref="Verify" />, which also fails on unexpected errors.
    /// </summary>
    internal sealed class RecordingLogHandler : ILogHandler
    {
        private readonly List<(LogType Type, string Message)> _entries = new();
        private readonly List<(LogType Type, string Pattern)> _expectations = new();

        public RecordingLogHandler()
        {
            Logger = new Logger(this);
        }

        public ILogger Logger { get; }

        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
        {
            _entries.Add((logType, string.Format(format, args)));
        }

        public void LogException(Exception exception, UnityEngine.Object context)
        {
            _entries.Add((LogType.Exception, exception.ToString()));
        }

        /// <summary>Expects a log of <paramref name="type" /> matching <paramref name="pattern" /> by the end of the test.</summary>
        public void Expect(LogType type, string pattern)
        {
            _expectations.Add((type, pattern));
        }

        /// <summary>Fails if a log of <paramref name="type" /> matching <paramref name="pattern" /> was recorded.</summary>
        public void AssertNotLogged(LogType type, string pattern)
        {
            List<(LogType Type, string Message)> matches =
                _entries.FindAll(entry => entry.Type == type && Regex.IsMatch(entry.Message, pattern));
            Assert.That(matches, Is.Empty, $"Unexpected {type} log matching '{pattern}': {Describe(matches)}");
        }

        public void Verify()
        {
            List<(LogType Type, string Message)> remaining = new(_entries);
            foreach ((LogType type, string pattern) in _expectations)
            {
                int index = remaining.FindIndex(entry => entry.Type == type && Regex.IsMatch(entry.Message, pattern));
                Assert.That(index, Is.GreaterThanOrEqualTo(0),
                    $"Expected {type} log matching '{pattern}'. Logged: {Describe(_entries)}");
                remaining.RemoveAt(index);
            }

            remaining.RemoveAll(entry => entry.Type is LogType.Log or LogType.Warning);
            Assert.That(remaining, Is.Empty, $"Unexpected error logs: {Describe(remaining)}");
        }

        private static string Describe(List<(LogType Type, string Message)> entries)
        {
            return entries.Count == 0
                ? "none"
                : string.Join(" | ", entries.ConvertAll(entry => $"[{entry.Type}] {entry.Message}"));
        }
    }
}
