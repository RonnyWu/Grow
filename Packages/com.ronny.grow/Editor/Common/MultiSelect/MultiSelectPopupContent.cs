// SPDX-License-Identifier: MIT

using System;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Grow.Editor.Common.MultiSelect {
    /// <summary>
    /// The PopupWindowContent hosting <see cref="MultiSelectTreeView"/>: supports searching, collapsing, native
    /// checkboxes, and stays open across consecutive interactions.
    /// </summary>
    internal class MultiSelectPopupContent : PopupWindowContent {
        private const float Width = 400f;
        private const float MaxHeight = 800f;
        private const float SearchBarH = 18f;
        private const float VPad = 3f;

        private readonly IMultiSelectBackend _backend;
        private readonly SearchField _searchField;
        private readonly MultiSelectTreeView _treeView;
        private string _search = string.Empty;

        /// <summary>Builds the popup and expands every node immediately.</summary>
        /// <param name="backend">The storage backend.</param>
        /// <param name="cache">The display data.</param>
        public MultiSelectPopupContent(IMultiSelectBackend backend, MultiSelectData cache) {
            _backend = backend;
            var treeState = new TreeViewState();
            _treeView = new MultiSelectTreeView(treeState, backend, cache);
            _treeView.Reload();
            _treeView.ExpandAll();

            _searchField = new SearchField();
            _searchField.downOrUpArrowKeyPressed += _treeView.SetFocusAndEnsureSelectedItem;
        }

        public override void OnClose() {
            if (_backend is IDisposable disposable) {
                disposable.Dispose();
            }

            base.OnClose();
        }

        public override Vector2 GetWindowSize() {
            var contentH = SearchBarH + VPad * 3 + _treeView.totalHeight + VPad;
            var height = Mathf.Clamp(contentH, 80f, MaxHeight);
            return new Vector2(Width, height);
        }

        public override void OnGUI(Rect rect) {
            var searchRect = new Rect(rect.x + VPad, rect.y + VPad, rect.width - VPad * 2, SearchBarH);
            var nextSearch = _searchField.OnGUI(searchRect, _search) ?? string.Empty;
            if (!string.Equals(_search, nextSearch, System.StringComparison.Ordinal)) {
                _search = nextSearch;
                _treeView.SetSearch(nextSearch);
            }

            var treeRect = new Rect(rect.x,
                searchRect.yMax + VPad,
                rect.width,
                rect.height - SearchBarH - VPad * 3);
            _treeView.OnGUI(treeRect);
        }
    }
}
