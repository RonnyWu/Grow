// SPDX-License-Identifier: MIT

namespace Grow.Kernel.Lifecycle {
    /// <summary>
    /// Unified lifecycle contract for MonoBehaviours and plain C# objects.
    /// </summary>
    /// <remarks>
    /// The object supplies its <see cref="LifecycleManager"/> and the manager supplies the phase
    /// events. The default <see cref="AttachApp"/> / <see cref="DetachApp"/> implementations delegate
    /// to that manager, which owns the subscription tokens; this keeps the interface free of mutable
    /// per-instance state and works with the token-based event primitive.
    /// <para>
    /// Attach calls the manager every time instead of short-circuiting on <see cref="AttachedApp"/>.
    /// The flag is a report of the last result, not the source of truth: after a host rebind or a
    /// session reset the manager can recreate the subscriptions without the target having to clear
    /// the flag first.
    /// </para>
    /// </remarks>
    public interface ILifecycle {
        /// <summary>Whether application-level subscriptions are currently attached.</summary>
        bool AttachedApp => LifecycleManager != null && LifecycleManager.IsAppAttached(this);

        /// <summary>Whether the updater subscriptions are currently attached.</summary>
        bool AttachedUpdater => false;

        /// <summary>Called once to initialize data and references.</summary>
        void OnAwake();

        /// <summary>Called when the object becomes enabled.</summary>
        void OnEnabled();

        /// <summary>Called on the first activation.</summary>
        void OnStart();

        /// <summary>Called at the fixed interval.</summary>
        void OnFixedUpdate();

        /// <summary>Called before scene scripts update.</summary>
        void OnPreUpdate();

        /// <summary>Called once per game frame.</summary>
        void OnUpdate();

        /// <summary>Called after the update group.</summary>
        void OnLateUpdate();

        /// <summary>
        /// Called after the whole update group and before late update. The empty default keeps
        /// existing implementations source-compatible; only objects declaring
        /// <see cref="UpdateFlag.PostUpdate"/> need to override it.
        /// </summary>
        void OnPostUpdate() {
        }

        /// <summary>Called when the application gains or loses focus.</summary>
        void OnAppFocus(bool hasFocus);

        /// <summary>Called when the application pauses or resumes.</summary>
        void OnAppPause(bool isPause);

        /// <summary>Called while the application is quitting.</summary>
        void OnAppQuit();

        /// <summary>Called when the object becomes disabled.</summary>
        void OnDisabled();

        /// <summary>Called when the object is destroyed.</summary>
        void OnDestroyed();

        /// <summary>The manager that owns this object's subscriptions.</summary>
        ILifecycleManager LifecycleManager { get; }

        /// <summary>
        /// Attaches quit, focus and pause callbacks through <see cref="LifecycleManager"/>.
        /// </summary>
        /// <remarks>
        /// Safe to call repeatedly; the manager de-duplicates by target. It never leaves a partial
        /// subscription behind when the host is unavailable, so calling it again after the host is
        /// bound completes the attachment.
        /// </remarks>
        void AttachApp() {
            var manager = LifecycleManager;
            if (manager != null) {
                manager.AttachApp(this);
            }
        }

        /// <summary>Releases the application callbacks; safe to call more than once.</summary>
        void DetachApp() {
            var manager = LifecycleManager;
            if (manager != null) {
                manager.DetachApp(this);
            }
        }
    }
}
