// SPDX-License-Identifier: MIT

using Grow.Kernel.Scheduling;
using UnityEngine.SceneManagement;

namespace Grow.Scenes.Impl {
    /// <summary>One scene operation tracked by <see cref="SceneFlow"/>.</summary>
    internal sealed class SceneOperation {
        internal SceneOperation(string sceneName, LoadSceneMode mode) {
            SceneName = sceneName;
            Mode = mode;
        }

        internal string SceneName { get; }

        internal LoadSceneMode Mode { get; }

        internal bool IsUnload { get; set; }

        internal ISceneHandle Handle { get; set; }

        internal TimerHandle Timer { get; set; }

        internal bool IsDone { get; set; }

        internal bool IsFaulted { get; set; }

        internal string Error { get; set; }

        internal bool UnloadRequested { get; set; }

        internal float Progress => Handle?.Progress ?? 0f;
    }
}
