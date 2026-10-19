// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Grow.Lens.Editor.Hierarchy {
    /// <summary>Draws a GameObject's component icons from the right edge of its Hierarchy row.</summary>
    internal static class ItemComponentIcon {
        private const string MissingScriptIconName = "console.warnicon";
        private const string ScriptIconName = "cs Script Icon";
        private const int IconSize = 16;

        private static Texture2D _missingScriptIcon;
        private static Texture2D _scriptIcon;
        private static GUIContent _nameWidthContent;

        private static readonly Dictionary<string, float> NameWidthCache = new();
        private static readonly Dictionary<Type, Texture2D> ComponentTypeIconCache = new();
        private static readonly Dictionary<int, List<(Texture2D texture, GUIContent tooltip)>> ComponentIconCache = new();
        private static readonly List<(Texture2D texture, GUIContent tooltip)> ComponentIconEmpty = new();
        private static readonly List<Component> ComponentContainer = new();

        internal static void OnGameObjectGUI(int instanceID, GameObject gameObject, Rect rect) {
            _missingScriptIcon ??= EditorGUIUtility.IconContent(MissingScriptIconName).image as Texture2D;
            DrawComponentIcons(instanceID, gameObject, rect);
        }

        internal static void OnHierarchyChanged() {
            NameWidthCache.Clear();
            ComponentIconCache.Clear();
        }

        private static float GetNameWidth(string name) {
            if (NameWidthCache.TryGetValue(name, out var width)) {
                return width;
            }

            _nameWidthContent ??= new GUIContent();
            _nameWidthContent.text = name;
            // The name is followed by the object icon, so add one icon width.
            return NameWidthCache[name] = EditorStyles.label.CalcSize(_nameWidthContent).x + IconSize;
        }

        private static Texture2D GetComponentTypeIcon(Component component) {
            _scriptIcon ??= EditorGUIUtility.FindTexture(ScriptIconName);
            if (component == null) {
                return _missingScriptIcon;
            }

            var componentType = component.GetType();
            if (ComponentTypeIconCache.TryGetValue(componentType, out var cachedIcon)) {
                return cachedIcon;
            }

            var icon = EditorGUIUtility.ObjectContent(component, componentType).image as Texture2D;
            return ComponentTypeIconCache[componentType] = icon;
        }

        private static List<(Texture2D texture, GUIContent tooltip)> GetComponentIcons(int instanceID,
            GameObject gameObject) {
            if (gameObject == null) {
                return ComponentIconEmpty;
            }

            if (ComponentIconCache.TryGetValue(instanceID, out var cachedIcons)) {
                return cachedIcons;
            }

            ComponentContainer.Clear();
            gameObject.GetComponents(ComponentContainer);
            return ComponentIconCache[instanceID] =
                ComponentContainer.Select(component => (GetComponentTypeIcon(component),
                    new GUIContent(string.Empty, component?.GetType().Name))).ToList();
        }

        private static void DrawComponentIcons(int instanceID, GameObject gameObject, Rect rect) {
            var icons = GetComponentIcons(instanceID, gameObject);
            if (icons.Count == 0) {
                return;
            }

            var minX = rect.x + GetNameWidth(gameObject.name);
            var startX = rect.x + rect.width;
            var index = 0;
            while (index < icons.Count && startX - IconSize > minX) {
                var iconRect = new Rect(startX - IconSize, rect.y, IconSize, IconSize);
                var content = icons[index];
                if (content.texture != null) {
                    GUI.DrawTexture(iconRect, content.texture);
                }

                if (iconRect.Contains(Event.current.mousePosition)) {
                    GUI.tooltip = content.tooltip.tooltip;
                }

                index++;
                startX -= IconSize;
            }
        }
    }
}
