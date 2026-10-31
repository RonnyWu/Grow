// SPDX-License-Identifier: MIT

using Grow.Lens.Editor.Core;
using UnityEngine;

namespace Grow.Lens.Editor.Settings {
    /// <summary>Transient serialized view used by the native Preferences fields.</summary>
    internal sealed class GrowLensSettingsProxy : ScriptableObject {
        public GrowLensSettings.Data Data;
        public bool HierarchyGroup;
    }
}
