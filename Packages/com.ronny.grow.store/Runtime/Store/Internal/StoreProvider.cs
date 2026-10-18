// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Text;
using Grow.Core.Log;
using Grow.Store.Contracts;
using Grow.Store.Format;
using Grow.Store.Scope;

namespace Grow.Store.Internal {
    /// <summary>The file-backed provider; owns one scope per store type.</summary>
    internal sealed class StoreProvider : IStoreProvider, IStoreDiagnostics {
        private const string LogCategory = "Store";

        private readonly object _gate = new object();
        private readonly Dictionary<StoreType, StoreScope> _scopes =
            new Dictionary<StoreType, StoreScope>();
        private readonly StoreOptions _options;
        private readonly string _rootOverride;
        private bool _disposed;

        public StoreProvider() : this(null, null) {
        }

        public StoreProvider(StoreOptions options) : this(options, null) {
        }

        internal StoreProvider(StoreOptions options, string rootOverride) {
            _options = options ?? new StoreOptions();
            _options.Validate();
            _rootOverride = rootOverride;
        }

        public IStore GetStore(string storeName, StoreType storeType) {
            var name = StoreNames.Normalize(storeName);
            var scope = GetScope(storeType);
            return scope.GetStore(name);
        }

        public bool TryGetStore(string storeName, StoreType storeType, out IStore store) {
            var name = StoreNames.Normalize(storeName);
            lock (_gate) {
                if (_scopes.TryGetValue(storeType, out var scope) &&
                    scope.TryGetStore(name, out var file)) {
                    store = file;
                    return true;
                }
            }

            store = null;
            return false;
        }

        public bool Close(string storeName, StoreType storeType) {
            var name = StoreNames.Normalize(storeName);
            StoreScope scope;
            lock (_gate) {
                if (!_scopes.TryGetValue(storeType, out scope)) {
                    return false;
                }
            }

            return scope.Close(name);
        }

        public bool TrySaveAll(out string error) {
            List<StoreScope> scopes;
            lock (_gate) {
                scopes = new List<StoreScope>(_scopes.Values);
            }

            StringBuilder errors = null;
            for (var i = 0; i < scopes.Count; i++) {
                string scopeError;
                if (scopes[i].TrySaveAll(out scopeError)) {
                    continue;
                }

                errors ??= new StringBuilder();
                if (errors.Length > 0) {
                    errors.Append("; ");
                }

                errors.Append(scopeError);
            }

            if (errors == null) {
                error = null;
                return true;
            }

            error = errors.ToString();
            GrowLog.Error($"[Store] Failed to save every store: {error}", LogCategory);
            return false;
        }

        public bool DeleteStore(string storeName, StoreType storeType) {
            var name = StoreNames.Normalize(storeName);
            return GetScope(storeType).DeleteStore(name);
        }

        public bool IsSupported(StoreType storeType) {
#if UNITY_EDITOR
            return storeType == StoreType.Editor || storeType == StoreType.Runtime ||
                   storeType == StoreType.User;
#else
            return storeType == StoreType.Runtime || storeType == StoreType.User;
#endif
        }

        public bool TrySplit(string storeName, StoreType storeType, out string error) {
            var name = StoreNames.Normalize(storeName);
            return GetScope(storeType).TrySplit(name, out error);
        }

        public bool TryGc(string storeName, StoreType storeType, out string error) {
            var name = StoreNames.Normalize(storeName);
            return GetScope(storeType).TryGc(name, out error);
        }

        public bool TrySaveAllWithinBudget() {
            List<StoreScope> scopes;
            lock (_gate) {
                scopes = new List<StoreScope>(_scopes.Values);
            }

            var ok = true;
            for (var i = 0; i < scopes.Count; i++) {
                ok &= scopes[i].TrySaveAllWithinBudget();
            }

            return ok;
        }

        public void Dispose() {
            List<StoreScope> scopes;
            lock (_gate) {
                if (_disposed) {
                    return;
                }

                _disposed = true;
                scopes = new List<StoreScope>(_scopes.Values);
                _scopes.Clear();
            }

            for (var i = 0; i < scopes.Count; i++) {
                scopes[i].Dispose();
            }
        }

        private StoreScope GetScope(StoreType storeType) {
            if (!IsSupported(storeType)) {
                throw new NotSupportedException(
                    $"Store type '{storeType}' is not available on this platform.");
            }

            lock (_gate) {
                if (_disposed) {
                    throw new ObjectDisposedException(nameof(StoreProvider));
                }

                if (_scopes.TryGetValue(storeType, out var scope)) {
                    return scope;
                }

                var root = StoreScope.GetRootDirectory(storeType, _options, _rootOverride);
                if (storeType == StoreType.Runtime) {
                    StoreScope.MigrateSingleFileRuntimePath(root);
                }

                scope = new StoreScope(root, _options);
                _scopes.Add(storeType, scope);
                return scope;
            }
        }
    }
}
