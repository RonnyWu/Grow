// SPDX-License-Identifier: MIT

using System;
using Grow.Core.Events;
using Grow.Core.Log;
using UnityEngine;

namespace Grow.Kernel.Loop {
    /// <summary>
    /// The engine-side host that owns the phase events, coroutines and application callbacks.
    /// </summary>
    /// <remarks>
    /// Frame callbacks are not native <c>MonoBehaviour.Update</c> methods: <see cref="PlayerLoopProfile"/>
    /// injects five phases into Unity's player loop and calls the matching <c>Raise*</c> methods here.
    /// The component therefore does not depend on script execution order or scene lifetime.
    /// <para>
    /// Application focus, pause and quit are the only native callbacks left on the component. On quit
    /// the host publishes <see cref="AppQuit"/>, removes the injected phases, shuts the kernel down and
    /// disposes its events. Event listener faults are reported by the per-event sink and never escape
    /// into the player loop.
    /// </para>
    /// </remarks>
    internal sealed class MonoExecutor : MonoBehaviour, IMonoExecutor {
        private const string LogCategory = "Loop";

        private readonly GrowEvent.Publisher _appQuit = CreateEvent();
        private readonly GrowEvent.Publisher _preUpdate = CreateEvent();
        private readonly GrowEvent.Publisher _fixedUpdate = CreateEvent();
        private readonly GrowEvent.Publisher _update = CreateEvent();
        private readonly GrowEvent.Publisher _postUpdate = CreateEvent();
        private readonly GrowEvent.Publisher _lateUpdate = CreateEvent();
        private readonly GrowEvent<bool>.Publisher _appFocus = CreateBoolEvent();
        private readonly GrowEvent<bool>.Publisher _appPause = CreateBoolEvent();

        private bool _quitting;

        public GrowEvent AppQuit => _appQuit.Event;
        public GrowEvent PreUpdate => _preUpdate.Event;
        public GrowEvent FixedUpdate => _fixedUpdate.Event;
        public GrowEvent Update => _update.Event;
        public GrowEvent PostUpdate => _postUpdate.Event;
        public GrowEvent LateUpdate => _lateUpdate.Event;
        public GrowEvent<bool> AppFocus => _appFocus.Event;
        public GrowEvent<bool> AppPause => _appPause.Event;
        public bool Valid => this != null;

        internal void RaisePreUpdate() => _preUpdate.Publish();
        internal void RaiseFixedUpdate() => _fixedUpdate.Publish();
        internal void RaiseUpdate() => _update.Publish();
        internal void RaisePostUpdate() => _postUpdate.Publish();
        internal void RaiseLateUpdate() => _lateUpdate.Publish();
        internal void RaiseAppFocus(bool hasFocus) => _appFocus.Publish(hasFocus);
        internal void RaiseAppPause(bool isPaused) => _appPause.Publish(isPaused);

        private void OnApplicationQuit() {
            QuitForPlayer();
        }

        private void OnApplicationFocus(bool hasFocus) {
            if (_quitting) {
                return;
            }

            _appFocus.Publish(hasFocus);
        }

        private void OnApplicationPause(bool pauseStatus) {
            if (_quitting) {
                return;
            }

            _appPause.Publish(pauseStatus);
        }

        private void OnDestroy() {
            // A host destroyed without OnApplicationQuit must not leave injected phases behind.
            PlayerLoopProfile.Uninstall(this);
            DisposeEvents();
        }

        /// <summary>
        /// Runs the application-quit cleanup once: app event, composition callback, loop, events.
        /// </summary>
        internal void QuitForPlayer() {
            if (_quitting) {
                return;
            }

            _quitting = true;

            try {
                _appQuit.Publish();
            } catch (Exception exception) {
                GrowLog.Error($"AppQuit listener failure: {exception}", LogCategory);
            }

            try {
                GrowLoop.NotifyQuitCommitted();
            } catch (Exception exception) {
                GrowLog.Error($"Composition quit failed: {exception}", LogCategory);
            }

            try {
                GrowLoop.Uninstall();
            } catch (Exception exception) {
                GrowLog.Error($"Loop quit cleanup failed: {exception}", LogCategory);
            }

            DisposeEvents();
        }

        private void DisposeEvents() {
            _appFocus.Dispose();
            _appPause.Dispose();
            _appQuit.Dispose();
            _preUpdate.Dispose();
            _fixedUpdate.Dispose();
            _update.Dispose();
            _postUpdate.Dispose();
            _lateUpdate.Dispose();
        }

        private static GrowEvent.Publisher CreateEvent() {
            return new GrowEvent.Publisher(OnListenerFault);
        }

        private static GrowEvent<bool>.Publisher CreateBoolEvent() {
            return new GrowEvent<bool>.Publisher(OnListenerFault);
        }

        private static void OnListenerFault(Exception exception) {
            GrowLog.Error($"A loop listener threw: {exception}", LogCategory);
        }
    }
}
