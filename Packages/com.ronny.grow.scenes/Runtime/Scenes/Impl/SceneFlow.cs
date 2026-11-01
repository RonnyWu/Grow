// SPDX-License-Identifier: MIT

using System;
using Grow.Core.Log;
using Grow.Kernel.Scheduling;
using UnityEngine.SceneManagement;

namespace Grow.Scenes.Impl {
    /// <summary>Default <see cref="ISceneFlow"/>: one foreground operation, polled by the scheduler.</summary>
    /// <remarks>
    /// The flow owns no frame source. It polls with <see cref="GrowScheduler.NextFrame"/>, so the
    /// kernel's update phase remains the only tick and the flow stops costing anything as soon as
    /// the operation finishes.
    /// <para>
    /// <see cref="Step"/> is internal so tests can advance the flow without a running player loop;
    /// the scheduled callback is the production caller and behaves the same way.
    /// </para>
    /// </remarks>
    internal sealed class SceneFlow : ISceneFlow {
        private const string LogCategory = "Scenes";

        private readonly ISceneBackend _backend;
        private SceneOperation _active;
        private bool _stopped;

        internal SceneFlow() : this(new UnitySceneBackend()) {
        }

        internal SceneFlow(ISceneBackend backend) {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        /// <inheritdoc />
        public bool IsBusy => _active != null && !_active.IsDone && !_active.IsFaulted;

        /// <inheritdoc />
        public string ActiveScene => _backend.ActiveSceneName;

        /// <inheritdoc />
        public SceneRequest Load(string sceneName, LoadSceneMode mode = LoadSceneMode.Single) {
            if (string.IsNullOrWhiteSpace(sceneName)) {
                throw new ArgumentNullException(nameof(sceneName));
            }

            if (IsBusy) {
                throw new InvalidOperationException(
                    "[Scenes] A scene operation is already running; only one foreground load is " +
                    "allowed at a time.");
            }

            return Start(sceneName, mode);
        }

        /// <inheritdoc />
        public SceneRequest LoadAdditive(string sceneName) {
            return Load(sceneName, LoadSceneMode.Additive);
        }

        /// <inheritdoc />
        public SceneRequest Unload(SceneRequest request) {
            if (!request.IsValid) {
                throw new ArgumentException("[Scenes] The request refers to no operation.", nameof(request));
            }

            var operation = request.Operation;
            if (operation.IsUnload) {
                throw new InvalidOperationException("[Scenes] The request is already an unload.");
            }

            if (operation.IsFaulted) {
                throw new InvalidOperationException(
                    "[Scenes] A faulted request has no loaded scene to unload.");
            }

            if (!operation.IsDone) {
                // Unity cannot abort a load; remember the intent and apply it when the load ends.
                operation.UnloadRequested = true;
                return request;
            }

            if (IsBusy) {
                throw new InvalidOperationException(
                    "[Scenes] A scene operation is already running; wait for it before unloading.");
            }

            return StartUnload(operation.SceneName);
        }

        /// <inheritdoc />
        public bool SetActive(SceneRequest request) {
            if (!request.IsValid) {
                return false;
            }

            var operation = request.Operation;
            if (operation.IsUnload || !operation.IsDone) {
                return false;
            }

            return _backend.SetActive(operation.SceneName);
        }

        /// <summary>Advances the active operation once; the scheduled callback and tests call this.</summary>
        internal void Step() {
            var operation = _active;
            if (_stopped || operation == null || operation.IsDone || operation.IsFaulted) {
                return;
            }

            var handle = operation.Handle;
            if (handle == null) {
                Fail(operation, "The scene operation is unavailable.");
                return;
            }

            if (!handle.IsDone) {
                Schedule(operation);
                return;
            }

            Complete(operation);
        }

        /// <summary>Cancels the pending poll and forgets the operation; the scene itself is untouched.</summary>
        internal void Stop() {
            _stopped = true;
            if (_active == null) {
                return;
            }

            GrowScheduler.Cancel(_active.Timer);
            _active = null;
        }

        private SceneRequest Start(string sceneName, LoadSceneMode mode) {
            var operation = new SceneOperation(sceneName, mode);
            _active = operation;
            _stopped = false;

            try {
                operation.Handle = _backend.Load(sceneName, mode);
            } catch (Exception exception) {
                return Fail(operation, exception.Message);
            }

            Schedule(operation);
            return new SceneRequest(operation);
        }

        private SceneRequest StartUnload(string sceneName) {
            var operation = new SceneOperation(sceneName, LoadSceneMode.Additive) { IsUnload = true };
            _active = operation;

            try {
                operation.Handle = _backend.Unload(sceneName);
            } catch (Exception exception) {
                return Fail(operation, exception.Message);
            }

            Schedule(operation);
            return new SceneRequest(operation);
        }

        private void Complete(SceneOperation operation) {
            operation.IsDone = true;
            if (operation.IsUnload) {
                return;
            }

            if (operation.UnloadRequested) {
                operation.UnloadRequested = false;
                // Unity requires a loaded scene to be active before it can be unloaded reliably, and
                // the design promises "activate, then unload" rather than a fake cancel.
                _backend.SetActive(operation.SceneName);
                StartUnload(operation.SceneName);
            }
        }

        private void Schedule(SceneOperation operation) {
            operation.Timer = GrowScheduler.NextFrame(() => Poll(operation));
        }

        private void Poll(SceneOperation operation) {
            if (!ReferenceEquals(_active, operation)) {
                return;
            }

            Step();
        }

        private SceneRequest Fail(SceneOperation operation, string message) {
            GrowLog.Error(
                $"[Scenes] Could not {(operation.IsUnload ? "unload" : "load")} " +
                $"'{operation.SceneName}': {message}",
                LogCategory);
            operation.IsFaulted = true;
            operation.Error = message;
            return new SceneRequest(operation);
        }
    }
}
