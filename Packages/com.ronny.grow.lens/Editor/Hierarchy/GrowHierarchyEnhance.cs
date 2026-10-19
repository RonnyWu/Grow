// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Grow.Lens.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace Grow.Lens.Editor.Hierarchy {
    /// <summary>
    /// Draws Grow Lens decorations on Hierarchy rows: component icons and indent guides.
    /// The host owns the subscriptions; this class only draws.
    /// </summary>
    internal static class GrowHierarchyEnhance {
        private static readonly Dictionary<int, GameObject> GameObjectCache = new();

        internal static void Subscribe() {
            EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyGUI;
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        internal static void Unsubscribe() {
            EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyGUI;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        }

        private static void OnHierarchyGUI(int instanceID, Rect rect) {
            if (Event.current == null || Event.current.type != EventType.Repaint) {
                return;
            }

            GrowLensGuard.Run("Hierarchy decorations", () => {
                if (!TryGetGameObject(instanceID, out var gameObject)) {
                    return;
                }

                ItemComponentIcon.OnGameObjectGUI(instanceID, gameObject, rect);
                ItemIndentLine.OnGameObjectGUI(instanceID, rect);
            });
        }

        private static void OnHierarchyChanged() {
            GameObjectCache.Clear();
            ItemComponentIcon.OnHierarchyChanged();
            ItemIndentLine.OnHierarchyChanged();
        }

        private static bool TryGetGameObject(int instanceID, out GameObject gameObject) {
            if (GameObjectCache.TryGetValue(instanceID, out gameObject)) {
                if (gameObject != null) {
                    return true;
                }

                GameObjectCache.Remove(instanceID);
            }

            gameObject = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (gameObject == null) {
                return false;
            }

            GameObjectCache[instanceID] = gameObject;
            return true;
        }
    }
}
