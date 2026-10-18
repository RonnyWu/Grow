// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Grow.Core.Log;
using Grow.Kernel.Modules;
using Grow.Store.Contracts;
using Grow.Store.Internal;

namespace Grow.Store {
    /// <summary>Integrates the store capability with the kernel.</summary>
    /// <remarks>
    /// The module owns an <see cref="IStoreProvider"/>, registers it and a default
    /// <see cref="IStore"/>, and saves every healthy dirty store on stop. It also exposes the
    /// optional <see cref="IStoreDiagnostics"/> surface. A second provider for the same scope is
    /// rejected when it first opens that scope.
    /// </remarks>
    public sealed class StoreModule : IModule {
        /// <summary>Default name of the store registered as <see cref="IStore"/>.</summary>
        public const string DefaultName = "settings";

        /// <summary>Default module name.</summary>
        public const string DefaultModuleName = "Store";

        private const string LogCategory = "Store";

        private readonly string _defaultStoreName;
        private readonly StoreType _defaultStoreType;
        private readonly string _moduleName;
        private readonly StoreOptions _options;
        private readonly bool _ownsProvider;
        private IStoreProvider _provider;
        private bool _disposed;

        /// <summary>Creates the module with the default store and options.</summary>
        public StoreModule(StoreOptions options = null)
            : this(DefaultName, StoreType.Runtime, null, DefaultModuleName, options) {
        }

        /// <summary>Creates the module.</summary>
        /// <param name="defaultStoreName">Name of the store exposed as <see cref="IStore"/>.</param>
        /// <param name="defaultStoreType">Placement of that default store.</param>
        /// <param name="provider">Provider to own; a file provider is created when omitted.</param>
        /// <param name="moduleName">Unique module name.</param>
        /// <param name="options">File backend options; used when the module creates its provider.</param>
        public StoreModule(
            string defaultStoreName,
            StoreType defaultStoreType = StoreType.Runtime,
            IStoreProvider provider = null,
            string moduleName = DefaultModuleName,
            StoreOptions options = null) {
            if (string.IsNullOrWhiteSpace(defaultStoreName)) {
                throw new ArgumentNullException(nameof(defaultStoreName));
            }

            if (string.IsNullOrWhiteSpace(moduleName)) {
                throw new ArgumentNullException(nameof(moduleName));
            }

            _defaultStoreName = defaultStoreName;
            _defaultStoreType = defaultStoreType;
            _moduleName = moduleName;
            _options = options ?? new StoreOptions();
            _options.Validate();
            _ownsProvider = provider == null;
            _provider = provider ?? new StoreProvider(_options);
        }

        /// <inheritdoc />
        public string Name => _moduleName;

        /// <inheritdoc />
        public int Priority => 1000;

        /// <inheritdoc />
        public IReadOnlyList<Type> Dependencies => Array.Empty<Type>();

        /// <summary>Diagnostics surface, or null when an injected provider does not implement it.</summary>
        public IStoreDiagnostics Diagnostics => _provider as IStoreDiagnostics;

        /// <inheritdoc />
        public void OnRegister(IModuleContext context) {
            if (_disposed) {
                if (!_ownsProvider) {
                    throw new InvalidOperationException(
                        "[Store] The injected IStoreProvider was disposed by the previous session; " +
                        "create a new StoreModule or inject a provider factory.");
                }

                _provider = new StoreProvider(_options);
                _disposed = false;
            }

            context.Register<IStoreProvider>(_provider);
            context.RegisterLazy<IStore>(() => context.GetRequiredService<IStoreProvider>()
                .GetStore(_defaultStoreName, _defaultStoreType));
        }

        /// <inheritdoc />
        public void OnInitialize(IModuleContext context) {
        }

        /// <inheritdoc />
        public void OnStart(IModuleContext context) {
        }

        /// <inheritdoc />
        public void OnStop() {
            if (!_provider.TrySaveAll(out var error)) {
                GrowLog.Error($"[Store] Shutdown could not save every store: {error}", LogCategory);
            }
        }

        /// <inheritdoc />
        public void OnDispose() {
            _provider.Dispose();
            _disposed = true;
        }
    }
}
