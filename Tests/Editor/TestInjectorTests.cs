using System;
using Kott.Scopes.Testing;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Kott.Scopes.Tests
{
    public class TestInjectorTests
    {
        private sealed class FakeFoo : IFooService
        { }

        private sealed class FakeContract : IManagerContract
        { }

        [Test]
        public void Inject_ContractAdded_FillsFieldAndReturnsTarget()
        {
            FakeFoo foo = new();
            FooConsumer target = new();

            FooConsumer result = TestInjector.Create().Add<IFooService>(foo).Inject(target);

            Assert.That(result, Is.SameAs(target));
            Assert.That(target.Foo, Is.SameAs(foo));
        }

        [Test]
        public void Inject_RequiredContractMissing_ThrowsNamingInjector()
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                TestInjector.Create().Inject(new FooConsumer()));

            Assert.That(exception.Message, Is.EqualTo(
                "Cannot inject FooConsumer: Foo: 'IFooService' is not provided by the TestInjector."));
        }

        [Test]
        public void Inject_OptionalContractMissing_LeavesNull()
        {
            OptionalFooConsumer target = TestInjector.Create().Inject(new OptionalFooConsumer());

            Assert.That(target.Foo, Is.Null);
        }

        [Test]
        public void Add_SameContractTwice_Throws()
        {
            TestInjector injector = TestInjector.Create().Add<IFooService>(new FakeFoo());

            Assert.Throws<InvalidOperationException>(() => injector.Add<IFooService>(new FakeFoo()));
        }

        [Test]
        public void Add_MarkerInterfaceItself_Throws()
        {
            Assert.Throws<ArgumentException>(() => TestInjector.Create().Add<IManagerContract>(new FakeContract()));
        }

        [Test]
        public void Inject_SceneObjectAdded_FillsSceneFields()
        {
            GameObject door = new("Door");
            GameObject spawn = new("Spawn");
            BoxCollider collider = spawn.AddComponent<BoxCollider>();
            try
            {
                SceneConsumer target = TestInjector.Create()
                    .AddSceneObject("door", door)
                    .AddSceneObject("spawn", spawn)
                    .Inject(new SceneConsumer());

                Assert.That(target.Door, Is.SameAs(door));
                Assert.That(target.Spawn, Is.SameAs(collider));
            }
            finally
            {
                Object.DestroyImmediate(door);
                Object.DestroyImmediate(spawn);
            }
        }
    }
}
