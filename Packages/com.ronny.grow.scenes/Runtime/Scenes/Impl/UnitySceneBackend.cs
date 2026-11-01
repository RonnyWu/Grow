// SPDX-License-Identifier: MIT

using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Grow.Scenes.Impl {
    /// <summary>The production backend: a thin pass-through to <see cref="SceneManager"/>.</summary>
    internal sealed class UnitySceneBackend : ISceneBackend {
        public string ActiveSceneName => SceneManager.GetActiveScene().name;

        public ISceneHandle Load(string sceneName, LoadSceneMode mode) {
            var operation = SceneManager.LoadSceneAsync(sceneName, mode);
            if (operation == null) {
                throw new InvalidOperationException(
                    $"Scene '{sceneName}' could not be loaded; check that it is in the build settings.");
            }

            return new UnitySceneHandle(operation);
        }

        public ISceneHandle Unload(string sceneName) {
            var operation = SceneManager.UnloadSceneAsync(sceneName);
            if (operation == null) {
                throw new InvalidOperationException(
                    $"Scene '{sceneName}' could not be unloaded; it may not be loaded.");
            }

            return new UnitySceneHandle(operation);
        }

        public bool SetActive(string sceneName) {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded) {
                return false;
            }

            return SceneManager.SetActiveScene(scene);
        }

        private sealed class UnitySceneHandle : ISceneHandle {
            private readonly AsyncOperation _operation;

            internal UnitySceneHandle(AsyncOperation operation) {
                _operation = operation;
            }

            public bool IsDone => _operation.isDone;

            public float Progress => _operation.progress;
        }
    }
}
