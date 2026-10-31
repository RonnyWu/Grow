// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Grow.Lens.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace Grow.Lens.Editor.Hierarchy {
    /// <summary>
    /// Draws Grow Lens decorations on Hierarchy rows: component icons, separators and indent
    /// guides. The host owns the subscriptions; this class only draws the enabled features.
    /// </summary>
    internal static class GrowHierarchyEnhance {
        private static readonly Dictionary<int, GameObject> GameObjectCache = new();

        internal static void Subscribe() {
            InvalidateCaches();
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
            var current = Event.current;
            if (current == null ||
                (current.type != EventType.Repaint && current.type != EventType.MouseDown)) {
                return;
            }

            var icons = GrowLensSettings.IsFeatureEnabled(GrowLensFeature.HierarchyComponentIcons);
            var componentInspector = GrowLensSettings.HierarchyComponentInspector;
            var separators = GrowLensSettings.IsFeatureEnabled(GrowLensFeature.HierarchySeparators);
            var guides = GrowLensSettings.IsFeatureEnabled(GrowLensFeature.HierarchyIndentGuides);
            var activeToggle =
                GrowLensSettings.IsFeatureEnabled(GrowLensFeature.HierarchyActiveToggle);
            if (!icons && !separators && !guides && !activeToggle) {
                return;
            }

            var rightInset = activeToggle ? ItemActiveToggle.ReservedWidth : 0f;
            GrowLensGuard.Run("Hierarchy decorations", () => {
                if (!TryGetGameObject(instanceID, out var gameObject)) {
                    return;
                }

                if (current.type == EventType.MouseDown) {
                    if (activeToggle && ItemActiveToggle.TryHandleClick(gameObject, rect)) {
                        return;
                    }

                    if (separators && !IsSearching() && HierarchySeparator.IsSeparator(gameObject.name)) {
                        return;
                    }

                    if (icons && componentInspector &&
                        ItemComponentIcon.TryHandleClick(instanceID, gameObject, rect, rightInset)) {
                        current.Use();
                    }

                    return;
                }

                if (separators && !IsSearching() &&
                    HierarchySeparator.TryDraw(instanceID, gameObject, rect)) {
                    if (guides) {
                        ItemIndentLine.OnGameObjectGUI(instanceID, rect);
                    }

                    if (activeToggle) {
                        ItemActiveToggle.OnGameObjectGUI(gameObject, rect);
                    }

                    return;
                }

                if (icons) {
                    ItemComponentIcon.OnGameObjectGUI(instanceID, gameObject, rect, rightInset);
                }

                if (guides) {
                    ItemIndentLine.OnGameObjectGUI(instanceID, rect);
                }

                if (activeToggle) {
                    ItemActiveToggle.OnGameObjectGUI(gameObject, rect);
                }
            });
        }

        private static bool IsSearching() {
            return HierarchyGuideProbe.TryGetContext()?.IsSearching == true;
        }

        private static void OnHierarchyChanged() {
            InvalidateCaches();
        }

        private static void InvalidateCaches() {
            GameObjectCache.Clear();
            ItemComponentIcon.OnHierarchyChanged();
            HierarchySeparator.OnHierarchyChanged();
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
