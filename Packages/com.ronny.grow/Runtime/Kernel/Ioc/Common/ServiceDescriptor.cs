// SPDX-License-Identifier: MIT

using System;

namespace Grow.Kernel.Ioc.Common {
    /// <summary>One registration: identity plus lifetime-specific creation and caching.</summary>
    internal abstract class ServiceDescriptor {
        protected ServiceDescriptor(ServiceKey key) {
            Key = key;
        }

        public ServiceKey Key { get; }
        public abstract LifeTime LifeTime { get; }
        public abstract object Get();

        public override string ToString() {
            return $"{LifeTime}:{Key}";
        }
    }

    /// <summary>Transient: the factory runs on every resolution.</summary>
    internal sealed class TransientServiceDescriptor : ServiceDescriptor {
        private readonly Func<string, object> _factory;

        public TransientServiceDescriptor(ServiceKey key, Func<string, object> factory) : base(key) {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public override LifeTime LifeTime => LifeTime.Transient;

        public override object Get() {
            return _factory(Key.Key);
        }
    }

    /// <summary>Singleton: one cached instance, created under a double-checked lock.</summary>
    internal sealed class SingletonServiceDescriptor : ServiceDescriptor {
        private readonly object _sync = new object();
        private readonly Func<string, object> _factory;
        private volatile object _instance;

        public SingletonServiceDescriptor(ServiceKey key, Func<string, object> factory) : base(key) {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public SingletonServiceDescriptor(ServiceKey key, object instance) : base(key) {
            _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        }

        public override LifeTime LifeTime => LifeTime.Singleton;

        public override object Get() {
            var instance = _instance;
            if (instance != null) {
                return instance;
            }

            lock (_sync) {
                if (_instance == null) {
                    _instance = _factory(Key.Key);
                }

                return _instance;
            }
        }
    }
}
