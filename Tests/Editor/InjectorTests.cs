using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Kott.Scopes.Tests
{
    /// <summary>Field discovery and filling (<see cref="Injector" />) and ordering (<see cref="InitializationOrder" />).</summary>
    public class InjectorTests
    {
        private sealed class FooService : IFooService
        { }

        private sealed class BarService : IBarService
        { }

        private class BaseTarget
        {
            [Inject]
            private IFooService _foo;

            public IFooService BaseFoo => _foo;
        }

        private sealed class DerivedTarget : BaseTarget
        {
            [Inject]
            public IBarService Bar;

            [Inject(Optional = true)]
            public IExtendedFooService Extended;

            public IFooService NotInjected;
        }

        private sealed class SceneTarget
        {
            [InjectScene("door")]
            public GameObject Door;

            [InjectScene("door")]
            public Transform DoorTransform;

            [InjectScene("spawn", Optional = true)]
            public BoxCollider Spawn;
        }

        private readonly List<Object> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object createdObject in _createdObjects)
            {
                Object.DestroyImmediate(createdObject);
            }

            _createdObjects.Clear();
        }

        [Test]
        public void IsContract_InterfacesAndMarker_OnlyDerivedInterfaces()
        {
            Assert.That(Injector.IsContract(typeof(IFooService)), Is.True);
            Assert.That(Injector.IsContract(typeof(IExtendedFooService)), Is.True);
            Assert.That(Injector.IsContract(typeof(IManagerContract)), Is.False);
            Assert.That(Injector.IsContract(typeof(IPlainService)), Is.False);
            Assert.That(Injector.IsContract(typeof(FooManager)), Is.False);
        }

        [Test]
        public void GetInfo_DerivedType_IncludesInheritedPrivateFieldsOnly()
        {
            InjectionInfo info = Injector.GetInfo(typeof(DerivedTarget));

            Assert.That(info.Fields.Select(field => field.Field.Name),
                Is.EquivalentTo(new[] { "Bar", "Extended", "_foo" }));
            Assert.That(Injector.GetInfo(typeof(DerivedTarget)), Is.SameAs(info), "cached per type");
        }

        [Test]
        public void Inject_AllProvided_FillsFieldsIncludingInherited()
        {
            FooService foo = new();
            BarService bar = new();
            DerivedTarget target = new();

            Injector.Inject(target,
                type => type == typeof(IFooService) ? foo : type == typeof(IBarService) ? bar : null,
                "test");

            Assert.That(target.BaseFoo, Is.SameAs(foo));
            Assert.That(target.Bar, Is.SameAs(bar));
            Assert.That(target.Extended, Is.Null, "optional and missing");
            Assert.That(target.NotInjected, Is.Null);
        }

        [Test]
        public void Inject_RequiredMissing_ThrowsAndFillsNothing()
        {
            DerivedTarget target = new();
            FooService foo = new();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                Injector.Inject(target, type => type == typeof(IFooService) ? foo : null, "the test"));

            Assert.That(exception.Message, Is.EqualTo(
                "Cannot inject DerivedTarget: Bar: 'IBarService' is not provided by the test."));
            Assert.That(target.BaseFoo, Is.Null);
        }

        [Test]
        public void Clear_InjectedTarget_SetsFieldsToNull()
        {
            FooConsumer target = new() { Foo = new FooService() };

            Injector.Clear(target);

            Assert.That(target.Foo, Is.Null);
        }

        [Test]
        public void InjectScene_LinkPresent_FillsGameObjectAndComponent()
        {
            GameObject door = CreateGameObject("Door");
            SceneTarget target = new();

            Injector.InjectScene(target, key => key == "door" ? door : null, "the test");

            Assert.That(target.Door, Is.SameAs(door));
            Assert.That(target.DoorTransform, Is.SameAs(door.transform));
            Assert.That(target.Spawn, Is.Null);
        }

        [Test]
        public void InjectScene_RequiredLinkMissing_ThrowsNamingKeyAndSource()
        {
            SceneTarget target = new();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                Injector.InjectScene(target, _ => null, "the scene of scope 'Board'"));

            Assert.That(exception.Message, Does.Contain(
                "Door: no SceneLink with key 'door' in the scene of scope 'Board'."));
        }

        [Test]
        public void InjectScene_ComponentMissing_Throws()
        {
            GameObject spawn = CreateGameObject("Spawn");
            SceneConsumer target = new();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                Injector.InjectScene(target, key => spawn, "test"));

            Assert.That(exception.Message,
                Does.Contain("Spawn: SceneLink 'spawn' (Spawn) has no BoxCollider component."));
            Assert.That(target.Door, Is.Null, "all or nothing");
        }

        [Test]
        public void TryCompute_NoDependencies_ListOrder()
        {
            Assert.That(InitializationOrder.TryCompute(new[] { typeof(RecordingManager), typeof(FooManager) },
                out List<int> order, out _), Is.True);
            Assert.That(order, Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void TryCompute_ChainListedBackwards_DependenciesFirst()
        {
            // CycleBar needs IFooService (FooManager); FooConsumer needs IFooService as well
            Type[] types = { typeof(FooConsumer), typeof(CycleBar), typeof(FooManager) };

            Assert.That(InitializationOrder.TryCompute(types, out List<int> order, out _), Is.True);
            Assert.That(order, Is.EqualTo(new[] { 2, 0, 1 }));
        }

        [Test]
        public void TryCompute_Cycle_ReturnsCycle()
        {
            Type[] types = { typeof(RecordingManager), typeof(CycleBar), typeof(CycleFoo) };

            Assert.That(InitializationOrder.TryCompute(types, out List<int> order, out List<int> cycle), Is.False);
            Assert.That(order, Is.Null);
            Assert.That(InitializationOrder.DescribeCycle(types, cycle), Is.EqualTo("CycleBar → CycleFoo → CycleBar"));
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new(name);
            _createdObjects.Add(gameObject);
            return gameObject;
        }
    }
}
