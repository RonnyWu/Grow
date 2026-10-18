// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Grow.Kernel.Ioc.Common;
using Grow.Kernel.Ioc.Interface;

namespace Grow.Kernel.Ioc {
    /// <summary>Thread-safe service registry with keyed identities and parent scopes.</summary>
    /// <remarks>
    /// Identity is <see cref="ServiceKey"/> (service type + ordinal key) and matching is exact. A
    /// child scope resolves its own registrations first and then walks toward the root; registrations
    /// and removals never write to a parent. A child registration shadows the parent for the same
    /// identity, while the parent still resolves its own value.
    /// <para>
    /// Collection state is protected by an instance lock. Instance creation and caching are owned by
    /// the descriptor, so concurrent first resolution of a singleton creates one instance without
    /// holding the collection lock.
    /// </para>
    /// </remarks>
    public sealed class ServiceCollection : IServiceCollection {
        private readonly object _sync = new object();
        private readonly Dictionary<ServiceKey, ServiceDescriptor> _descriptors =
            new Dictionary<ServiceKey, ServiceDescriptor>();
        private readonly ServiceCollection _parent;

        /// <summary>Creates an isolated collection with no parent.</summary>
        public ServiceCollection() : this(null) {
        }

        /// <summary>Creates a child scope over <paramref name="parent"/>; null means isolated.</summary>
        internal ServiceCollection(ServiceCollection parent) {
            _parent = parent;
        }

        /// <inheritdoc />
        public T GetService<T>(string key = "") where T : class {
            return TryResolve(typeof(T), key, out var instance) ? (T)instance : null;
        }

        /// <inheritdoc />
        public T GetRequiredService<T>(string key = "") where T : class {
            if (TryResolve(typeof(T), key, out var instance) && instance != null) {
                return (T)instance;
            }

            throw new ServiceNotFoundException(typeof(T), key);
        }

        /// <inheritdoc />
        public bool Contains<T>(string key = "") where T : class {
            var serviceKey = new ServiceKey(typeof(T), key);
            lock (_sync) {
                if (_descriptors.ContainsKey(serviceKey)) {
                    return true;
                }
            }

            return _parent != null && _parent.Contains<T>(key);
        }

        /// <inheritdoc />
        public bool Remove<T>(string key = "") where T : class {
            lock (_sync) {
                return _descriptors.Remove(new ServiceKey(typeof(T), key));
            }
        }

        /// <inheritdoc />
        public void Clear() {
            lock (_sync) {
                _descriptors.Clear();
            }
        }

        /// <inheritdoc />
        public void AddSingleton<T>(T instance) where T : class {
            if (instance == null) {
                throw new ArgumentNullException(nameof(instance));
            }

            Register(new SingletonServiceDescriptor(
                new ServiceKey(typeof(T), string.Empty),
                instance));
        }

        /// <summary>Registers a keyless singleton factory.</summary>
        public void AddSingleton<T>(Func<T> factory) where T : class {
            if (factory == null) {
                throw new ArgumentNullException(nameof(factory));
            }

            Register(new SingletonServiceDescriptor(
                new ServiceKey(typeof(T), string.Empty),
                _ => factory()));
        }

        /// <summary>Removes a registration from this scope only (used by ownership adapters).</summary>
        internal bool Remove(Type type, string key) {
            lock (_sync) {
                return _descriptors.Remove(new ServiceKey(type, key));
            }
        }

        /// <inheritdoc />
        public void AddUnique<T>(T instance, string key = "") where T : class {
            if (instance == null) {
                throw new ArgumentNullException(nameof(instance));
            }
            // Keyed registration is a keyed singleton; "unique" is the historical API name.
            Register(new SingletonServiceDescriptor(
                new ServiceKey(typeof(T), key),
                instance));
        }

        /// <inheritdoc />
        public void InjectInstance<T, TFactory>()
            where T : class
            where TFactory : IServiceFactory<T>, new() {
            Register(new TransientServiceDescriptor(
                new ServiceKey(typeof(T), string.Empty),
                CreateFactory<T, TFactory>()));
        }

        /// <inheritdoc />
        public void InjectUnique<T, TFactory>(string key = "")
            where T : class
            where TFactory : IServiceFactory<T>, new() {
            // Keyed registration is a keyed singleton; "unique" is the historical API name.
            Register(new SingletonServiceDescriptor(
                new ServiceKey(typeof(T), key),
                CreateFactory<T, TFactory>()));
        }

        /// <inheritdoc />
        public void InjectSingleton<T, TFactory>()
            where T : class
            where TFactory : IServiceFactory<T>, new() {
            Register(new SingletonServiceDescriptor(
                new ServiceKey(typeof(T), string.Empty),
                CreateFactory<T, TFactory>()));
        }

        /// <inheritdoc />
        public IServiceCollection CreateScope() {
            return new ServiceCollection(this);
        }

        private static Func<string, object> CreateFactory<T, TFactory>()
            where T : class
            where TFactory : IServiceFactory<T>, new() {
            var factory = new TFactory();
            return key => factory.GetService(key);
        }

        private void Register(ServiceDescriptor descriptor) {
            lock (_sync) {
                _descriptors[descriptor.Key] = descriptor;
            }
        }

        private bool TryResolve(Type type, string key, out object instance) {
            ServiceDescriptor descriptor;
            lock (_sync) {
                _descriptors.TryGetValue(new ServiceKey(type, key), out descriptor);
            }

            if (descriptor == null) {
                if (_parent != null) {
                    return _parent.TryResolve(type, key, out instance);
                }

                instance = null;
                return false;
            }

            instance = descriptor.Get();
            return instance != null;
        }
    }
}
