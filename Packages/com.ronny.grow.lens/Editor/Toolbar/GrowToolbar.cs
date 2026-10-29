// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Reflection;
using Grow.Lens.Editor.Core;
using Grow.Lens.Editor.Localization;
using Grow.Lens.Editor.Menus;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Grow.Lens.Editor.Toolbar {
    /// <summary>
    /// Adds a single "Grow" entry to the left side of the main toolbar. The entry opens a menu
    /// instead of installing a row of buttons; the PlayMode container is never touched.
    /// </summary>
    internal static class GrowToolbar {
        private const string ToolbarTypeName = "UnityEditor.Toolbar";
        private const float PollIntervalSeconds = 3f;

        private static Type _toolbarType;
        private static VisualElement _root;
        private static ToolbarButton _button;
        private static double _lastPollTime;
        private static bool _subscribed;
        private static bool _apiWarned;

        internal static void Subscribe() {
            if (_subscribed) {
                return;
            }

            _subscribed = true;
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
            TryAttach();
        }

        internal static void Unsubscribe() {
            if (!_subscribed) {
                return;
            }

            _subscribed = false;
            EditorApplication.update -= OnUpdate;
            Detach();
        }

        private static void OnUpdate() {
            GrowLensGuard.Run("Toolbar", () => {
                if (_button != null && _button.panel != null) {
                    return;
                }

                if (EditorApplication.timeSinceStartup - _lastPollTime < PollIntervalSeconds) {
                    return;
                }

                _lastPollTime = EditorApplication.timeSinceStartup;
                TryAttach();
            });
        }

        private static void TryAttach() {
            _toolbarType ??= typeof(UnityEditor.Editor).Assembly.GetType(ToolbarTypeName);
            if (_toolbarType == null) {
                WarnApiOnce("UnityEditor.Toolbar is unavailable.");
                return;
            }

            var toolbar = FindToolbar();
            var field = _toolbarType.GetField("m_Root", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null) {
                WarnApiOnce("UnityEditor.Toolbar.m_Root is unavailable.");
                return;
            }

            if (toolbar == null) {
                return;
            }

            if (field.GetValue(toolbar) is not VisualElement root) {
                return;
            }

            var zone = root.Q("ToolbarZoneLeftAlign");
            if (zone == null) {
                return;
            }

            if (_root != root) {
                Detach();
                _root = root;
            }

            if (_button != null && _button.parent == zone) {
                return;
            }

            _button ??= CreateButton();
            zone.Add(_button);
        }

        private static UnityEngine.Object FindToolbar() {
            var objects = Resources.FindObjectsOfTypeAll(_toolbarType);
            foreach (var candidate in objects) {
                if (candidate != null) {
                    return candidate;
                }
            }

            return null;
        }

        private static void WarnApiOnce(string message) {
            if (_apiWarned) {
                return;
            }

            _apiWarned = true;
            Debug.LogWarning($"[Grow Lens] Toolbar entry is unavailable: {message}");
        }

        private static ToolbarButton CreateButton() {
            var button = new ToolbarButton(() => GrowLensGuard.Run("Toolbar menu", ShowMenu)) {
                text = "Grow",
                tooltip = GrowLensLocalization.Get("grow.lens.toolbar.tooltip"),
                style = {
                    height = 20,
                    marginLeft = 2,
                    marginRight = 2,
                    paddingLeft = 4,
                    paddingRight = 4,
                    fontSize = 12,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
            return button;
        }

        private static void Detach() {
            if (_button != null) {
                _button.RemoveFromHierarchy();
                _button = null;
            }

            _root = null;
        }

        private static void ShowMenu() {
            var menu = new GenericMenu();
            var preferences = Application.platform == RuntimePlatform.OSXEditor
                ? "Unity/Preferences..."
                : "Edit/Preferences...";
            AddMenuItem(menu, "grow.lens.toolbar.preferences", preferences);
            AddMenuItem(menu, "grow.lens.toolbar.projectSettings", "Edit/Project Settings...");
            AddMenuItem(menu, "grow.lens.toolbar.buildSettings", "File/Build Settings...");
            menu.AddSeparator("");
            AddMenuItem(menu, "grow.lens.toolbar.packageManager", "Window/Package Manager");
            AddMenuItem(menu, "grow.lens.toolbar.console", "Window/General/Console");
            AddMenuItem(menu, "grow.lens.toolbar.profiler", "Window/Analysis/Profiler");
            AddMenuItem(menu, "grow.lens.toolbar.memoryProfiler", "Window/Analysis/Memory Profiler");
            AddMenuItem(menu, "grow.lens.toolbar.frameDebugger", "Window/Analysis/Frame Debugger");
            AddMenuItem(menu, "grow.lens.toolbar.inputDebugger", "Window/Analysis/Input Debugger");
            menu.AddSeparator("");
            AddMenuItem(menu, "grow.lens.toolbar.openCSharpProject", "Assets/Open C# Project");
            menu.AddItem(new GUIContent(GrowLensLocalization.Get("grow.lens.menu.createSeparator")),
                GrowLensSettings.IsFeatureEnabled(GrowLensFeature.HierarchySeparators) &&
                GrowMenuActions.CanCreateHierarchySeparator,
                () => GrowLensGuard.RunAction("Toolbar separator",
                    GrowMenuActions.CreateHierarchySeparator));
            menu.AddItem(new GUIContent(GrowLensLocalization.Get("grow.lens.toolbar.takeScreenshot")),
                false, () => GrowLensGuard.RunAction("Toolbar screenshot", TakeScreenshot));
            menu.AddItem(new GUIContent(GrowLensLocalization.Get("grow.lens.toolbar.openScreenshotsFolder")),
                false, () => GrowLensGuard.RunAction("Toolbar screenshots folder", OpenScreenshotsFolder));
            if (_button != null) {
                menu.DropDown(_button.worldBound);
            } else {
                menu.ShowAsContext();
            }
        }

        private static void AddMenuItem(GenericMenu menu, string textKey, string menuPath) {
            var text = GrowLensLocalization.Get(textKey);
            menu.AddItem(new GUIContent(text), false,
                () => GrowLensGuard.RunAction($"Toolbar {text}",
                    () => EditorApplication.ExecuteMenuItem(menuPath)));
        }

        private static string ScreenshotsDirectory {
            get {
                var projectRoot = Path.GetDirectoryName(Application.dataPath) ?? ".";
                return Path.Combine(projectRoot, "Screenshots");
            }
        }

        private static void TakeScreenshot() {
            var directory = ScreenshotsDirectory;
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"screenshot_{DateTime.Now:HHmmss_fff}.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[Grow Lens] Screenshot requested: {path}");
        }

        private static void OpenScreenshotsFolder() {
            var directory = ScreenshotsDirectory;
            Directory.CreateDirectory(directory);
            EditorUtility.RevealInFinder(directory);
        }
    }
}
