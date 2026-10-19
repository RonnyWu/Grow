// SPDX-License-Identifier: MIT

using System;
using Grow.Lens.Editor.Core;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Grow.Lens.Editor.Menus {
    /// <summary>
    /// Ctrl+right-click menus for the Project and Hierarchy windows. The menus call commands
    /// directly and never run Grow's own menu items through <c>EditorApplication.ExecuteMenuItem</c>.
    /// </summary>
    internal static class GrowContextMenus {
        internal static void Subscribe() {
            EditorApplication.projectWindowItemOnGUI -= OnProjectGUI;
            EditorApplication.projectWindowItemOnGUI += OnProjectGUI;
            EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyGUI;
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
        }

        internal static void Unsubscribe() {
            EditorApplication.projectWindowItemOnGUI -= OnProjectGUI;
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
            menu.AddDisabledItem(new GUIContent("Grow Lens"));
            menu.AddSeparator("");

            Add(menu, "Copy Name", () => GrowMenuActions.CopyName(target), hasAsset);
            Add(menu, "Copy Path", () => GrowMenuActions.CopyPath(target), hasAsset);
            Add(menu, "Copy GUID", () => GrowMenuActions.CopyGuid(target), hasAsset);
            Add(menu, "Show in Explorer", () => GrowMenuActions.RevealInFinder(target), hasAsset);

            menu.AddSeparator("");
            Add(menu, "Create Folder", GrowMenuActions.CreateFolder);
            Add(menu, "Create Scene", () => GrowMenuActions.ExecuteMenuItem("Assets/Create/Scene"));
            Add(menu, "Create Material", () => GrowMenuActions.ExecuteMenuItem("Assets/Create/Material"));
            Add(menu, "Create Prefab", () => GrowMenuActions.ExecuteMenuItem("Assets/Create/Prefab"));
            Add(menu, "Create Prefab Variant",
                () => GrowMenuActions.ExecuteMenuItem("Assets/Create/Prefab Variant"));

            menu.AddSeparator("");
            Add(menu, "Delete", GrowMenuActions.DeleteSelection, hasAsset);
            menu.ShowAsContext();
        }

        private static void ShowHierarchyMenu(GameObject target) {
            var menu = new GenericMenu();
            menu.AddDisabledItem(new GUIContent("Grow Lens"));
            menu.AddSeparator("");

            var hasGuid = GrowMenuActions.TryGetGuidInfo(target, out _, out _);
            Add(menu, "Copy Name", () => GrowMenuActions.CopyName(target));
            Add(menu, "Copy Path", () => GrowMenuActions.CopyPath(target));
            Add(menu, "Copy GUID", () => GrowMenuActions.CopyGuid(target), hasGuid);

            menu.AddSeparator("");
            Add(menu, "Create Empty And Reset",
                () => GrowMenuActions.CreateEmpty("GameObject/Create Empty"));
            Add(menu, "Create Empty Child And Reset",
                () => GrowMenuActions.CreateEmpty("GameObject/Create Empty Child"));
            Add(menu, "Create Empty Parent And Reset",
                () => GrowMenuActions.CreateEmpty("GameObject/Create Empty Parent"));
            Add(menu, "Create Separator", GrowMenuActions.CreateHierarchySeparator,
                GrowMenuActions.CanCreateHierarchySeparator);

            menu.AddSeparator("");
            Add(menu, "Delete", GrowMenuActions.DeleteSelection);
            menu.ShowAsContext();
        }

        private static void Add(GenericMenu menu, string text, Action action, bool enabled = true) {
            var content = new GUIContent(text);
            if (enabled) {
                menu.AddItem(content, false, () => GrowLensGuard.RunAction("Context menu action", action));
            } else {
                menu.AddDisabledItem(content);
            }
        }
    }
}
