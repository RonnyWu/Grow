// SPDX-License-Identifier: MIT

using System.Threading;
using Grow.Kernel.Ioc;
using Grow.Kernel.Ioc.Interface;
using NUnit.Framework;

namespace Grow.Tests.Editor.Ioc {
    public interface IFoo {
        int Id { get; }
    }

    public sealed class Foo : IFoo {
        private static int _sequence;

        public int Id { get; } = Interlocked.Increment(ref _sequence);
    }

    public interface IBar {
    }

    public sealed class Bar : IBar {
    }

    public sealed class CountingFooFactory : IServiceFactory<IFoo> {
        public static int Calls;

        public IFoo GetService(string key) {
            Interlocked.Increment(ref Calls);
            return new Foo();
        }
    }

    public sealed class BarFactory : IServiceFactory<IBar> {
        public IBar GetService(string key) {
            return new Bar();
        }
    }

    public abstract class IocTestBase {
        [SetUp]
        public void IocBaseSetUp() {
            IOC.Clear();
            CountingFooFactory.Calls = 0;
        }

        [TearDown]
        public void IocBaseTearDown() {
            IOC.Clear();
        }
    }
}
