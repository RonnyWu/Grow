// SPDX-License-Identifier: MIT

using UnityEngine;

namespace Grow.Lens.Editor.Settings {
    /// <summary>
    /// Transient serialized view of <see cref="Core.GrowLensSettings"/>. It exists only so the
    /// Preferences page can be drawn with Unity's native <c>SerializedProperty</c> fields; the
    /// JSON file remains the single source of truth.
    /// </summary>
    internal sealed class GrowLensSettingsProxy : ScriptableObject {
        /// <summary>
        /// Creates the non-persistent proxy used by the page. <see cref="HideFlags.HideAndDontSave"/>
        /// includes <see cref="HideFlags.NotEditable"/>, which would make every PropertyField
        /// read-only; remove that one flag so the native fields stay interactive.
        /// </summary>
        internal static GrowLensSettingsProxy CreateTransient() {
            var proxy = CreateInstance<GrowLensSettingsProxy>();
            proxy.hideFlags = HideFlags.HideAndDontSave & ~HideFlags.NotEditable;
            return proxy;
        }

        [SerializeField]
        private bool enabled;

        [SerializeField]
        private bool inspectorGroup;

        [SerializeField]
        private bool projectGroup;

        [SerializeField]
        private bool hierarchyGroup;

        [SerializeField]
        private bool toolbarGroup;

        [SerializeField]
        private bool hierarchyComponentIcons;

        [SerializeField]
        private bool hierarchySeparators;

        [SerializeField]
        private bool hierarchyIndentGuides;

        [SerializeField]
        private bool hierarchyGuideDashed;

        [SerializeField]
        private Color hierarchyGuideColor;

        [SerializeField]
        private bool hierarchyActiveToggle;

        [SerializeField]
        private bool hierarchyContextMenus;

        [SerializeField]
        private bool projectContextMenus;

        [SerializeField]
        private bool assetHeader;

        [SerializeField]
        private bool toolbar;

        internal bool Enabled {
            get => enabled;
            set => enabled = value;
        }

        internal bool InspectorGroup {
            get => inspectorGroup;
            set => inspectorGroup = value;
        }

        internal bool ProjectGroup {
            get => projectGroup;
            set => projectGroup = value;
        }

        internal bool HierarchyGroup {
            get => hierarchyGroup;
            set => hierarchyGroup = value;
        }

        internal bool ToolbarGroup {
            get => toolbarGroup;
            set => toolbarGroup = value;
        }

        internal bool HierarchyComponentIcons {
            get => hierarchyComponentIcons;
            set => hierarchyComponentIcons = value;
        }

        internal bool HierarchySeparators {
            get => hierarchySeparators;
            set => hierarchySeparators = value;
        }

        internal bool HierarchyIndentGuides {
            get => hierarchyIndentGuides;
            set => hierarchyIndentGuides = value;
        }

        internal bool HierarchyGuideDashed {
            get => hierarchyGuideDashed;
            set => hierarchyGuideDashed = value;
        }

        internal Color HierarchyGuideColor {
            get => hierarchyGuideColor;
            set => hierarchyGuideColor = value;
        }

        internal bool HierarchyActiveToggle {
            get => hierarchyActiveToggle;
            set => hierarchyActiveToggle = value;
        }

        internal bool HierarchyContextMenus {
            get => hierarchyContextMenus;
            set => hierarchyContextMenus = value;
        }

        internal bool ProjectContextMenus {
            get => projectContextMenus;
            set => projectContextMenus = value;
        }

        internal bool AssetHeader {
            get => assetHeader;
            set => assetHeader = value;
        }

        internal bool Toolbar {
            get => toolbar;
            set => toolbar = value;
        }
    }
}
