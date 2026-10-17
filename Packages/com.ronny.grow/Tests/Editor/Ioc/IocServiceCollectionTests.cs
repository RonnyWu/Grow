// SPDX-License-Identifier: MIT

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Grow.Kernel.Ioc.Common;
using Grow.Kernel.Ioc.Interface;
using NUnit.Framework;
using GrowIOC = Grow.Kernel.Ioc.IOC;

namespace Grow.Tests.Editor.Ioc {
    [Category("Grow.Ioc")]
    public class IocServiceCollectionTests : IocTestBase {
        [Test]
        public void AddSingleton_Instance_ResolvesSameInstance() {
            var foo = new Foo();
            GrowIOC.AddSingleton<IFoo>(foo);

            Assert.AreSame(foo, GrowIOC.GetService<IFoo>());
            Assert.AreSame(foo, GrowIOC.GetService<IFoo>());
        }

        [Test]
        public void InjectSingleton_Factory_CreatesOnce() {
            GrowIOC.InjectSingleton<IFoo, CountingFooFactory>();

            var first = GrowIOC.GetService<IFoo>();
            var second = GrowIOC.GetService<IFoo>();

            Assert.AreSame(first, second);
            Assert.AreEqual(1, CountingFooFactory.Calls);
        }

        [Test]
        public void InjectInstance_Factory_CreatesEachTime() {
            GrowIOC.InjectInstance<IFoo, CountingFooFactory>();

            var first = GrowIOC.GetService<IFoo>();
            var second = GrowIOC.GetService<IFoo>();

            Assert.AreNotSame(first, second);
            Assert.AreEqual(2, CountingFooFactory.Calls);
        }

        [Test]
        public void AddUnique_DistinctKeys_ResolveDistinctInstances() {
            var a = new Foo();
            var b = new Foo();
            GrowIOC.AddUnique<IFoo>(a, "a");
            GrowIOC.AddUnique<IFoo>(b, "b");

            Assert.AreSame(a, GrowIOC.GetService<IFoo>("a"));
            Assert.AreSame(b, GrowIOC.GetService<IFoo>("b"));
            Assert.AreNotSame(GrowIOC.GetService<IFoo>("a"), GrowIOC.GetService<IFoo>("b"));
        }

        [Test]
        public void InjectUnique_Factory_CachesPerKey() {
            GrowIOC.InjectUnique<IBar, BarFactory>("a");
            GrowIOC.InjectUnique<IBar, BarFactory>("b");

            var a1 = GrowIOC.GetService<IBar>("a");
            var a2 = GrowIOC.GetService<IBar>("a");
            var b1 = GrowIOC.GetService<IBar>("b");

            Assert.AreSame(a1, a2);
            Assert.AreNotSame(a1, b1);
        }

        [Test]
        public void KeylessRegistration_NotVisibleFromKeyedLookup() {
            GrowIOC.AddSingleton<IFoo>(new Foo());

            Assert.IsTrue(GrowIOC.Contains<IFoo>());
            Assert.IsFalse(GrowIOC.Contains<IFoo>("missing"));
            Assert.IsNull(GrowIOC.GetService<IFoo>("missing"));
        }

        [Test]
        public void KeyedRegistration_NotVisibleFromKeylessLookup() {
            GrowIOC.AddUnique<IFoo>(new Foo(), "x");

            Assert.IsTrue(GrowIOC.Contains<IFoo>("x"));
            Assert.IsFalse(GrowIOC.Contains<IFoo>());
            Assert.IsNull(GrowIOC.GetService<IFoo>());
        }

        [Test]
        public void KeyedAndKeyless_CoexistIndependently() {
            var keyless = new Foo();
            var keyed = new Foo();
            GrowIOC.AddSingleton<IFoo>(keyless);
            GrowIOC.AddUnique<IFoo>(keyed, "x");

            Assert.AreSame(keyless, GrowIOC.GetService<IFoo>());
            Assert.AreSame(keyed, GrowIOC.GetService<IFoo>("x"));
        }

        [Test]
        public void DistinctServiceTypes_DoNotCollide() {
            var foo = new Foo();
            var bar = new Bar();
            GrowIOC.AddSingleton<IFoo>(foo);
            GrowIOC.AddUnique<IBar>(bar, "k");

            Assert.AreSame(foo, GrowIOC.GetService<IFoo>());
            Assert.AreSame(bar, GrowIOC.GetService<IBar>("k"));
            Assert.IsNull(GrowIOC.GetService<IBar>());
        }

        [Test]
        public void DuplicateRegistration_ReplacesPrevious() {
            var first = new Foo();
            var second = new Foo();
            GrowIOC.AddSingleton<IFoo>(first);
            GrowIOC.AddSingleton<IFoo>(second);

            Assert.AreSame(second, GrowIOC.GetService<IFoo>());
        }

        [Test]
        public void DuplicateInjectSingleton_ResetsCachedInstance() {
            GrowIOC.InjectSingleton<IFoo, CountingFooFactory>();
            var first = GrowIOC.GetService<IFoo>();

            GrowIOC.InjectSingleton<IFoo, CountingFooFactory>();
            var second = GrowIOC.GetService<IFoo>();

            Assert.AreNotSame(first, second);
            Assert.AreEqual(2, CountingFooFactory.Calls);
        }

        [Test]
        public void Remove_Keyless_ReturnsTrueThenFalse() {
            GrowIOC.AddSingleton<IFoo>(new Foo());

            Assert.IsTrue(GrowIOC.Remove<IFoo>());
            Assert.IsFalse(GrowIOC.Contains<IFoo>());
            Assert.IsFalse(GrowIOC.Remove<IFoo>());
        }

        [Test]
        public void Remove_Keyed_OnlyRemovesThatKey() {
            GrowIOC.AddUnique<IFoo>(new Foo(), "a");
            GrowIOC.AddUnique<IFoo>(new Foo(), "b");

            Assert.IsTrue(GrowIOC.Remove<IFoo>("a"));
            Assert.IsFalse(GrowIOC.Contains<IFoo>("a"));
            Assert.IsTrue(GrowIOC.Contains<IFoo>("b"));
        }

        [Test]
        public void Clear_RemovesEverything() {
            GrowIOC.AddSingleton<IFoo>(new Foo());
            GrowIOC.AddUnique<IBar>(new Bar(), "b");

            GrowIOC.Clear();

            Assert.IsFalse(GrowIOC.Contains<IFoo>());
            Assert.IsFalse(GrowIOC.Contains<IBar>("b"));
            Assert.IsNull(GrowIOC.GetService<IFoo>());
        }

        [Test]
        public void GetService_Hit_ReturnsInstance() {
            var foo = new Foo();
            GrowIOC.AddSingleton<IFoo>(foo);

            Assert.AreSame(foo, GrowIOC.GetService<IFoo>());
        }

        [Test]
        public void GetService_Miss_ReturnsNull() {
            Assert.IsNull(GrowIOC.GetService<IFoo>());
        }

        [Test]
        public void GetRequiredService_Miss_ThrowsServiceNotFound() {
            var exception = Assert.Throws<ServiceNotFoundException>(() => GrowIOC.GetRequiredService<IFoo>());
            Assert.AreEqual(typeof(IFoo), exception.ServiceType);

            var keyed = Assert.Throws<ServiceNotFoundException>(
                () => GrowIOC.GetRequiredService<IFoo>("missing"));
            Assert.AreEqual("missing", keyed.Key);
        }

        [Test]
        public void AddSingleton_NullInstance_Throws() {
            Assert.Throws<ArgumentNullException>(() => GrowIOC.AddSingleton<IFoo>(null));
            Assert.IsFalse(GrowIOC.Contains<IFoo>());
        }

        [Test]
        public void ConcurrentResolve_Singleton_CreatesSingleInstance() {
            GrowIOC.InjectSingleton<IFoo, CountingFooFactory>();

            var results = new ConcurrentBag<IFoo>();
            Parallel.For(0, 128, _ => results.Add(GrowIOC.GetService<IFoo>()));

            Assert.AreEqual(1, CountingFooFactory.Calls);
            Assert.AreEqual(1, results.Distinct().Count());
        }

        [Test]
        public void ResetStaticState_DropsTheRoot() {
            GrowIOC.AddSingleton<IFoo>(new Foo());

            GrowIOC.ResetStaticState();

            Assert.IsFalse(GrowIOC.Contains<IFoo>());
        }
    }
}
