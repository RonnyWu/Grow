// SPDX-License-Identifier: MIT

using UnityEngine.SceneManagement;

namespace Grow.Scenes.Impl {
    /// <summary>The engine operations the flow needs; replaced by a fake in tests.</summary>
    /// <remarks>
    /// Internal and single-implementation: it exists so the flow can be tested without loading real
    /// scenes, not as a replaceable backend seam. A second real backend would be the signal to make
    /// it public, and that is not the case today.
    /// </remarks>
    internal interface ISceneBackend {
        string ActiveSceneName { get; }

        ISceneHandle Load(string sceneName, LoadSceneMode mode);

        ISceneHandle Unload(string sceneName);

        bool SetActive(string sceneName);
    }

    /// <summary>One in-flight engine scene operation.</summary>
    internal interface ISceneHandle {
        bool IsDone { get; }

        float Progress { get; }
    }
}
