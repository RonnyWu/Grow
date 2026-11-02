// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Collections.Generic;
using Grow.Kernel.Modules;

namespace Grow.Samples.VerticalSlice {
    /// <summary>
    /// Loads "configuration" over two frames, then starts gameplay.
    /// </summary>
    /// <remarks>
    /// This is the smallest useful <see cref="IBootLoadable"/>: the kernel keeps the boot in
    /// <c>Loading</c> until <see cref="Load"/> finishes, and no module's <c>OnStart</c> runs before
    /// that. Replace the loop with a real asynchronous operation and yield it directly.
    /// </remarks>
    public sealed class GameModule : IModule, IBootLoadable {
        private const int LoadFrames = 2;

        private int _frames;

        /// <inheritdoc />
        public string Name => "Sample.Game";

        /// <inheritdoc />
        public int Priority => 0;

        /// <inheritdoc />
        public IReadOnlyList<Type> Dependencies => Array.Empty<Type>();

        /// <summary>True once the load finished and the module started.</summary>
        public bool IsLoaded { get; private set; }

        /// <inheritdoc />
        public float LoadProgress => _frames / (float)LoadFrames;

        /// <inheritdoc />
        public void OnRegister(IModuleContext context) {
            context.Register(this);
        }

        /// <inheritdoc />
        public void OnInitialize(IModuleContext context) {
        }

        /// <inheritdoc />
        public void OnStart(IModuleContext context) {
            IsLoaded = true;
        }

        /// <inheritdoc />
        public void OnStop() {
            IsLoaded = false;
        }

        /// <inheritdoc />
        public void OnDispose() {
        }

        /// <inheritdoc />
        public IEnumerator Load(IModuleContext context) {
            _frames = 0;
            for (var i = 0; i < LoadFrames; i++) {
                _frames++;
                yield return null;
            }
        }
    }
}
