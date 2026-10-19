// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Grow.Lens.Editor.Hierarchy {
    /// <summary>
    /// Reads the current Hierarchy window's real TreeViewGUI geometry. Unity does not expose the
    /// scene hierarchy tree view publicly, so this is reflection over 2022.3 internals. Any missing
    /// member makes the probe fail closed instead of guessing with hardcoded pixels.
    /// </summary>
    internal static class HierarchyGuideProbe {
        private static bool _initialized;
        private static bool _failed;
        private static object _cachedGuiView;
        private static HierarchyGuideContext _cached;

        private static Type _sceneHierarchyWindowType;
        private static Type _guiViewType;
        private static Type _guiMembersType;
        private static PropertyInfo _currentGuiViewProperty;
        private static FieldInfo _currentGuiViewField;
        private static MethodInfo _getAllWindows;
        private static FieldInfo _parentField;
        private static FieldInfo _sceneHierarchyField;
        private static PropertyInfo _treeViewProperty;
        private static PropertyInfo _dataProperty;
        private static MethodInfo _getRowsMethod;
        private static MethodInfo _isExpandableMethod;
        private static MethodInfo _getRowMethod;
        private static PropertyInfo _itemIdProperty;
        private static PropertyInfo _itemDepthProperty;
        private static PropertyInfo _guiProperty;
        private static PropertyInfo _isSearchingProperty;
        private static FieldInfo _baseIndentField;
        private static PropertyInfo _indentWidthProperty;
        private static PropertyInfo _foldoutStyleWidthProperty;
        private static PropertyInfo _lineStyleProperty;
        private static PropertyInfo _rowHeightProperty;
        private static PropertyInfo _topRowMarginProperty;

        /// <summary>Returns the current window context, or null when the geometry cannot be trusted.</summary>
        internal static HierarchyGuideContext TryGetContext() {
            if (_failed) {
                return null;
            }

            try {
                EnsureInitialized();
            } catch (Exception) {
                _failed = true;
                return null;
            }

            try {
                var guiView = GetCurrentGuiView();
                if (guiView != null && ReferenceEquals(guiView, _cachedGuiView) && _cached != null) {
                    return _cached;
                }

                var window = FindWindow(guiView);
                if (window == null) {
                    return null;
                }

                var context = CreateContext(window);
                _cachedGuiView = guiView;
                _cached = context;
                return context;
            } catch (Exception) {
                // Tree views can be transiently uninitialized; retry on the next repaint.
                _cached = null;
                _cachedGuiView = null;
                return null;
            }
        }

        /// <summary>Validates that the 2022.3 internal member names still resolve; tests only.</summary>
        internal static string TryValidateReflection() {
            try {
                EnsureInitialized();
                return null;
            } catch (Exception exception) {
                return exception.Message;
            }
        }

        /// <summary>Clears the cached window; hierarchy changes can recreate the tree view.</summary>
        internal static void Reset() {
            _cachedGuiView = null;
            _cached = null;
            _failed = false;
            _initialized = false;
            _guiMembersType = null;
        }

        private static void EnsureInitialized() {
            if (_initialized) {
                return;
            }

            var editorAssembly = typeof(EditorWindow).Assembly;
            _sceneHierarchyWindowType = editorAssembly.GetType("UnityEditor.SceneHierarchyWindow");
            _guiViewType = editorAssembly.GetType("UnityEditor.GUIView");
            if (_sceneHierarchyWindowType == null || _guiViewType == null) {
                throw new InvalidOperationException("SceneHierarchyWindow/GUIView not found.");
            }

            _currentGuiViewProperty = _guiViewType.GetProperty("current",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            _currentGuiViewField = _currentGuiViewProperty == null
                ? _guiViewType.GetField("current", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                : null;
            if (_currentGuiViewProperty == null && _currentGuiViewField == null) {
                throw new InvalidOperationException("GUIView.current not found.");
            }

            _getAllWindows = _sceneHierarchyWindowType.GetMethod("GetAllSceneHierarchyWindows",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            _parentField = typeof(EditorWindow).GetField("m_Parent",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _sceneHierarchyField = _sceneHierarchyWindowType.GetField("m_SceneHierarchy",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (_getAllWindows == null || _parentField == null || _sceneHierarchyField == null) {
                throw new InvalidOperationException("Hierarchy window members not found.");
            }

            _treeViewProperty = _sceneHierarchyField.FieldType.GetProperty("treeView",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (_treeViewProperty == null) {
                throw new InvalidOperationException("SceneHierarchy.treeView not found.");
            }

            _guiProperty = _treeViewProperty.PropertyType.GetProperty("gui",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _isSearchingProperty = _treeViewProperty.PropertyType.GetProperty("isSearching",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _dataProperty = _treeViewProperty.PropertyType.GetProperty("data",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (_guiProperty == null || _isSearchingProperty == null || _dataProperty == null) {
                throw new InvalidOperationException("TreeViewController members not found.");
            }

            var dataType = _dataProperty.PropertyType;
            _getRowsMethod = FindMethod(dataType, "GetRows");
            _isExpandableMethod = FindMethod(dataType, "IsExpandable");
            _getRowMethod = FindMethod(dataType, "GetRow");
            var treeViewItemType = editorAssembly.GetType("UnityEditor.IMGUI.Controls.TreeViewItem");
            _itemIdProperty = treeViewItemType == null ? null : FindProperty(treeViewItemType, "id");
            _itemDepthProperty = treeViewItemType == null ? null : FindProperty(treeViewItemType, "depth");
            if (_getRowsMethod == null || _isExpandableMethod == null || _getRowMethod == null ||
                _itemIdProperty == null || _itemDepthProperty == null) {
                throw new InvalidOperationException("TreeView item members not found.");
            }

            var guiType = editorAssembly.GetType("UnityEditor.GameObjectTreeViewGUI");
            if (guiType == null) {
                throw new InvalidOperationException("GameObjectTreeViewGUI not found.");
            }

            EnsureGuiMembers(guiType);
            _initialized = true;
        }

        private static void EnsureGuiMembers(Type guiType) {
            if (guiType == _guiMembersType) {
                return;
            }

            var baseIndentField = FindField(guiType, "k_BaseIndent");
            var indentWidthProperty = FindProperty(guiType, "indentWidth");
            var foldoutStyleWidthProperty = FindProperty(guiType, "foldoutStyleWidth");
            var lineStyleProperty = FindProperty(guiType, "lineStyle");
            var rowHeightProperty = FindProperty(guiType, "k_LineHeight");
            var topRowMarginProperty = FindProperty(guiType, "topRowMargin");
            if (baseIndentField == null || indentWidthProperty == null ||
                foldoutStyleWidthProperty == null || lineStyleProperty == null ||
                rowHeightProperty == null || topRowMarginProperty == null) {
                throw new InvalidOperationException(
                    "TreeViewGUI geometry members not found: " +
                    $"{nameof(baseIndentField)}={baseIndentField != null}, " +
                    $"{nameof(indentWidthProperty)}={indentWidthProperty != null}, " +
                    $"{nameof(foldoutStyleWidthProperty)}={foldoutStyleWidthProperty != null}, " +
                    $"{nameof(lineStyleProperty)}={lineStyleProperty != null}, " +
                    $"{nameof(rowHeightProperty)}={rowHeightProperty != null}, " +
                    $"{nameof(topRowMarginProperty)}={topRowMarginProperty != null}");
            }

            _baseIndentField = baseIndentField;
            _indentWidthProperty = indentWidthProperty;
            _foldoutStyleWidthProperty = foldoutStyleWidthProperty;
            _lineStyleProperty = lineStyleProperty;
            _rowHeightProperty = rowHeightProperty;
            _topRowMarginProperty = topRowMarginProperty;
            _guiMembersType = guiType;
        }

        private static MethodInfo FindMethod(Type type, string name) {
            for (var current = type; current != null; current = current.BaseType) {
                var method = current.GetMethod(name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);
                if (method != null) {
                    return method;
                }
            }

            return null;
        }

        private static PropertyInfo FindProperty(Type type, string name) {
            for (var current = type; current != null; current = current.BaseType) {
                var property = current.GetProperty(name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);
                if (property != null) {
                    return property;
                }
            }

            return null;
        }

        private static FieldInfo FindField(Type type, string name) {
            for (var current = type; current != null; current = current.BaseType) {
                var field = current.GetField(name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);
                if (field != null) {
                    return field;
                }
            }

            return null;
        }

        private static object GetCurrentGuiView() {
            return _currentGuiViewProperty != null
                ? _currentGuiViewProperty.GetValue(null)
                : _currentGuiViewField?.GetValue(null);
        }

        private static object FindWindow(object guiView) {
            if (_getAllWindows.Invoke(null, null) is not IEnumerable windows) {
                return null;
            }

            object fallback = null;
            var count = 0;
            foreach (var window in windows) {
                count++;
                fallback = window;
                if (guiView != null && ReferenceEquals(_parentField.GetValue(window), guiView)) {
                    return window;
                }
            }

            // A single Hierarchy window is unambiguous even when GUIView.current is unavailable.
            return count == 1 ? fallback : null;
        }

        private static HierarchyGuideContext CreateContext(object window) {
            var sceneHierarchy = _sceneHierarchyField.GetValue(window);
            var treeView = sceneHierarchy == null ? null : _treeViewProperty.GetValue(sceneHierarchy);
            var gui = treeView == null ? null : _guiProperty.GetValue(treeView);
            var data = treeView == null ? null : _dataProperty.GetValue(treeView);
            if (treeView == null || gui == null || data == null) {
                throw new InvalidOperationException("Tree view is not initialized.");
            }

            EnsureGuiMembers(gui.GetType());
            return new HierarchyGuideContext(treeView, gui, data, _isSearchingProperty, _baseIndentField,
                _indentWidthProperty, _foldoutStyleWidthProperty, _lineStyleProperty,
                _rowHeightProperty, _topRowMarginProperty, _getRowsMethod, _isExpandableMethod, _getRowMethod,
                _itemIdProperty, _itemDepthProperty);
        }
    }

    /// <summary>Per-window geometry values used by the indent guide renderer.</summary>
    internal sealed class HierarchyGuideContext {
        private readonly object _treeView;
        private readonly object _gui;
        private readonly object _data;
        private readonly PropertyInfo _isSearchingProperty;
        private readonly FieldInfo _baseIndentField;
        private readonly PropertyInfo _indentWidthProperty;
        private readonly PropertyInfo _foldoutStyleWidthProperty;
        private readonly PropertyInfo _lineStyleProperty;
        private readonly PropertyInfo _rowHeightProperty;
        private readonly PropertyInfo _topRowMarginProperty;
        private readonly MethodInfo _getRowsMethod;
        private readonly MethodInfo _isExpandableMethod;
        private readonly MethodInfo _getRowMethod;
        private readonly PropertyInfo _itemIdProperty;
        private readonly PropertyInfo _itemDepthProperty;
        private IList _rowDepthsSource;
        private int[] _rowDepths;

        internal HierarchyGuideContext(object treeView, object gui, object data,
            PropertyInfo isSearchingProperty, FieldInfo baseIndentField, PropertyInfo indentWidthProperty,
            PropertyInfo foldoutStyleWidthProperty, PropertyInfo lineStyleProperty,
            PropertyInfo rowHeightProperty, PropertyInfo topRowMarginProperty, MethodInfo getRowsMethod,
            MethodInfo isExpandableMethod, MethodInfo getRowMethod, PropertyInfo itemIdProperty, PropertyInfo itemDepthProperty) {
            _treeView = treeView;
            _gui = gui;
            _data = data;
            _isSearchingProperty = isSearchingProperty;
            _baseIndentField = baseIndentField;
            _indentWidthProperty = indentWidthProperty;
            _foldoutStyleWidthProperty = foldoutStyleWidthProperty;
            _lineStyleProperty = lineStyleProperty;
            _rowHeightProperty = rowHeightProperty;
            _topRowMarginProperty = topRowMarginProperty;
            _getRowsMethod = getRowsMethod;
            _isExpandableMethod = isExpandableMethod;
            _getRowMethod = getRowMethod;
            _itemIdProperty = itemIdProperty;
            _itemDepthProperty = itemDepthProperty;
        }

        internal bool IsSearching => (bool)_isSearchingProperty.GetValue(_treeView);

        internal float BaseIndent => ReadFloat(_baseIndentField.GetValue(_gui));

        internal float IndentWidth => ReadFloat(_indentWidthProperty.GetValue(_gui));

        internal float FoldoutWidth => ReadFloat(_foldoutStyleWidthProperty.GetValue(_gui));

        internal float RowHeight => ReadFloat(_rowHeightProperty.GetValue(_gui));

        internal float TopRowMargin => ReadFloat(_topRowMarginProperty.GetValue(_gui));

        internal IList Rows => _getRowsMethod.Invoke(_data, null) as IList;

        internal int[] GetRowDepths(IList rows) {
            if (rows == null) {
                return null;
            }

            if (ReferenceEquals(rows, _rowDepthsSource) && _rowDepths != null &&
                _rowDepths.Length == rows.Count) {
                return _rowDepths;
            }

            var depths = new int[rows.Count];
            for (var i = 0; i < rows.Count; i++) {
                var item = rows[i];
                depths[i] = item == null ? -1 : ItemDepth(item);
            }

            _rowDepthsSource = rows;
            _rowDepths = depths;
            return depths;
        }

        internal int ItemId(object item) => (int)_itemIdProperty.GetValue(item);

        internal int ItemDepth(object item) => (int)_itemDepthProperty.GetValue(item);

        internal int GetRow(int instanceID) => (int)_getRowMethod.Invoke(_data, new object[] { instanceID });

        internal bool IsExpandable(object item) =>
            (bool)_isExpandableMethod.Invoke(_data, new[] { item });

        internal float MarginLeft {
            get {
                var style = _lineStyleProperty.GetValue(_gui) as GUIStyle;
                return style?.margin.left ?? 0f;
            }
        }

        private static float ReadFloat(object value) {
            if (value is not float result || float.IsNaN(result) || float.IsInfinity(result)) {
                throw new InvalidOperationException("Invalid TreeViewGUI geometry.");
            }

            return result;
        }
    }
}
