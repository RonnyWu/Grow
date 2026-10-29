// SPDX-License-Identifier: MIT

using System;
using Grow.Lens.Editor.Core;
using Grow.Lens.Editor.Localization;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Grow.Lens.Editor.Menus {
    /// <summary>
    /// Ctrl+right-click menus for the Project and Hierarchy windows. The menus call commands
    /// directly and never run Grow's own menu items through <c>EditorApplication.ExecuteMenuItem</c>.
    /// </summary>
    internal static class GrowContextMenus {
        internal static void SubscribeProject() {
            EditorApplication.projectWindowItemOnGUI -= OnProjectGUI;
            EditorApplication.projectWindowItemOnGUI += OnProjectGUI;
        }

        internal static void UnsubscribeProject() {
            EditorApplication.projectWindowItemOnGUI -= OnProjectGUI;
        }

        internal static void SubscribeHierarchy() {
            EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyGUI;
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
        }

        internal static void UnsubscribeHierarchy() {
            EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyGUI;
        }

        private static void OnProjectGUI(string guid, Rect selectionRect) {
            var evt = Event.current;
            if (evt == null || evt.type != EventType.MouseDown || evt.button != 1 ||
                !(evt.control || evt.command) || !selectionRect.Contains(evt.mousePosition)) {
                return;
            }

            var path = AssetDatabase.GUIDToAssetPath(guid);
            var selected = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Object>(path);
            if (selected != null && !Selection.Contains(selected)) {
                Selection.activeObject = selected;
            }

            GrowLensGuard.RunAction("Project context menu", () => ShowProjectMenu(guid, selected));
            evt.Use();
        }

        private static void OnHierarchyGUI(int instanceID, Rect selectionRect) {
            var evt = Event.current;
            if (evt == null || evt.type != EventType.MouseDown || evt.button != 1 ||
                !(evt.control || evt.command) || !selectionRect.Contains(evt.mousePosition)) {
                return;
            }

            var gameObject = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (gameObject == null) {
                return;
            }

            if (!Selection.Contains(gameObject)) {
                Selection.activeGameObject = gameObject;
            }

            GrowLensGuard.RunAction("Hierarchy context menu", () => ShowHierarchyMenu(gameObject));
            evt.Use();
        }

        private static void ShowProjectMenu(string guid, Object target) {
            var assetPath = string.IsNullOrEmpty(guid) ? string.Empty : AssetDatabase.GUIDToAssetPath(guid);
            var hasAsset = target != null && !string.IsNullOrEmpty(assetPath);

            var menu = new GenericMenu();
            menu.AddDisabledItem(new GUIContent(Text("grow.lens.menu.header")));
            menu.AddSeparator("");

            Add(menu, "grow.lens.menu.copyName", () => GrowMenuActions.CopyName(target), hasAsset);
            Add(menu, "grow.lens.menu.copyPath", () => GrowMenuActions.CopyPath(target), hasAsset);
            Add(menu, "grow.lens.menu.copyGuid", () => GrowMenuActions.CopyGuid(target), hasAsset);
            Add(menu, "grow.lens.menu.showInExplorer", () => GrowMenuActions.RevealInFinder(target), hasAsset);

            menu.AddSeparator("");
            Add(menu, "grow.lens.menu.createFolder", GrowMenuActions.CreateFolder);
            Add(menu, "grow.lens.menu.createScene",
                () => GrowMenuActions.ExecuteMenuItem("Assets/Create/Scene"));
            Add(menu, "grow.lens.menu.createMaterial",
                () => GrowMenuActions.ExecuteMenuItem("Assets/Create/Material"));
            Add(menu, "grow.lens.menu.createPrefab",
                () => GrowMenuActions.ExecuteMenuItem("Assets/Create/Prefab"));
            Add(menu, "grow.lens.menu.createPrefabVariant",
                () => GrowMenuActions.ExecuteMenuItem("Assets/Create/Prefab Variant"));

            menu.AddSeparator("");
            Add(menu, "grow.lens.menu.delete", GrowMenuActions.DeleteSelection, hasAsset);
            menu.ShowAsContext();
        }

        private static void ShowHierarchyMenu(GameObject target) {
            var menu = new GenericMenu();
            menu.AddDisabledItem(new GUIContent(Text("grow.lens.menu.header")));
            menu.AddSeparator("");

            var hasGuid = GrowMenuActions.TryGetGuidInfo(target, out _, out _);
            Add(menu, "grow.lens.menu.copyName", () => GrowMenuActions.CopyName(target));
            Add(menu, "grow.lens.menu.copyPath", () => GrowMenuActions.CopyPath(target));
            Add(menu, "grow.lens.menu.copyGuid", () => GrowMenuActions.CopyGuid(target), hasGuid);

            menu.AddSeparator("");
            Add(menu, "grow.lens.menu.createEmptyReset",
                () => GrowMenuActions.CreateEmpty("GameObject/Create Empty"));
            Add(menu, "grow.lens.menu.createEmptyChildReset",
                () => GrowMenuActions.CreateEmpty("GameObject/Create Empty Child"));
            Add(menu, "grow.lens.menu.createEmptyParentReset",
                () => GrowMenuActions.CreateEmpty("GameObject/Create Empty Parent"));
            Add(menu, "grow.lens.menu.createSeparator", GrowMenuActions.CreateHierarchySeparator,
                GrowLensSettings.IsFeatureEnabled(GrowLensFeature.HierarchySeparators) &&
                GrowMenuActions.CanCreateHierarchySeparator);

            menu.AddSeparator("");
            Add(menu, "grow.lens.menu.delete", GrowMenuActions.DeleteSelection);
            menu.ShowAsContext();
        }

        private static void Add(GenericMenu menu, string textKey, Action action, bool enabled = true) {
            var content = new GUIContent(Text(textKey));
            if (enabled) {
                menu.AddItem(content, false, () => GrowLensGuard.RunAction("Context menu action", action));
            } else {
                menu.AddDisabledItem(content);
            }
        }

        private static string Text(string key) {
            return GrowLensLocalization.Get(key);
        }
    }
}
