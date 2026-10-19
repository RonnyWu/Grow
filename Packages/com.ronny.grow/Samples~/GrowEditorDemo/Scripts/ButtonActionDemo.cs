// SPDX-License-Identifier: MIT

using Grow.Core.Attributes;
using UnityEngine;

namespace Grow.Samples.GrowEditorDemo {
    /// <summary>Mode enum for the button parameter demo.</summary>
    public enum DemoMode {
        [GrowLabel("Add")] Add,
        [GrowLabel("Multiply")] Multiply,
    }

    /// <summary>
    /// Demo 6: [GrowButton] method buttons (no parameter / single parameter / multiple parameters).
    /// <para>Methods with parameters get generated input controls, supporting int/long/float/double/bool/string/enum,
    /// and the last input is persisted in EditorPrefs; fields and buttons can share one Box group.</para>
    /// </summary>
    [AddComponentMenu("Grow/Samples/Grow Editor Demo/06 Button Actions")]
    public sealed class ButtonActionDemo : AnnotationDemoBehaviour {
        public override string DemoSummary =>
            "[GrowButton] method buttons: methods without parameters are invoked directly; methods with parameters get generated input controls (int/float/bool/enum...) persisted between sessions; [GrowGroup(..., Box)] puts fields and buttons in the same card.";

        [GrowGroup("Numeric")]
        [GrowLabel("Current value")]
        public int Value;

        [GrowGroup("Numeric")]
        [GrowLabel("Factor")]
        public float Factor = 1.5f;

        [GrowLabel("Status (read-only)")]
        [GrowReadOnly]
        public string Status = "Ready";

        [GrowButton("Randomize"), GrowGroup("Actions")]
        public void Randomize() {
            Value = Random.Range(0, 100);
            Status = $"Randomize -> {Value}";
        }

        [GrowButton("Accumulate (int parameter)"), GrowGroup("Actions")]
        public void Add(int delta) {
            Value += delta;
            Status = $"Add {delta} -> {Value}";
        }

        [GrowButton("Apply (float / bool / enum parameters)"), GrowGroup("Actions")]
        public void Apply(float scale, bool round, DemoMode mode) {
            var result = mode == DemoMode.Multiply ? Value * scale : Value + scale;
            Value = round ? Mathf.RoundToInt(result) : (int)result;
            Status = $"Apply scale={scale}, round={round}, mode={mode} -> {Value}";
        }

        [GrowGroup("Debug Tools", GrowGroupKind.Box)]
        [GrowLabel("Debug switch (same group as the button below)")]
        public bool DebugEnabled = true;

        [GrowGroup("Debug Tools", GrowGroupKind.Box), GrowButton("Reset")]
        public void Reset() {
            Value = 0;
            Factor = 1.5f;
            Status = "Reset";
        }
    }
}
