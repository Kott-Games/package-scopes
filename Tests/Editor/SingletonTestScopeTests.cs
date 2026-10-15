using System;
using Kott.Scopes.Testing;
using NUnit.Framework;

namespace Kott.Scopes.Tests
{
    public class SingletonTestScopeTests
    {
        [Test]
        public void Create_InsideUsing_OverridesAndRestoresAfterwards()
        {
            SingletonRegistry before = SingletonRegistry.Current;
            FooManager fake = new();
            Singleton<IFooService> foo = default;

            using (SingletonTestScope.Create().Add<IFooService>(fake))
            {
                Assert.That(foo.Instance, Is.SameAs(fake));
            }

            Assert.That(SingletonRegistry.Current, Is.SameAs(before));
        }

        [Test]
        public void Create_Nested_RestoresInReverseOrder()
        {
            FooManager outerFake = new();
            FooManager innerFake = new();
            Singleton<IFooService> foo = default;

            using (SingletonTestScope.Create().Add<IFooService>(outerFake))
            {
                using (SingletonTestScope.Create().Add<IFooService>(innerFake))
                {
                    Assert.That(foo.Instance, Is.SameAs(innerFake));
                }

                Assert.That(foo.Instance, Is.SameAs(outerFake));
            }
        }

        [Test]
        public void Dispose_OutOfOrder_Throws()
        {
            SingletonTestScope outer = SingletonTestScope.Create();
            SingletonTestScope inner = SingletonTestScope.Create();
            try
            {
                Assert.Throws<InvalidOperationException>(() => outer.Dispose());
            }
            finally
            {
                inner.Dispose();
                outer.Dispose();
            }
        }

        [Test]
        public void Add_GenericType_RegistersOnlyThatType()
        {
            using SingletonTestScope scope = SingletonTestScope.Create().Add<IFooService>(new FooManager());

            Assert.That(scope.Registry.IsRegistered(typeof(IFooService)), Is.True);
            Assert.That(scope.Registry.IsRegistered(typeof(FooManager)), Is.False);
        }

        [Test]
        public void AddWithContracts_RegistersConcreteTypeAndContracts()
        {
            using SingletonTestScope scope = SingletonTestScope.Create().AddWithContracts(new FooBarManager());

            Assert.That(scope.Registry.IsRegistered(typeof(FooBarManager)), Is.True);
            Assert.That(scope.Registry.IsRegistered(typeof(IFooService)), Is.True);
            Assert.That(scope.Registry.IsRegistered(typeof(IBarService)), Is.True);
        }

        [Test]
        public void Singleton_AfterReRegistration_ResolvesNewInstance()
        {
            using SingletonTestScope scope = SingletonTestScope.Create();
            Singleton<IFooService> foo = default;
            FooManager first = new();
            FooManager second = new();

            scope.Registry.Register("owner", first);
            Assert.That(foo.Instance, Is.SameAs(first));

            scope.Registry.Unregister("owner");
            scope.Registry.Register("owner", second);

            Assert.That(foo.Instance, Is.SameAs(second));
        }

        [Test]
        public void Singleton_NothingRegistered_TryGetFalseAndNotAvailable()
        {
            using SingletonTestScope scope = SingletonTestScope.Create();
            Singleton<IFooService> foo = default;

            Assert.That(foo.IsAvailable, Is.False);
            Assert.That(foo.TryGet(out _), Is.False);
            Assert.Throws<InvalidOperationException>(() => _ = foo.Instance);
        }
    }
}
