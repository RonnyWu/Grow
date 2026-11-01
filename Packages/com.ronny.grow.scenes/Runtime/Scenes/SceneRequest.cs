// SPDX-License-Identifier: MIT

using Grow.Scenes.Impl;

namespace Grow.Scenes {
    /// <summary>Handle to one scene operation started by <see cref="ISceneFlow"/>.</summary>
    /// <remarks>
    /// The request is a live view: <see cref="IsDone"/>, <see cref="Progress"/> and
    /// <see cref="IsFaulted"/> change as the operation runs. The default value is
    /// <see cref="None"/> and is never valid.
    /// <para>
    /// A faulted request carries a readable <see cref="Error"/>; the flow does not throw for it
    /// because a missing scene is a content problem the game should display, not an exception to
    /// unwind.
    /// </para>
    /// </remarks>
    public readonly struct SceneRequest {
        private readonly SceneOperation _operation;

        internal SceneRequest(SceneOperation operation) {
            _operation = operation;
        }

        /// <summary>A request that refers to no operation.</summary>
        public static SceneRequest None => default;

        /// <summary>True when the request was issued by an <see cref="ISceneFlow"/>.</summary>
        public bool IsValid => _operation != null;

        /// <summary>True once the operation finished successfully.</summary>
        public bool IsDone => _operation != null && _operation.IsDone;

        /// <summary>True when the operation failed; see <see cref="Error"/>.</summary>
        public bool IsFaulted => _operation != null && _operation.IsFaulted;

        /// <summary>Reported progress in the 0..1 range, 0 while unavailable.</summary>
        public float Progress => _operation?.Progress ?? 0f;

        /// <summary>Scene name the operation targets, or an empty string for <see cref="None"/>.</summary>
        public string Name => _operation?.SceneName ?? string.Empty;

        /// <summary>Failure message, or <c>null</c> when the operation is healthy.</summary>
        public string Error => _operation?.Error;

        internal SceneOperation Operation => _operation;
    }
}
