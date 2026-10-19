// SPDX-License-Identifier: MIT

using Grow.Core.Attributes;
using UnityEngine;

namespace Grow.Samples.GrowEditorDemo {
    /// <summary>
    /// Demo 5: value-changed callbacks (GrowOnValueChanged: calls a parameterless method on the object after the
    /// field is modified).
    /// </summary>
    [AddComponentMenu("Grow/Samples/Grow Editor Demo/05 Value Changed")]
    public sealed class ValueChangedDemo : AnnotationDemoBehaviour {
        public override string DemoSummary =>
            "Value-changed callbacks: [GrowOnValueChanged] invokes a parameterless method on this object after the field is modified, typically to refresh dependent fields or trigger a recompute.";

        [GrowLabel("Counter")]
        [GrowOnValueChanged(nameof(OnCounterChanged))]
        public int Counter;

        [GrowLabel("Tag name")]
        [GrowOnValueChanged(nameof(OnTagChanged))]
        public string TagName = "Player";

        [GrowLabel("Last change (read-only)")]
        [GrowReadOnly]
        public string LastChange = "No changes yet";

        private void OnCounterChanged() {
            LastChange = $"Counter = {Counter} (frame {Time.frameCount})";
            Debug.Log($"[{nameof(ValueChangedDemo)}] OnCounterChanged -> {LastChange}");
        }

        private void OnTagChanged() {
            LastChange = $"TagName = \"{TagName}\" (frame {Time.frameCount})";
            Debug.Log($"[{nameof(ValueChangedDemo)}] OnTagChanged -> {LastChange}");
        }
    }
}
