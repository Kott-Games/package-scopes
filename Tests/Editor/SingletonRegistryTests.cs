using System;
using NUnit.Framework;

namespace Kott.Scopes.Tests
{
    public class SingletonRegistryTests
    {
        private const string OwnerA = "scope 'A'";
        private const string OwnerB = "scope 'B'";

        private SingletonRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _registry = new SingletonRegistry();
        }

        [Test]
        public void Get_ConcreteType_ReturnsInstance()
        {
            FooManager foo = new();
            _registry.Register(OwnerA, foo);

            Assert.That(_registry.Get<FooManager>(), Is.SameAs(foo));
        }

        [Test]
        public void Get_ContractInterface_ReturnsInstance()
        {
            FooManager foo = new();
            _registry.Register(OwnerA, foo);

            Assert.That(_registry.Get<IFooService>(), Is.SameAs(foo));
        }

        [Test]
        public void Register_MultipleContracts_AllResolvable()
        {
            FooBarManager fooBar = new();
            _registry.Register(OwnerA, fooBar);

            Assert.That(_registry.Get<IFooService>(), Is.SameAs(fooBar));
            Assert.That(_registry.Get<IBarService>(), Is.SameAs(fooBar));
        }

        [Test]
        public void Get_NonContractInterface_ThrowsExplainingContracts()
        {
            _registry.Register(OwnerA, new PlainManager());

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => _registry.Get<IPlainService>());

            Assert.That(exception.Message, Does.Contain("IPlainService").And.Contain("ISingletonContract<IPlainService>"));
            Assert.That(_registry.Get<PlainManager>(), Is.Not.Null);
        }

        [Test]
        public void Get_Unregistered_MessageNamesType()
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => _registry.Get<IFooService>());

            Assert.That(exception.Message, Does.Contain(typeof(IFooService).FullName));
        }

        [Test]
        public void TryGet_Unregistered_ReturnsFalse()
        {
            Assert.That(_registry.TryGet(out IFooService foo), Is.False);
            Assert.That(foo, Is.Null);
        }

        [Test]
        public void Register_DuplicateContract_MessageNamesTypeAndBothOwners()
        {
            _registry.Register(OwnerA, new FooManager());

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() => _registry.Register(OwnerB, new FooBarManager()));

            Assert.That(exception.Message, Does.Contain("IFooService").And.Contain(OwnerA).And.Contain(OwnerB)
                .And.Contain(nameof(FooManager)).And.Contain(nameof(FooBarManager)));
        }

        [Test]
        public void Register_Duplicate_RegistersNothing()
        {
            _registry.Register(OwnerA, new FooManager());

            Assert.Throws<InvalidOperationException>(() => _registry.Register(OwnerB, new FooBarManager()));

            Assert.That(_registry.IsRegistered(typeof(IBarService)), Is.False);
            Assert.That(_registry.IsRegistered(typeof(FooBarManager)), Is.False);
        }

        [Test]
        public void Unregister_Owner_RemovesOnlyItsEntries()
        {
            FooManager foo = new();
            _registry.Register(OwnerA, foo);
            _registry.Register(OwnerB, new BarManager());

            _registry.Unregister(OwnerB);

            Assert.That(_registry.Get<IFooService>(), Is.SameAs(foo));
            Assert.That(_registry.IsRegistered(typeof(IBarService)), Is.False);
            Assert.That(_registry.IsRegistered(typeof(BarManager)), Is.False);
        }

        [Test]
        public void Register_AfterUnregister_Works()
        {
            _registry.Register(OwnerA, new FooManager());
            _registry.Unregister(OwnerA);
            FooManager second = new();

            _registry.Register(OwnerB, second);

            Assert.That(_registry.Get<IFooService>(), Is.SameAs(second));
        }

        [Test]
        public void GetContractTypes_ManagerWithTwoContracts_ReturnsBoth()
        {
            Assert.That(SingletonRegistry.GetContractTypes(typeof(FooBarManager)),
                Is.EquivalentTo(new[] { typeof(IFooService), typeof(IBarService) }));
            Assert.That(SingletonRegistry.GetContractTypes(typeof(PlainManager)), Is.Empty);
        }
    }
}
