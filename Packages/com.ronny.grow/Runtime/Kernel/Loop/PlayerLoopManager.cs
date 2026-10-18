// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Grow.Core.Log;
using UnityEngine;
using UnityEngine.LowLevel;
using static UnityEngine.LowLevel.PlayerLoopSystem;

namespace Grow.Kernel.Loop {
    /// <summary>
    /// Installs and removes Grow-managed systems on Unity's player loop.
    /// </summary>
    /// <remarks>
    /// The manager is intentionally static and internal: the later <c>GrowLoop</c> facade is the
    /// public entry, and tests reach it through <c>InternalsVisibleTo</c>. It keeps a snapshot of the
    /// loop as it was before the first install, removes only systems it installed, and is idempotent
    /// by system type: installing the same type again removes the old node and re-inserts it at the
    /// new anchor.
    /// <para>
    /// A missing anchor is a soft failure: it is logged and the current loop is not written. The
    /// editor hook restores the pre-install loop when Play Mode ends, which keeps a project that
    /// disables Domain Reload from carrying injected systems into the next session;
    /// <see cref="ResetStaticState"/> repeats that cleanup; <see cref="Entry.GrowSession"/> calls it
    /// at <c>SubsystemRegistration</c>.
    /// </para>
    /// </remarks>
    internal static class PlayerLoopManager {
        private const string LogCategory = "Loop";

        private static readonly Dictionary<Type, PlayerLoopSystem> Installed = new();

        private static PlayerLoopSystem _original;
        private static bool _hasSnapshot;
        private static bool _modified;

        /// <summary>True when at least one Grow system is currently installed.</summary>
        internal static bool IsModified => _modified;

        /// <summary>Installs a system, using <typeparamref name="T"/> as its identity.</summary>
        internal static bool Install<T>(UpdateFunction update, Type anchorType,
            InsertLocation location)
            where T : class {
            return Install(typeof(T), update, anchorType, location);
        }

        /// <summary>
        /// Installs or refreshes a system relative to <paramref name="anchorType"/>.
        /// </summary>
        /// <returns>False when an argument is null or the anchor is absent; the loop is untouched.</returns>
        internal static bool Install(
            Type systemType,
            UpdateFunction update,
            Type anchorType,
            InsertLocation location) {
            if (systemType == null) {
                GrowLog.Error("Install failed: system type is null.", LogCategory);
                return false;
            }

            if (update == null) {
                GrowLog.Error($"Install<{systemType.Name}> failed: update delegate is null.",
                    LogCategory);
                return false;
            }

            if (anchorType == null) {
                GrowLog.Error($"Install<{systemType.Name}> failed: anchor type is null.",
                    LogCategory);
                return false;
            }

            var current = PlayerLoop.GetCurrentPlayerLoop();
            var systems = current.subSystemList == null
                ? Array.Empty<PlayerLoopSystem>()
                : PlayerLoopGraph.CloneArray(current.subSystemList);

            // Remove a previous node of the same type first: a re-install refreshes the callback and
            // keeps exactly one managed node per type, including after a session without Domain Reload.
            while (PlayerLoopGraph.TryRemove(ref systems, systemType)) {
            }

            var newSystem = new PlayerLoopSystem { type = systemType, updateDelegate = update };
            if (!PlayerLoopGraph.TryInsert(ref systems, anchorType, newSystem, location)) {
                GrowLog.Error(
                    $"Install<{systemType.Name}> failed: anchor '{anchorType.Name}' not found in PlayerLoop.",
                    LogCategory);
                return false;
            }

            if (!_hasSnapshot) {
                _original = PlayerLoopGraph.Clone(current);
                _hasSnapshot = true;
            }

            current.subSystemList = systems;
            // Record before applying: if SetPlayerLoop throws, RemoveInstalled still knows this type
            // may have been inserted and can clean it up.
            Installed[systemType] = newSystem;
            _modified = true;
            PlayerLoop.SetPlayerLoop(current);
            GrowLog.Info(
                $"Inserted {systemType.Name} {(location == InsertLocation.Before ? "before" : "after")} " +
                $"{anchorType.Name} in PlayerLoop.",
                LogCategory);
            return true;
        }

        /// <summary>Removes a managed system of <typeparamref name="T"/>.</summary>
        internal static void Remove<T>() where T : class {
            Remove(typeof(T));
        }

        /// <summary>Removes a managed system of <paramref name="systemType"/>.</summary>
        internal static void Remove(Type systemType) {
            if (systemType == null) {
                return;
            }

            if (!Installed.Remove(systemType)) {
                GrowLog.Error($"Remove {systemType.Name} failed: system is not managed by Grow.",
                    LogCategory);
                return;
            }

            var current = PlayerLoop.GetCurrentPlayerLoop();
            var systems = current.subSystemList == null
                ? Array.Empty<PlayerLoopSystem>()
                : PlayerLoopGraph.CloneArray(current.subSystemList);

            if (!PlayerLoopGraph.TryRemove(ref systems, systemType)) {
                GrowLog.Error($"Remove {systemType.Name} failed: system not found in PlayerLoop.",
                    LogCategory);
                ReleaseSnapshotIfIdle();
                return;
            }

            while (PlayerLoopGraph.TryRemove(ref systems, systemType)) {
            }

            current.subSystemList = systems;
            PlayerLoop.SetPlayerLoop(current);
            ReleaseSnapshotIfIdle();
            GrowLog.Info($"Removed {systemType.Name} from PlayerLoop.", LogCategory);
        }

        /// <summary>
        /// Removes every Grow-managed system without touching nodes inserted by other code.
        /// </summary>
        internal static void RemoveInstalled() {
            if (Installed.Count == 0) {
                ReleaseSnapshotIfIdle();
                return;
            }

            var current = PlayerLoop.GetCurrentPlayerLoop();
            var systems = current.subSystemList == null
                ? Array.Empty<PlayerLoopSystem>()
                : PlayerLoopGraph.CloneArray(current.subSystemList);

            var removedAny = false;
            foreach (var type in new List<Type>(Installed.Keys)) {
                while (PlayerLoopGraph.TryRemove(ref systems, type)) {
                    removedAny = true;
                }
            }

            if (removedAny) {
                current.subSystemList = systems;
                PlayerLoop.SetPlayerLoop(current);
            }

            Installed.Clear();
            ReleaseSnapshot();
            GrowLog.Info(
                "Removed all Grow-managed PlayerLoop systems (nodes inserted by others left untouched).",
                LogCategory);
        }

        /// <summary>
        /// Restores the loop captured before the first install and clears all managed systems.
        /// </summary>
        /// <remarks>
        /// This overwrites the whole loop, so nodes inserted by other code after the snapshot are
        /// lost. Prefer <see cref="RemoveInstalled"/> for normal teardown; this is the editor/domain
        /// reload fallback.
        /// </remarks>
        internal static void RestoreOriginal() {
            if (!_hasSnapshot) {
                return;
            }

            PlayerLoop.SetPlayerLoop(_original);
            Installed.Clear();
            _hasSnapshot = false;
            _original = default;
            _modified = false;
            GrowLog.Info("Restored the pre-install PlayerLoop snapshot.", LogCategory);
        }

        /// <summary>Drops the snapshot when no managed system remains, so the next install re-snapshots.</summary>
        private static void ReleaseSnapshotIfIdle() {
            if (Installed.Count == 0) {
                ReleaseSnapshot();
            }
        }

        private static void ReleaseSnapshot() {
            _hasSnapshot = false;
            _original = default;
            _modified = false;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorHooks() {
            UnityEditor.EditorApplication.playModeStateChanged += OnEditorPlayModeStateChanged;
        }

        private static void OnEditorPlayModeStateChanged(UnityEditor.PlayModeStateChange state) {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode ||
                state == UnityEditor.PlayModeStateChange.ExitingEditMode) {
                RestoreOriginal();
            }
        }
#endif

        /// <summary>Clears the static state; called by <see cref="Entry.GrowSession"/>.</summary>
        internal static void ResetStaticState() {
            // A previous session may have left managed nodes behind when Domain Reload is disabled;
            // restoring the snapshot before forgetting the registry is the only way to find them.
            if (_hasSnapshot) {
                RestoreOriginal();
            }

            Installed.Clear();
            _original = default;
            _hasSnapshot = false;
            _modified = false;
        }
    }
}
