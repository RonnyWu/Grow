// SPDX-License-Identifier: MIT

using Grow.Lens.Editor.Core;
using Grow.Lens.Editor.Menus;
using UnityEditor;

namespace Grow.Lens.Editor.Hierarchy {
    /// <summary>Adds a GameObject menu entry for creating a formatted Hierarchy separator.</summary>
    internal static class HierarchySeparatorMenu {
        [MenuItem("GameObject/Grow/Create Separator", false, 10)]
        private static void Create() {
            GrowLensGuard.RunAction("Create Hierarchy Separator", GrowMenuActions.CreateHierarchySeparator);
        }

        [MenuItem("GameObject/Grow/Create Separator", true)]
        private static bool Validate() {
            return GrowLensSettings.Enabled && GrowMenuActions.CanCreateHierarchySeparator;
        }
    }
}
