// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Grow.Kernel.Modules;
using Grow.Scenes.Impl;

namespace Grow.Scenes {
    /// <summary>Registers the scene flow as <see cref="ISceneFlow"/>.</summary>
    /// <remarks>
    /// The module owns no scene of its own; it only exposes the flow. Pass an initial scene to load
    /// it on start, or leave it null and drive the flow from gameplay.
    /// <para>
    /// The scene capability ships without a contracts package on purpose: there is exactly one
    /// backend (Unity's <c>SceneManager</c>), so the "two real, interchangeable backends" rule does
    /// not hold and no abstraction is introduced. If a second backend ever appears, that is the
    /// signal to split the public surface into a contract package.
    /// </para>
    /// </remarks>
    public sealed class SceneModule : IModule {
        /// <summary>Default module name.</summary>
        public const string DefaultModuleName = "Scenes";

        private readonly ISceneFlow _flow;
        private readonly string _initialScene;

        /// <summary>Creates the module, optionally loading one scene on start.</summary>
        /// <param name="initialScene">Scene loaded when the module starts; null loads nothing.</param>
        public SceneModule(string initialScene = null) : this(initialScene, null) {
        }

        internal SceneModule(string initialScene, ISceneFlow flow) {
            _initialScene = string.IsNullOrWhiteSpace(initialScene) ? null : initialScene;
            _flow = flow ?? new SceneFlow();
        }

        /// <inheritdoc />
        public string Name => DefaultModuleName;

        /// <inheritdoc />
        public int Priority => 500;

        /// <inheritdoc />
        public IReadOnlyList<Type> Dependencies => Array.Empty<Type>();

        /// <summary>The flow this module registers.</summary>
        public ISceneFlow Flow => _flow;

        /// <inheritdoc />
        public void OnRegister(IModuleContext context) {
            context.Register<ISceneFlow>(_flow);
        }

        /// <inheritdoc />
        public void OnInitialize(IModuleContext context) {
        }

        /// <inheritdoc />
        public void OnStart(IModuleContext context) {
            if (_initialScene == null) {
                return;
            }

            _flow.Load(_initialScene);
        }

        /// <inheritdoc />
        public void OnStop() {
            if (_flow is SceneFlow flow) {
                flow.Stop();
            }
        }

        /// <inheritdoc />
        public void OnDispose() {
            OnStop();
        }
    }
}
