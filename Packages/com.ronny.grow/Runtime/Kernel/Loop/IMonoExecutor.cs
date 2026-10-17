// SPDX-License-Identifier: MIT

using System.Collections;
using Grow.Core.Events;
using UnityEngine;

namespace Grow.Kernel.Loop {
    /// <summary>
    /// Engine-side host contract consumed by the lifecycle layer.
    /// </summary>
    /// <remarks>
    /// The concrete host is a <see cref="MonoBehaviour"/> created by <see cref="GrowLoop"/>. Keeping
    /// this as an interface lets the lifecycle layer depend on the host contract instead of the
    /// PlayerLoop implementation details.
    /// </remarks>
    internal interface IMonoExecutor {
        /// <summary>Raised once when the application is quitting.</summary>
        GrowEvent AppQuit { get; }

        /// <summary>Raised before scene scripts update.</summary>
        GrowEvent PreUpdate { get; }

        /// <summary>Raised after the fixed-update group.</summary>
        GrowEvent FixedUpdate { get; }

        /// <summary>Raised after scene scripts update.</summary>
        GrowEvent Update { get; }

        /// <summary>Raised after the whole update group, before late update.</summary>
        GrowEvent PostUpdate { get; }

        /// <summary>Raised after scene scripts late-update.</summary>
        GrowEvent LateUpdate { get; }

        /// <summary>Raised when the application gains or loses focus.</summary>
        GrowEvent<bool> AppFocus { get; }

        /// <summary>Raised when the application pauses or resumes.</summary>
        GrowEvent<bool> AppPause { get; }

        /// <summary>True while the underlying host object is alive.</summary>
        bool Valid { get; }

        /// <summary>Starts a coroutine on the host.</summary>
        Coroutine StartCoroutine(IEnumerator routine);

        /// <summary>Stops a coroutine started by this host.</summary>
        void StopCoroutine(Coroutine coroutine);
    }
}
