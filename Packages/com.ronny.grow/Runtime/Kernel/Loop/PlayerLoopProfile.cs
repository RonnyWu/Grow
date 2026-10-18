// SPDX-License-Identifier: MIT

using System;
using Grow.Core.Log;
using static UnityEngine.LowLevel.PlayerLoopSystem;

namespace Grow.Kernel.Loop {
    /// <summary>
    /// Installs the five Grow frame phases into Unity's player loop.
    /// </summary>
    /// <remarks>
    /// The phase table is fixed: PreUpdate before the update group, FixedUpdate after the fixed
    /// group, Update after scene scripts update, PostUpdate after the whole update group, and
    /// LateUpdate after scene scripts late-update. Each injected callback checks that the host is
    /// still alive before raising its event, so a destroyed host cannot throw from the player loop.
    /// <para>
    /// Install is all-or-nothing: if any anchor is missing, systems installed by this profile are
    /// removed again and the method returns false.
    /// </para>
    /// </remarks>
    internal static class PlayerLoopProfile {
        private const string LogCategory = "Loop";

        private enum Phase {
            PreUpdate,
            FixedUpdate,
            Update,
            PostUpdate,
            LateUpdate
        }

        private readonly struct PhaseDescriptor {
            public readonly Type SystemType;
            public readonly Type AnchorType;
            public readonly InsertLocation Location;
            public readonly Phase Phase;

            public PhaseDescriptor(Type systemType, Type anchorType, InsertLocation location,
                Phase phase) {
                SystemType = systemType;
                AnchorType = anchorType;
                Location = location;
                Phase = phase;
            }
        }

        private sealed class PreUpdateSystem {
        }

        private sealed class FixedUpdateSystem {
        }

        private sealed class UpdateSystem {
        }

        private sealed class PostUpdateSystem {
        }

        private sealed class LateUpdateSystem {
        }

        private static readonly PhaseDescriptor[] Phases = {
            new(
                typeof(PreUpdateSystem),
                typeof(UnityEngine.PlayerLoop.Update),
                InsertLocation.Before,
                Phase.PreUpdate),
            new(
                typeof(FixedUpdateSystem),
                // The top-level FixedUpdate is a group, not a fixed-step callback: a sibling after it
                // runs once per frame. Insert inside the group so Unity invokes it once per fixed step.
                typeof(UnityEngine.PlayerLoop.FixedUpdate.ScriptRunBehaviourFixedUpdate),
                InsertLocation.After,
                Phase.FixedUpdate),
            new(
                typeof(UpdateSystem),
                typeof(UnityEngine.PlayerLoop.Update.ScriptRunBehaviourUpdate),
                InsertLocation.After,
                Phase.Update),
            new(
                typeof(PostUpdateSystem),
                typeof(UnityEngine.PlayerLoop.Update),
                InsertLocation.After,
                Phase.PostUpdate),
            new(
                typeof(LateUpdateSystem),
                typeof(UnityEngine.PlayerLoop.PreLateUpdate.ScriptRunBehaviourLateUpdate),
                InsertLocation.After,
                Phase.LateUpdate)
        };

        private static MonoExecutor _owner;

        /// <summary>True while the profile's phases are installed.</summary>
        internal static bool Installed => _owner != null;

        /// <summary>Installs all phases for <paramref name="executor"/>; false if an anchor is missing.</summary>
        internal static bool Install(MonoExecutor executor) {
            if (executor == null) {
                GrowLog.Error("PlayerLoop profile install failed: executor is null.", LogCategory);
                return false;
            }

            if (ReferenceEquals(_owner, executor) && Installed) {
                return true;
            }

            var rebind = _owner != null;
            try {
                for (var i = 0; i < Phases.Length; i++) {
                    var phase = Phases[i];
                    if (PlayerLoopManager.Install(
                            phase.SystemType,
                            CreateCallback(executor, phase.Phase),
                            phase.AnchorType,
                            phase.Location)) {
                        continue;
                    }

                    RemoveInstalledSafely();
                    _owner = null;
                    GrowLog.Error(
                        "PlayerLoop profile install incomplete: an anchor is missing; managed systems were removed.",
                        LogCategory);
                    return false;
                }
            } catch {
                RemoveInstalledSafely();
                _owner = null;
                throw;
            }

            _owner = executor;
            GrowLog.Info(
                rebind
                    ? "PlayerLoop profile rebound to the new host (PreUpdate/FixedUpdate/Update/PostUpdate/LateUpdate)."
                    : "PlayerLoop profile installed (PreUpdate/FixedUpdate/Update/PostUpdate/LateUpdate).",
                LogCategory);
            return true;
        }

        /// <summary>
        /// Removes the profile's phases. A non-null <paramref name="expectedOwner"/> only uninstalls
        /// when it is still the owner, so a late <c>OnDestroy</c> from an old host cannot tear down a
        /// profile that already belongs to its replacement.
        /// </summary>
        internal static void Uninstall(MonoExecutor expectedOwner = null) {
            if (!ReferenceEquals(expectedOwner, null) &&
                _owner != null &&
                !ReferenceEquals(_owner, expectedOwner)) {
                return;
            }

            var wasOwner = _owner != null;
            _owner = null;
            try {
                // Remove even when _owner is null: a failed Install can leave nodes behind before the
                // owner is recorded.
                PlayerLoopManager.RemoveInstalled();
            } finally {
                if (wasOwner) {
                    GrowLog.Info("PlayerLoop profile uninstalled.", LogCategory);
                }
            }
        }

        /// <summary>Drops the static owner without touching the loop (test/session reset helper).</summary>
        internal static void ResetStaticState() {
            _owner = null;
        }

        private static void RemoveInstalledSafely() {
            try {
                PlayerLoopManager.RemoveInstalled();
            } catch (Exception exception) {
                GrowLog.Error(
                    $"[Loop] Failed to clean up a partial profile install: {exception}",
                    LogCategory);
            }
        }

        private static UpdateFunction CreateCallback(MonoExecutor executor, Phase phase) {
            return () => {
                if (executor == null || !executor.Valid) {
                    return;
                }

                switch (phase) {
                    case Phase.PreUpdate:
                        executor.RaisePreUpdate();
                        break;
                    case Phase.FixedUpdate:
                        executor.RaiseFixedUpdate();
                        break;
                    case Phase.Update:
                        executor.RaiseUpdate();
                        break;
                    case Phase.PostUpdate:
                        executor.RaisePostUpdate();
                        break;
                    case Phase.LateUpdate:
                        executor.RaiseLateUpdate();
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown player loop phase {phase}.");
                }
            };
        }
    }
}
