// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Collections.Generic;
using Grow.Kernel.Entry;
using Grow.Kernel.Modules;
using Grow.Scenes;
using Grow.Store;
using Grow.Store.Contracts;

namespace Grow.Samples.VerticalSlice {
    /// <summary>
    /// The composition of the vertical slice: one game module, one session module and the two
    /// capabilities, assembled with public API only.
    /// </summary>
    /// <remarks>
    /// This is the shape a game uses: the framework is not asked for anything beyond
    /// <see cref="GrowBuilder"/>, <see cref="IModule"/> and the capability extension methods.
    /// </remarks>
    internal static class SliceEntry {
        internal static GrowHost Start(string saveName) {
            return new GrowBuilder()
                .AddModule(new SliceGameModule())
                .AddModule(new SliceSessionModule(saveName))
                .UseStore()
                .UseScenes()
                .Build();
        }
    }

    /// <summary>Loads "configuration" over two frames, then starts gameplay.</summary>
    internal sealed class SliceGameModule : IModule, IBootLoadable {
        private const int LoadFrames = 2;

        private int _frames;

        public string Name => "Slice.Game";

        public int Priority => 0;

        public IReadOnlyList<Type> Dependencies => Array.Empty<Type>();

        public bool IsLoaded { get; private set; }

        public float LoadProgress => _frames / (float)LoadFrames;

        public void OnRegister(IModuleContext context) {
            context.Register(this);
        }

        public void OnInitialize(IModuleContext context) {
        }

        public void OnStart(IModuleContext context) {
            IsLoaded = true;
        }

        public void OnStop() {
            IsLoaded = false;
        }

        public void OnDispose() {
        }

        public IEnumerator Load(IModuleContext context) {
            _frames = 0;
            for (var i = 0; i < LoadFrames; i++) {
                _frames++;
                yield return null;
            }
        }
    }

    /// <summary>Owns the session coins and persists them through the store capability.</summary>
    internal sealed class SliceSessionModule : IModule {
        private const string CoinsKey = "coins";

        private readonly string _storeName;
        private IStore _store;

        internal SliceSessionModule(string storeName) {
            _storeName = storeName;
        }

        public string Name => "Slice.Session";

        public int Priority => -10;

        public IReadOnlyList<Type> Dependencies => new[] { typeof(SliceGameModule) };

        public int Coins { get; private set; }

        public void OnRegister(IModuleContext context) {
            context.Register(this);
        }

        public void OnInitialize(IModuleContext context) {
            var provider = context.GetRequiredService<IStoreProvider>();
            _store = provider.GetStore(_storeName, StoreType.Runtime);
            Coins = _store.Get(CoinsKey, 0);
        }

        public void OnStart(IModuleContext context) {
        }

        public void OnStop() {
            Save();
        }

        public void OnDispose() {
        }

        public void AddCoins(int amount) {
            if (amount <= 0) {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            Coins += amount;
        }

        public void Save() {
            _store?.Set(CoinsKey, Coins).Save();
        }
    }
}
