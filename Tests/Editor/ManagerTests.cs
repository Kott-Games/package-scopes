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
            [Inject]
            private INameService _names;

            public string Greeting { get; private set; }

            public override IEnumerator Initialize(IProgress<ProgressReport> progress)
            {
                Greeting = $"Hello {_names.Name}";
                yield break;
            }
        }

        private interface INameService : IManagerContract
        {
            string Name { get; }
        }

        private sealed class FakeNameService : INameService
        {
            public string Name => "Ada";
        }

        [Test]
        public void Initialize_DependencyFromTestInjector_UsesFake()
        {
            GreetingManager manager = TestInjector.Create().Add<INameService>(new FakeNameService())
                .Inject(new GreetingManager());

            IEnumerator routine = manager.Initialize(null);
            while (routine.MoveNext())
            { }

            Assert.That(manager.Greeting, Is.EqualTo("Hello Ada"));
        }
    }
}
