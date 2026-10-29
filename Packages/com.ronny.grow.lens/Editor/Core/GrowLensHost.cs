// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Grow.Lens.Editor.Hierarchy;
using Grow.Lens.Editor.Inspector;
using Grow.Lens.Editor.Menus;
using Grow.Lens.Editor.Toolbar;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Grow.Lens.Editor.Core {
    /// <summary>
    /// Single owner of every Grow Lens editor subscription. Features never subscribe themselves;
    /// each subsystem is attached only while its feature is enabled, and turning Grow Lens off
    /// removes every callback instead of leaving callbacks that return early.
    /// </summary>
    internal static class GrowLensHost {
        private const string SessionDisabledKey = "Grow.Lens.SessionDisabled";
        private static bool _active;
        private static bool _hierarchySubscribed;
        private static bool _assetHeaderSubscribed;
        private static bool _projectContextMenusSubscribed;
        private static bool _hierarchyContextMenusSubscribed;
        private static bool _toolbarSubscribed;
        private static bool _repaintScheduled;

        [InitializeOnLoadMethod]
        private static void Initialize() {
            Apply();
        }

        /// <summary>Applies the current setting; safe to call repeatedly and after domain reload.</summary>
        internal static void Apply() {
            var active = GrowLensSettings.Enabled && !Application.isBatchMode &&
                !SessionState.GetBool(SessionDisabledKey, false);
            if (active && !_active) {
                GrowLensGuard.Reset();
            }

            _active = active;
            var hierarchy = active && GrowLensSettings.AnyHierarchyDecoration;
            var assetHeader = active && GrowLensSettings.GetFeature(GrowLensFeature.AssetHeader);
            var projectMenus =
                active && GrowLensSettings.GetFeature(GrowLensFeature.ProjectContextMenus);
            var hierarchyMenus =
                active && GrowLensSettings.GetFeature(GrowLensFeature.HierarchyContextMenus);
            var toolbar = active && GrowLensSettings.GetFeature(GrowLensFeature.Toolbar);

            Sync(ref _hierarchySubscribed, hierarchy, "Hierarchy subscribe",
                GrowHierarchyEnhance.Subscribe, GrowHierarchyEnhance.Unsubscribe);
            Sync(ref _assetHeaderSubscribed, assetHeader, "Asset header subscribe",
                GrowAssetHead.Subscribe, GrowAssetHead.Unsubscribe);
            Sync(ref _projectContextMenusSubscribed, projectMenus, "Project context menus subscribe",
                GrowContextMenus.SubscribeProject, GrowContextMenus.UnsubscribeProject);
            Sync(ref _hierarchyContextMenusSubscribed, hierarchyMenus,
                "Hierarchy context menus subscribe", GrowContextMenus.SubscribeHierarchy,
                GrowContextMenus.UnsubscribeHierarchy);
            Sync(ref _toolbarSubscribed, toolbar, "Toolbar subscribe",
                GrowToolbar.Subscribe, GrowToolbar.Unsubscribe);
        }

        /// <summary>
        /// Requests one deferred repaint of the Settings window and every editor view. The
        /// settings controls run inside IMGUI, so changing subscriptions or drawing flags must
        /// not repaint synchronously; without this the toggle looks inert until the mouse moves.
        /// </summary>
        internal static void RequestRepaint() {
            if (Application.isBatchMode || _repaintScheduled) {
                return;
            }

            _repaintScheduled = true;
            EditorApplication.delayCall += () => {
                _repaintScheduled = false;
                SettingsService.RepaintAllSettingsWindow();
                InternalEditorUtility.RepaintAllViews();
            };
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
            UnsubscribeAll();
            RequestRepaint();
        }

        /// <summary>Clears a session-level failure disable before an explicit re-enable.</summary>
        internal static void ClearSessionDisable() {
            SessionState.SetBool(SessionDisabledKey, false);
        }

        /// <summary>Clears subscriptions and session state without reading project settings; tests only.</summary>
        internal static void ResetForTests() {
            SessionState.SetBool(SessionDisabledKey, false);
            UnsubscribeAll();
            GrowLensGuard.ResetForTests();
        }

        private static void Sync(ref bool state, bool desired, string feature, Action subscribe,
            Action unsubscribe) {
            if (state == desired) {
                return;
            }

            state = desired;
            if (desired) {
                GrowLensGuard.Run(feature, subscribe);
            } else {
                unsubscribe();
            }
        }

        private static void UnsubscribeAll() {
            _active = false;
            _hierarchySubscribed = false;
            _assetHeaderSubscribed = false;
            _projectContextMenusSubscribed = false;
            _hierarchyContextMenusSubscribed = false;
            _toolbarSubscribed = false;
            GrowHierarchyEnhance.Unsubscribe();
            GrowAssetHead.Unsubscribe();
            GrowContextMenus.UnsubscribeProject();
            GrowContextMenus.UnsubscribeHierarchy();
            GrowToolbar.Unsubscribe();
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
