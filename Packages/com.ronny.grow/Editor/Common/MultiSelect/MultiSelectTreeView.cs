// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Grow.Editor.Common.MultiSelect {
    /// <summary>
    /// Hierarchical multi-select TreeView:
    /// <para>- Builds a collapsible menu from the '/' hierarchy in the label (multi-level grouping
    /// at any depth)</para>
    /// <para>- Sorts child nodes of each level by label</para>
    /// <para>- Parent nodes offer tri-state toggles (all / partial / none), leaf nodes offer a single toggle</para>
    /// <para>- Nothing / Everything are pinned to the top as fixed rows</para>
    /// <para>Storage-agnostic: all reads and writes go through <see cref="IMultiSelectBackend"/>.</para>
    /// </summary>
    internal class MultiSelectTreeView : TreeView {
        // Sentinel markers (negative values never collide with real values)
        private const int CategoryBit = -1;
        private const int NothingBit = -2;
        private const int EverythingBit = -3;

        private static readonly Color SelectedTextColor = new(0, 0.694f, 0.302f, 1f);

        private readonly IMultiSelectBackend _backend;
        private readonly MultiSelectData _cache;

        private int _idCounter;
        private string _search = string.Empty;
        private GUIStyle _normalStyle;
        private GUIStyle _selectedStyle;

        /// <summary>Builds the hierarchical multi-select tree.</summary>
        /// <param name="state">TreeView expand/selection state (owned by the caller).</param>
        /// <param name="backend">The storage backend.</param>
        /// <param name="cache">The display data.</param>
        public MultiSelectTreeView(TreeViewState state, IMultiSelectBackend backend, MultiSelectData cache) : base(state) {
            _backend = backend;
            _cache = cache;
            showBorder = true;
            showAlternatingRowBackgrounds = true;
            rowHeight = EditorGUIUtility.singleLineHeight + 2f;
        }

        /// <summary>Applies a search filter without relying on TreeView's flat name search.</summary>
        /// <param name="search">Text to match against the full label; empty clears the filter.</param>
        internal void SetSearch(string search) {
            search = (search ?? string.Empty).Trim();
            if (string.Equals(_search, search, StringComparison.Ordinal)) {
                return;
            }

            _search = search;
            Reload();
            ExpandAll();
        }

        /// <summary>Writes the node's toggle state to the backend and requests a repaint.</summary>
        /// <param name="item">The node being operated on.</param>
        /// <param name="newValue">The new toggle state.</param>
        private void ApplyChange(Item item, bool newValue) {
            switch (item.BitIndex) {
                case NothingBit: _backend.Clear(); break;
                case EverythingBit: _backend.SelectAll(_cache.Values); break;
                case CategoryBit:
                    foreach (var bit in item.LeafBits) {
                        _backend.SetValue(bit, newValue);
                    }

                    break;
                default: _backend.SetValue(item.BitIndex, newValue); break;
            }

            _backend.Apply();
            Repaint();
        }

        /// <summary>A TreeView node carrying multi-select state.</summary>
        private sealed class Item : TreeViewItem {
            /// <summary>A real value for leaves; a negative marker for categories and fixed rows.</summary>
            public int BitIndex = CategoryBit;

            /// <summary>Trailing detail after the label, formatted "Name = Value" (leaves only).</summary>
            public string InfoText;

            /// <summary>All leaf values under a category node (or combination alias row), used for
            /// tri-state and batch operations.</summary>
            public List<int> LeafBits;

            /// <summary>Whether the entry is [Obsolete] (disabled, dimmed and non-interactive).</summary>
            public bool Obsolete;

            /// <summary>Full text used for search matching (leaves and combination alias rows only).</summary>
            public string SearchText;
        }

        protected override TreeViewItem BuildRoot() {
            _idCounter = 0;
            var root = new Item {
                id = _idCounter++,
                depth = -1,
                displayName = "Root",
                BitIndex = CategoryBit
            };

            var pathMap = new Dictionary<string, Item>();

            for (var i = 0; i < _cache.Values.Length; i++) {
                var value = _cache.Values[i];
                var label = _cache.Labels[i].Replace("__", "");
                var obsolete = _cache.Obsoletes != null && i < _cache.Obsoletes.Length && _cache.Obsoletes[i];

                // Parse "{value} : {Name} : {Label}" into the trailing detail "Name = Value"
                var parts = label.Split(new[] { " : " }, StringSplitOptions.None);
                var name = parts.Length == 3 ? parts[1] : label;
                var infoText = $"{name} = {value}";

                var segments = SplitPath(label);

                // Create or find category nodes level by level (at any depth)
                var parent = root;
                var accum = "";
                for (var k = 0; k < segments.Length - 1; k++) {
                    var seg = segments[k];
                    accum += "/" + seg;
                    if (!pathMap.TryGetValue(accum, out var category)) {
                        category = new Item {
                            id = _idCounter++,
                            displayName = seg,
                            BitIndex = CategoryBit
                        };
                        parent.AddChild(category);
                        pathMap[accum] = category;
                    }

                    parent = category;
                }

                var leaf = new Item {
                    id = _idCounter++,
                    displayName = segments[^1],
                    BitIndex = value,
                    SearchText = label,
                    InfoText = infoText,
                    Obsolete = obsolete
                };
                parent.AddChild(leaf);
            }

            SortRecursive(root);

            var everything = new Item {
                id = _idCounter++,
                displayName = _cache.EverythingLabel,
                BitIndex = EverythingBit,
                InfoText = _cache.EverythingInfo
            };
            var nothing = new Item {
                id = _idCounter++,
                displayName = _cache.NothingLabel,
                BitIndex = NothingBit,
                InfoText = _cache.NothingInfo
            };
            root.children ??= new List<TreeViewItem>();
            root.children.Insert(0, everything);
            root.children.Insert(0, nothing);
            everything.parent = root;
            nothing.parent = root;

            // Combination alias rows ([Flags] mode only): multi-bit defined members rendered as whole-row toggles at
            // the bottom, reusing the CategoryBit tri-state / batch-toggle semantics (BitIndex < 0 never collides
            // with a real value).
            if (_cache.ComboValues != null) {
                var obsoleteByValue = new Dictionary<int, bool>();
                for (var i = 0; i < _cache.Values.Length; i++) {
                    obsoleteByValue[_cache.Values[i]] = _cache.Obsoletes != null && i < _cache.Obsoletes.Length && _cache.Obsoletes[i];
                }

                for (var c = 0; c < _cache.ComboValues.Length; c++) {
                    var comboValue = _cache.ComboValues[c];
                    var parts = _cache.ComboLabels[c].Split(new[] { " : " }, StringSplitOptions.None);
                    var name = parts.Length == 3 ? parts[1] : _cache.ComboLabels[c];

                    var combo = new Item {
                        id = _idCounter++,
                        displayName = name,
                        BitIndex = CategoryBit,
                        InfoText = $"{name} = {comboValue}",
                        SearchText = _cache.ComboLabels[c]
                    };
                    // Component bits = the single-bit subset of presented leaves (excluding
                    // Obsolete entries, matching
                    // the category tri-state rule); skip the row when no usable bit remains
                    combo.LeafBits = _cache.Values
                        .Where(v => (comboValue & v) == v && !obsoleteByValue[v])
                        .ToList();
                    if (combo.LeafBits.Count == 0) {
                        continue;
                    }

                    root.AddChild(combo);
                }
            }

            if (!string.IsNullOrEmpty(_search)) {
                FilterRecursive(root, _search);
            }

            FillLeafBits(root);

            SetupDepthsFromParentsAndChildren(root);
            return root;
        }

        /// <summary>
        /// Keeps matching leaves and combiners, prunes empty groups, and hides Nothing / Everything.
        /// </summary>
        private static bool FilterRecursive(Item item, string search) {
            if (item.children == null) {
                return false;
            }

            for (var i = item.children.Count - 1; i >= 0; i--) {
                var child = (Item)item.children[i];
                if (child.SearchText != null) {
                    if (child.SearchText.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) {
                        item.children.RemoveAt(i);
                    }
                } else if (child.BitIndex == CategoryBit) {
                    if (!FilterRecursive(child, search)) {
                        item.children.RemoveAt(i);
                    }
                } else {
                    // Nothing / Everything are convenience rows, not search results.
                    item.children.RemoveAt(i);
                }
            }

            return item.children.Count > 0;
        }

        /// <summary>Parses "{value} : {Name} : {Label}" and uses the '/' characters in the Label as
        /// a hierarchy path (any depth allowed).</summary>
        private static string[] SplitPath(string label) {
            var parts = label.Split(new[] { " : " }, StringSplitOptions.None);
            var pathSource = parts.Length == 3 ? parts[2] : label;

            var segments = pathSource
                .Split('/')
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToArray();

            return segments.Length > 0 ? segments : new[] { label };
        }

        /// <summary>Recursively sorts child nodes by displayName, case-insensitively.</summary>
        private static void SortRecursive(TreeViewItem item) {
            if (item.children == null) {
                return;
            }

            item.children.Sort((a, b) => string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase));
            foreach (var child in item.children) {
                SortRecursive(child);
            }
        }

        /// <summary>Recursively collects the leaf values of every node (used for tri-state
        /// evaluation; [Obsolete] entries are excluded).</summary>
        private static void FillLeafBits(Item item) {
            item.LeafBits ??= new List<int>();
            if (item.children == null) {
                return;
            }

            foreach (var child in item.children.Cast<Item>()) {
                FillLeafBits(child);
                if (child.BitIndex >= 0 && !child.Obsolete) {
                    item.LeafBits.Add(child.BitIndex);
                } else if (child.BitIndex == CategoryBit) {
                    item.LeafBits.AddRange(child.LeafBits);
                }
            }
        }

        private void EnsureStyles() {
            if (_normalStyle != null) {
                return;
            }

            _normalStyle = new GUIStyle(EditorStyles.label) {
                richText = false,
                alignment = TextAnchor.MiddleLeft
            };

            _selectedStyle = new GUIStyle(_normalStyle) {
                fontStyle = FontStyle.Bold
            };
            _selectedStyle.normal.textColor = SelectedTextColor;
            _selectedStyle.hover.textColor = SelectedTextColor;
        }

        protected override void RowGUI(RowGUIArgs args) {
            EnsureStyles();

            var item = (Item)args.item;
            var rect = args.rowRect;
            var indent = GetContentIndent(item);

            var toggleRect = new Rect(rect.x + indent, rect.y + (rect.height - 16f) * 0.5f, 16f, 16f);
            var labelRect = new Rect(toggleRect.xMax + 4f, rect.y, rect.width - toggleRect.xMax - 6f, rect.height);

            var isOn = false;
            var mixed = false;

            switch (item.BitIndex) {
                case NothingBit:
                    isOn = !_backend.IsMixed && _cache.Values.All(v => !_backend.Contains(v));
                    mixed = _backend.IsMixed && _cache.Values.Any(_backend.IsMixedAt);
                    break;
                case EverythingBit:
                    isOn = !_backend.IsMixed && _cache.Values.Length > 0 &&
                        _cache.Values.All(_backend.Contains);
                    mixed = _backend.IsMixed && _cache.Values.Any(_backend.IsMixedAt);
                    break;
                case CategoryBit: {
                        var onCount = item.LeafBits.Count(_backend.Contains);
                        isOn = item.LeafBits.Count > 0 && onCount == item.LeafBits.Count;
                        mixed = item.LeafBits.Count(_backend.IsMixedAt) > 0 ||
                            onCount > 0 && onCount < item.LeafBits.Count;
                        break;
                    }
                default:
                    mixed = _backend.IsMixedAt(item.BitIndex);
                    isOn = !mixed && _backend.Contains(item.BitIndex);
                    break;
            }

            var previousMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = mixed;
            try {
                using (new EditorGUI.DisabledScope(item.Obsolete)) {
                    EditorGUI.BeginChangeCheck();
                    var newValue = EditorGUI.Toggle(toggleRect, isOn);
                    if (EditorGUI.EndChangeCheck()) {
                        ApplyChange(item, newValue);
                    }

                    var style = isOn && !mixed ? _selectedStyle : _normalStyle;
                    GUI.Label(labelRect, item.displayName, style);

                    if (!string.IsNullOrEmpty(item.InfoText)) {
                        var nameWidth = style.CalcSize(new GUIContent(item.displayName)).x;
                        var infoRect = new Rect(labelRect.x + nameWidth + 12f, labelRect.y,
                            labelRect.width - nameWidth - 12f, labelRect.height);
                        var prevInfo = GUI.enabled;
                        GUI.enabled = false;
                        GUI.Label(infoRect, item.InfoText, _normalStyle);
                        GUI.enabled = prevInfo;
                    }
                }
            } finally {
                EditorGUI.showMixedValue = previousMixed;
            }

            // Clicking anywhere on the row (outside the checkbox) toggles it, so the small box need
            // not be hit exactly
            if (!item.Obsolete) {
                var clickRect = new Rect(toggleRect.xMax, rect.y, rect.width - (toggleRect.xMax - rect.x), rect.height);
                var current = Event.current;
                if (current.type == EventType.MouseDown && current.button == 0 && clickRect.Contains(current.mousePosition)) {
                    ApplyChange(item, !isOn || mixed);
                    current.Use();
                }
            }
        }
    }
}
