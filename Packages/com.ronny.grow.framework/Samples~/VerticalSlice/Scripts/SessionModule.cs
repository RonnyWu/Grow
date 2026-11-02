// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Grow.Kernel.Modules;
using Grow.Store.Contracts;

namespace Grow.Samples.VerticalSlice {
    /// <summary>
    /// Owns the session coins and persists them through the store capability.
    /// </summary>
    /// <remarks>
    /// The module owns its own store, so the sample never touches another module's data. The coin
    /// count is read during <see cref="OnInitialize"/> and written by <see cref="Save"/>; the module
    /// also saves on stop, which is what makes a Play Mode restart keep the value.
    /// </remarks>
    public sealed class SessionModule : IModule {
        private const string CoinsKey = "coins";

        private readonly string _storeName;
        private IStore _store;

        /// <summary>Creates the module over a named store.</summary>
        /// <param name="storeName">Store name; use one name per save file.</param>
        public SessionModule(string storeName) {
            if (string.IsNullOrWhiteSpace(storeName)) {
                throw new ArgumentNullException(nameof(storeName));
            }

            _storeName = storeName;
        }

        /// <inheritdoc />
        public string Name => "Sample.Session";

        /// <inheritdoc />
        public int Priority => -10;

        /// <inheritdoc />
        public IReadOnlyList<Type> Dependencies => new[] { typeof(GameModule) };

        /// <summary>Persisted coins, restored when the module initializes.</summary>
        public int Coins { get; private set; }

        /// <inheritdoc />
        public void OnRegister(IModuleContext context) {
            context.Register(this);
        }

        /// <inheritdoc />
        public void OnInitialize(IModuleContext context) {
            var provider = context.GetRequiredService<IStoreProvider>();
            _store = provider.GetStore(_storeName, StoreType.Runtime);
            Coins = _store.Get(CoinsKey, 0);
        }

        /// <inheritdoc />
        public void OnStart(IModuleContext context) {
        }

        /// <inheritdoc />
        public void OnStop() {
            Save();
        }

        /// <inheritdoc />
        public void OnDispose() {
        }

        /// <summary>Adds coins to the session; the change is persisted by <see cref="Save"/>.</summary>
        public void AddCoins(int amount) {
            if (amount <= 0) {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            Coins += amount;
        }

        /// <summary>Writes the current coins to the store.</summary>
        public void Save() {
            _store?.Set(CoinsKey, Coins).Save();
        }
    }
}
