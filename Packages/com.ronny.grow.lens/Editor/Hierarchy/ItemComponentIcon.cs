// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        private static bool _missingScriptIconResolved;
        private static bool _scriptIconResolved;
        private static GUIContent _nameWidthContent;
        private static Type _propertyEditorType;
        private static MethodInfo _openPropertyEditor;
        private static bool _propertyEditorResolved;

        private static readonly Dictionary<string, float> NameWidthCache = new();
        private static readonly Dictionary<Type, Texture2D> ComponentTypeIconCache = new();
        private static readonly Dictionary<int, List<ComponentIconEntry>> ComponentIconCache = new();
        private static readonly List<ComponentIconEntry> ComponentIconEmpty = new();
        private static readonly List<Component> ComponentContainer = new();

        private readonly struct ComponentIconEntry {
            internal readonly Component Component;
            internal readonly Texture2D Texture;
            internal readonly GUIContent Tooltip;

            internal ComponentIconEntry(Component component, Texture2D texture, GUIContent tooltip) {
                Component = component;
                Texture = texture;
                Tooltip = tooltip;
            }
        }

        internal static void OnGameObjectGUI(int instanceID, GameObject gameObject, Rect rect) {
            if (!_missingScriptIconResolved) {
                _missingScriptIcon = EditorGUIUtility.IconContent(MissingScriptIconName).image as Texture2D;
                _missingScriptIconResolved = true;
            }

            var icons = GetComponentIcons(instanceID, gameObject);
            if (icons.Count == 0) {
                return;
            }

            var visibleCount =
                ComponentIconLayout.GetVisibleCount(rect, GetNameWidth(gameObject.name), icons.Count, IconSize);
            for (var index = 0; index < visibleCount; index++) {
                var iconRect = ComponentIconLayout.GetRect(rect, index, IconSize);
                var entry = icons[index];
                if (entry.Texture != null) {
                    GUI.DrawTexture(iconRect, entry.Texture);
                }

                if (Event.current != null && iconRect.Contains(Event.current.mousePosition)) {
                    GUI.tooltip = entry.Tooltip.tooltip;
                }
            }
        }

        /// <summary>Returns true when the event is a plain left click on a visible component icon.</summary>
        internal static bool TryHandleClick(int instanceID, GameObject gameObject, Rect rect) {
            var current = Event.current;
            if (current == null || current.type != EventType.MouseDown || current.button != 0 ||
                current.control || current.shift || current.alt || current.command ||
                !rect.Contains(current.mousePosition)) {
                return false;
            }

            var icons = GetComponentIcons(instanceID, gameObject);
            if (icons.Count == 0) {
                return false;
            }

            var visibleCount =
                ComponentIconLayout.GetVisibleCount(rect, GetNameWidth(gameObject.name), icons.Count, IconSize);
            for (var index = 0; index < visibleCount; index++) {
                if (!ComponentIconLayout.GetRect(rect, index, IconSize).Contains(current.mousePosition)) {
                    continue;
                }

                var component = icons[index].Component;
                if (component == null) {
                    return false;
                }

                OpenComponent(component);
                return true;
            }

            return false;
        }

        internal static void OnHierarchyChanged() {
            NameWidthCache.Clear();
            ComponentIconCache.Clear();
            ComponentIconEmpty.Clear();
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
            if (!_scriptIconResolved) {
                _scriptIcon = EditorGUIUtility.FindTexture(ScriptIconName);
                _scriptIconResolved = true;
            }

            if (component == null) {
                return _missingScriptIcon;
            }

            var componentType = component.GetType();
            if (ComponentTypeIconCache.TryGetValue(componentType, out var cachedIcon)) {
                return cachedIcon;
            }

            var icon = EditorGUIUtility.ObjectContent(component, componentType).image as Texture2D;
            return ComponentTypeIconCache[componentType] = icon ?? _scriptIcon;
        }

        private static List<ComponentIconEntry> GetComponentIcons(int instanceID, GameObject gameObject) {
            if (gameObject == null) {
                return ComponentIconEmpty;
            }

            if (ComponentIconCache.TryGetValue(instanceID, out var cachedIcons)) {
                return cachedIcons;
            }

            ComponentContainer.Clear();
            gameObject.GetComponents(ComponentContainer);
            return ComponentIconCache[instanceID] = ComponentContainer.Select(ToEntry).ToList();
        }

        private static ComponentIconEntry ToEntry(Component component) {
            return new ComponentIconEntry(component, GetComponentTypeIcon(component),
                new GUIContent(string.Empty,
                    component == null ? "Missing Script" : component.GetType().Name));
        }

        private static void OpenComponent(Component component) {
            if (component == null) {
                return;
            }

            // Defer window creation until after the Hierarchy row's IMGUI event.
            EditorApplication.delayCall += () => OpenFocusedInspector(component);
        }

        private static void OpenFocusedInspector(Component component) {
            if (component == null) {
                return;
            }

            if (!ResolveFocusedInspector()) {
                // Older Unity versions may not expose PropertyEditor; keep a usable fallback.
                Selection.activeObject = component;
                EditorApplication.ExecuteMenuItem("Window/General/Inspector");
                return;
            }

            try {
                _openPropertyEditor.Invoke(null, new object[] { component, true });
            } catch (Exception) {
                Selection.activeObject = component;
                EditorApplication.ExecuteMenuItem("Window/General/Inspector");
            }
        }

        /// <summary>Validates the internal member names used by the focused-Inspector action; tests only.</summary>
        internal static string TryValidateFocusedInspector() {
            return ResolveFocusedInspector()
                ? null
                : "UnityEditor.PropertyEditor.OpenPropertyEditor(Object, bool) not found.";
        }

        private static bool ResolveFocusedInspector() {
            if (_propertyEditorResolved) {
                return _openPropertyEditor != null;
            }

            _propertyEditorResolved = true;
            _propertyEditorType = typeof(EditorWindow).Assembly.GetType("UnityEditor.PropertyEditor");
            _openPropertyEditor = _propertyEditorType?.GetMethod("OpenPropertyEditor",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null,
                new[] { typeof(UnityEngine.Object), typeof(bool) }, null);
            return _openPropertyEditor != null;
        }
    }
}
