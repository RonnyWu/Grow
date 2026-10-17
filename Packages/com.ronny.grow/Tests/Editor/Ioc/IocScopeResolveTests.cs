// SPDX-License-Identifier: MIT

using Grow.Kernel.Ioc;
using Grow.Kernel.Ioc.Common;
using Grow.Kernel.Ioc.Interface;
using NUnit.Framework;
using GrowIOC = Grow.Kernel.Ioc.IOC;

namespace Grow.Tests.Editor.Ioc {
    [Category("Grow.Ioc")]
    public class IocScopeResolveTests : IocTestBase {
        [Test]
        public void Resolve_Hit_ReturnsRegisteredInstance() {
            var collection = new ServiceCollection();
            var foo = new Foo();
            collection.AddSingleton<IFoo>(foo);

            Assert.AreSame(foo, collection.GetService<IFoo>());
        }

        [Test]
        public void Resolve_Miss_ReturnsNullWithoutThrowing() {
            var collection = new ServiceCollection();

            IFoo resolved = null;
            Assert.DoesNotThrow(() => resolved = collection.GetService<IFoo>());
            Assert.IsNull(resolved);
        }

        [Test]
        public void Resolve_KeyedMiss_DoesNotFallbackToKeyless() {
            var collection = new ServiceCollection();
            collection.AddSingleton<IFoo>(new Foo());

            Assert.IsNull(collection.GetService<IFoo>("missing"));
            Assert.IsNotNull(collection.GetService<IFoo>());
        }

        [Test]
        public void ChildScope_ResolvesParentRegistration() {
            var root = new ServiceCollection();
            var foo = new Foo();
            root.AddSingleton<IFoo>(foo);

            var child = root.CreateScope();

            Assert.AreSame(foo, child.GetService<IFoo>());
            Assert.IsTrue(child.Contains<IFoo>());
        }

        [Test]
        public void ParentScope_CannotResolveChildRegistration() {
            var root = new ServiceCollection();
            var child = root.CreateScope();
            var bar = new Bar();
            child.AddSingleton<IBar>(bar);

            Assert.AreSame(bar, child.GetService<IBar>());
            Assert.IsFalse(root.Contains<IBar>());
            Assert.IsNull(root.GetService<IBar>());
        }

        [Test]
        public void CreateScope_RegistrationDoesNotWriteParent() {
            var root = new ServiceCollection();
            var child = root.CreateScope();
            var foo = new Foo();
            child.AddSingleton<IFoo>(foo);

            Assert.IsFalse(root.Contains<IFoo>());
            Assert.AreSame(foo, child.GetService<IFoo>());
        }

        [Test]
        public void ChildRegistration_ShadowsParent_ChildWins() {
            var root = new ServiceCollection();
            var rootFoo = new Foo();
            root.AddSingleton<IFoo>(rootFoo);

            var child = root.CreateScope();
            var childFoo = new Foo();
            child.AddSingleton<IFoo>(childFoo);

            Assert.AreSame(childFoo, child.GetService<IFoo>());
            Assert.AreSame(rootFoo, root.GetService<IFoo>());
        }

        [Test]
        public void Remove_OnChild_DoesNotRemoveParent() {
            var root = new ServiceCollection();
            var rootFoo = new Foo();
            root.AddSingleton<IFoo>(rootFoo);

            var child = root.CreateScope();

            Assert.IsFalse(child.Remove<IFoo>());
            Assert.AreSame(rootFoo, child.GetService<IFoo>());
            Assert.IsTrue(root.Contains<IFoo>());
        }

        [Test]
        public void ChildClear_DoesNotClearParent() {
            var root = new ServiceCollection();
            root.AddSingleton<IFoo>(new Foo());
            var child = root.CreateScope();
            child.AddSingleton<IBar>(new Bar());

            child.Clear();

            Assert.IsFalse(child.Contains<IBar>());
            Assert.IsTrue(root.Contains<IFoo>());
            Assert.IsNotNull(child.GetService<IFoo>());
        }

        [Test]
        public void ChildScope_Miss_GetServiceStillThrows() {
            var root = new ServiceCollection();
            var child = root.CreateScope();

            var exception = Assert.Throws<ServiceNotFoundException>(() => child.GetRequiredService<IFoo>());
            Assert.AreEqual(typeof(IFoo), exception.ServiceType);

            var keyed = Assert.Throws<ServiceNotFoundException>(
                () => child.GetRequiredService<IFoo>("missing"));
            Assert.AreEqual("missing", keyed.Key);
        }

        [Test]
        public void IsolatedContainer_IndependentFromGlobalIOC() {
            var local = new ServiceCollection();
            var localFoo = new Foo();
            local.AddSingleton<IFoo>(localFoo);

            Assert.IsTrue(local.Contains<IFoo>());
            Assert.IsFalse(GrowIOC.Contains<IFoo>());
            Assert.IsNull(GrowIOC.GetService<IFoo>());

            var globalFoo = new Foo();
            GrowIOC.AddSingleton<IFoo>(globalFoo);

            Assert.AreSame(globalFoo, GrowIOC.GetService<IFoo>());
            Assert.AreSame(localFoo, local.GetService<IFoo>());
        }

        [Test]
        public void IOCCreateScope_UsesRootAsParent() {
            var scope = GrowIOC.CreateScope();

            var rootFoo = new Foo();
            GrowIOC.AddSingleton<IFoo>(rootFoo);
            Assert.AreSame(rootFoo, scope.GetService<IFoo>());

            var scopedBar = new Bar();
            scope.AddSingleton<IBar>(scopedBar);
            Assert.IsFalse(GrowIOC.Contains<IBar>());
            Assert.AreSame(scopedBar, scope.GetService<IBar>());
        }

        [Test]
        public void CreateScope_Nested_GrandchildResolvesRootRegistration() {
            var root = new ServiceCollection();
            var rootFoo = new Foo();
            root.AddSingleton<IFoo>(rootFoo);

            var child = root.CreateScope();
            var grandchild = child.CreateScope();

            Assert.AreSame(rootFoo, grandchild.GetService<IFoo>());
            Assert.IsTrue(grandchild.Contains<IFoo>());
        }

        [Test]
        public void CreateScope_Nested_NearestRegistrationWins() {
            var root = new ServiceCollection();
            root.AddSingleton<IFoo>(new Foo());

            var child = root.CreateScope();
            var childFoo = new Foo();
            child.AddSingleton<IFoo>(childFoo);

            var grandchild = child.CreateScope();

            Assert.AreSame(childFoo, grandchild.GetService<IFoo>());
        }

        [Test]
        public void Scope_KeyLookup_DoesNotFallbackToKeylessParent() {
            var root = new ServiceCollection();
            root.AddSingleton<IFoo>(new Foo());

            var child = root.CreateScope();

            Assert.IsNull(child.GetService<IFoo>("missing"));
            Assert.IsFalse(child.Contains<IFoo>("missing"));
        }

        [Test]
        public void Scope_KeylessLookup_DoesNotFallbackToKeyedParent() {
            var root = new ServiceCollection();
            root.AddUnique<IFoo>(new Foo(), "x");

            var child = root.CreateScope();

            Assert.IsNull(child.GetService<IFoo>());
            Assert.IsFalse(child.Contains<IFoo>());
            Assert.IsNotNull(child.GetService<IFoo>("x"));
        }
    }
}
