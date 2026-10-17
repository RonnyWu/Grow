// SPDX-License-Identifier: MIT

namespace Grow.Kernel.Lifecycle {
    /// <summary>Initializes data and references after dependencies have been assembled.</summary>
    public interface IInitializable {
        /// <summary>Called once after dependency assembly.</summary>
        void OnInitialize();
    }

    /// <summary>Starts activation behaviour such as subscriptions or worker threads.</summary>
    public interface IStartable {
        /// <summary>Called when the object becomes active.</summary>
        void OnStart();
    }

    /// <summary>Receives an update before scene scripts update.</summary>
    public interface IPreUpdatable {
        /// <summary>Runs before scene scripts update.</summary>
        void OnPreUpdate();
    }

    /// <summary>Receives the per-frame update.</summary>
    public interface IUpdatable {
        /// <summary>Runs once per game frame.</summary>
        void OnUpdate();
    }

    /// <summary>Receives the fixed-interval physics update.</summary>
    public interface IFixedUpdatable {
        /// <summary>Runs at the fixed interval.</summary>
        void OnFixedUpdate();
    }

    /// <summary>Receives the update after the update group.</summary>
    public interface ILateUpdatable {
        /// <summary>Runs after the update group.</summary>
        void OnLateUpdate();
    }

    /// <summary>Receives the update after the whole update group and before late update.</summary>
    public interface IPostUpdatable {
        /// <summary>Runs after the update group, before late update.</summary>
        void OnPostUpdate();
    }

    /// <summary>Releases resources when a container or scope ends.</summary>
    /// <remarks>
    /// This is intentionally distinct from <see cref="System.IDisposable"/>: it describes the
    /// container/scope lifetime, not general resource ownership.
    /// </remarks>
    public interface IDeinitializable {
        /// <summary>Called while the container or scope releases objects in reverse order.</summary>
        void OnDeinitialize();
    }

    /// <summary>Observes application focus, pause and quit events.</summary>
    public interface IAppEventAware {
        /// <summary>Called when the application gains or loses focus.</summary>
        void OnAppFocus(bool hasFocus);

        /// <summary>Called when the application pauses or resumes.</summary>
        void OnAppPause(bool isPause);

        /// <summary>Called while the application is quitting.</summary>
        void OnAppQuit();
    }

    /// <summary>
    /// A service that a container or scope can drive through initialize, start and deinitialize.
    /// </summary>
    public interface IManagedService : IInitializable, IStartable, IDeinitializable {
    }
}
