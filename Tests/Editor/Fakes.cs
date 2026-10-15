using System;
using System.Collections;
using System.Collections.Generic;
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

        /// <summary>The registry of the system under test, for probes.</summary>
        public static SingletonRegistry Registry;

        public static void Add(string entry)
        {
            Entries.Add(entry);
        }

        public static void Clear()
        {
            Entries.Clear();
            Registry = null;
        }

        public static int IndexOf(string entry)
        {
            int index = Entries.IndexOf(entry);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), $"'{entry}' was not logged. Log: {string.Join(", ", Entries)}");
            return index;
        }
    }

    internal interface IFooService : ISingletonContract<IFooService>
    { }

    internal interface IBarService : ISingletonContract<IBarService>
    { }

    /// <summary>An ISingleton interface that is not a contract.</summary>
    internal interface IPlainService : ISingleton
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

        public override void SceneExit()
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

    /// <summary>Logs whether IFooService is registered during its own Initialize/Uninitialize.</summary>
    [Serializable]
    internal class RegistryProbeManager : Manager
    {
        public override IEnumerator Initialize(IProgress<ProgressReport> progress)
        {
            TestLog.Add(TestLog.Registry.IsRegistered(typeof(IFooService)) ? "Probe.Initialize:Foo" : "Probe.Initialize:NoFoo");
            yield break;
        }

        public override IEnumerator Uninitialize(IProgress<ProgressReport> progress)
        {
            TestLog.Add(TestLog.Registry.IsRegistered(typeof(IFooService)) ? "Probe.Uninitialize:Foo" : "Probe.Uninitialize:NoFoo");
            yield break;
        }
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

        public FakeSceneReference()
        { }

        public FakeSceneReference(string name, bool failLoad = false)
        {
            Name = name;
            FailLoad = failLoad;
        }

        public override bool IsAssigned => true;
        public override string DisplayName => Name;

        public override ISceneLoader CreateLoader()
        {
            return new FakeSceneLoader(Name, FailLoad);
        }
    }

    /// <summary>Completes instantly without a real scene.</summary>
    internal sealed class FakeSceneLoader : ISceneLoader
    {
        private readonly string _name;
        private readonly bool _failLoad;

        public FakeSceneLoader(string name, bool failLoad)
        {
            _name = name;
            _failLoad = failLoad;
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
}
