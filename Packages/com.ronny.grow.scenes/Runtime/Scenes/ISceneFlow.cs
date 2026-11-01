// SPDX-License-Identifier: MIT

using UnityEngine.SceneManagement;

namespace Grow.Scenes {
    /// <summary>
    /// The scene flow of a running game: load, activate and unload scenes through one path.
    /// </summary>
    /// <remarks>
    /// Resolve it from the service registry after <see cref="SceneModule"/> is registered. Every
    /// operation is asynchronous and is reported on a <see cref="SceneRequest"/>; the flow never
    /// throws for a failed load, so a caller can show the error instead of unwinding gameplay.
    /// <para>
    /// Only one foreground operation runs at a time: a second <see cref="Load"/> while
    /// <see cref="IsBusy"/> throws, because overlapping scene loads make activation order
    /// unpredictable. Queuing, priorities and preloading are deliberately not provided.
    /// </para>
    /// <para>
    /// Unity cannot abort a scene operation. <see cref="Unload"/> of a request that is still loading
    /// is therefore applied when that load completes: the scene is activated (when asked) and then
    /// unloaded. There is no <c>Cancel</c>, and no API pretends there is one.
    /// </para>
    /// <para>
    /// Transitions, loading screens and addressable scene lookup belong to the game or to another
    /// capability, not here.
    /// </para>
    /// </remarks>
    public interface ISceneFlow {
        /// <summary>True while a foreground operation has not finished yet.</summary>
        bool IsBusy { get; }

        /// <summary>Name of the scene Unity currently treats as active.</summary>
        string ActiveScene { get; }

        /// <summary>Starts loading a scene; throws while another operation is in flight.</summary>
        /// <param name="sceneName">Scene name or path accepted by <c>SceneManager</c>.</param>
        /// <param name="mode">Single replaces the visible scene; additive keeps the current one.</param>
        /// <returns>A request that reports progress and completion.</returns>
        SceneRequest Load(string sceneName, LoadSceneMode mode = LoadSceneMode.Single);

        /// <summary>Starts loading a scene without unloading the current one.</summary>
        SceneRequest LoadAdditive(string sceneName);

        /// <summary>
        /// Unloads the scene behind a finished request, or marks a loading one for unloading when it
        /// completes.
        /// </summary>
        SceneRequest Unload(SceneRequest request);

        /// <summary>Makes a finished additive scene the active one; false when it is not ready.</summary>
        bool SetActive(SceneRequest request);
    }
}
