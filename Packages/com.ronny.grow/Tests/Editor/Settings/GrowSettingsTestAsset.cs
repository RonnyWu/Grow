// SPDX-License-Identifier: MIT

using UnityEngine;

namespace Grow.Tests.Editor.Settings {
    /// <summary>Test-only settings asset used by <see cref="GrowSettingsAssetManagerTests"/>.</summary>
    public sealed class GrowSettingsTestAsset : ScriptableObject {
        public int Value = 7;
    }
}
