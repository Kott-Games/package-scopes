using System;
using System.Collections;
using Kott.Scopes.Testing;
using Kott.Utilities.Progress;
using NUnit.Framework;

namespace Kott.Scopes.Tests
{
    /// <summary>Managers are plain classes: constructible and testable without loading a scope.</summary>
    public class ManagerTests
    {
        [Serializable]
        private sealed class GreetingManager : Manager
        {
            private Singleton<INameService> _names;

            public string Greeting { get; private set; }

            public override IEnumerator Initialize(IProgress<ProgressReport> progress)
            {
                Greeting = $"Hello {_names.Instance.Name}";
                yield break;
            }
        }

        private interface INameService : ISingletonContract<INameService>
        {
            string Name { get; }
        }

        private sealed class FakeNameService : INameService
        {
            public string Name => "Ada";
        }

        [Test]
        public void Initialize_DependencyFromSingletonTestScope_UsesFake()
        {
            using SingletonTestScope singletons = SingletonTestScope.Create().Add<INameService>(new FakeNameService());
            GreetingManager manager = new();

            IEnumerator routine = manager.Initialize(null);
            while (routine.MoveNext())
            { }

            Assert.That(manager.Greeting, Is.EqualTo("Hello Ada"));
        }
    }
}
