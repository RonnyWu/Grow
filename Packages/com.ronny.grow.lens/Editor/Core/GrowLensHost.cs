// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Grow.Lens.Editor.Hierarchy;
using Grow.Lens.Editor.Inspector;
using Grow.Lens.Editor.Menus;
using Grow.Lens.Editor.Toolbar;
using UnityEditor;
using UnityEngine;

namespace Grow.Lens.Editor.Core {
    /// <summary>
    /// Single owner of every Grow Lens editor subscription. Features never subscribe themselves;
    /// turning the setting off removes every callback instead of leaving callbacks that return early.
    /// </summary>
    internal static class GrowLensHost {
        private const string SessionDisabledKey = "Grow.Lens.SessionDisabled";
        private static bool _subscribed;

        [InitializeOnLoadMethod]
        private static void Initialize() {
            Apply();
        }

        /// <summary>Applies the current setting; safe to call repeatedly and after domain reload.</summary>
        internal static void Apply() {
            var enabled = GrowLensSettings.Enabled && !Application.isBatchMode &&
                !SessionState.GetBool(SessionDisabledKey, false);
            if (enabled == _subscribed) {
                return;
            }

            _subscribed = enabled;
            if (enabled) {
                GrowLensGuard.Reset();
                GrowLensGuard.Run("Hierarchy subscribe", GrowHierarchyEnhance.Subscribe);
                GrowLensGuard.Run("Asset header subscribe", GrowAssetHead.Subscribe);
                GrowLensGuard.Run("Context menus subscribe", GrowContextMenus.Subscribe);
                GrowLensGuard.Run("Toolbar subscribe", GrowToolbar.Subscribe);
                return;
            }

            GrowHierarchyEnhance.Unsubscribe();
            GrowAssetHead.Unsubscribe();
            GrowContextMenus.Unsubscribe();
            GrowToolbar.Unsubscribe();
        }

        /// <summary>
        /// Disables the enhancements for this editor session and removes every callback.
        /// The saved preference stays enabled so a restart or an explicit re-enable can retry.
        /// </summary>
        internal static void Disable(string reason) {
            Debug.LogError(
                $"[Grow Lens] Disabling editor enhancements for this session (saved preference unchanged): {reason}");
            SessionState.SetBool(SessionDisabledKey, true);
            GrowLensSettings.DisableForSession();
            if (_subscribed) {
                _subscribed = false;
                GrowHierarchyEnhance.Unsubscribe();
                GrowAssetHead.Unsubscribe();
                GrowContextMenus.Unsubscribe();
                GrowToolbar.Unsubscribe();
            }
        }

        /// <summary>Clears a session-level failure disable before an explicit re-enable.</summary>
        internal static void ClearSessionDisable() {
            SessionState.SetBool(SessionDisabledKey, false);
        }

        /// <summary>Clears subscriptions and session state without reading project settings; tests only.</summary>
        internal static void ResetForTests() {
            SessionState.SetBool(SessionDisabledKey, false);
            if (_subscribed) {
                _subscribed = false;
                GrowHierarchyEnhance.Unsubscribe();
                GrowAssetHead.Unsubscribe();
                GrowContextMenus.Unsubscribe();
                GrowToolbar.Unsubscribe();
            }

            GrowLensGuard.ResetForTests();
        }
    }

    /// <summary>Contains a failing editor callback so it cannot spam the console every repaint.</summary>
    internal static class GrowLensGuard {
        private const int FailureLimit = 3;
        private static readonly Dictionary<string, int> Failures = new(StringComparer.Ordinal);
        private static readonly HashSet<string> Logged = new(StringComparer.Ordinal);

        internal static void Run(string feature, Action action) {
            try {
                action();
                Failures.Remove(feature);
            } catch (ExitGUIException) {
                throw;
            } catch (Exception exception) {
                Report(feature, exception);
            }
        }

        /// <summary>Runs a user-initiated command; failures are logged but never disable Lens.</summary>
        internal static void RunAction(string feature, Action action) {
            try {
                action();
            } catch (ExitGUIException) {
                throw;
            } catch (Exception exception) {
                Debug.LogError($"[Grow Lens] {feature} failed.\n{exception}");
            }
        }

        /// <summary>Records a failure without allocating a delegate; used by per-row callbacks.</summary>
        internal static void Report(string feature, Exception exception) {
            Failures.TryGetValue(feature, out var count);
            count++;
            Failures[feature] = count;
            if (Logged.Add(feature)) {
                Debug.LogError($"[Grow Lens] {feature} failed; further failures are suppressed.\n{exception}");
            }

            if (count >= FailureLimit) {
                GrowLensHost.Disable($"{feature} failed {count} times.");
            }
        }

        internal static void Reset() {
            Failures.Clear();
            Logged.Clear();
        }

        internal static void ResetForTests() {
            Reset();
        }
    }
}
