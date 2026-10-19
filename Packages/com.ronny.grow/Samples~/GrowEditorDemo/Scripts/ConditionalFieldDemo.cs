// SPDX-License-Identifier: MIT

using Grow.Core.Attributes;
using UnityEngine;

namespace Grow.Samples.GrowEditorDemo {
    /// <summary>Difficulty enum for the conditional demo.</summary>
    public enum Difficulty {
        [GrowLabel("Easy")] Easy,
        [GrowLabel("Normal")] Normal,
        [GrowLabel("Hard")] Hard,
    }

    /// <summary>
    /// Demo 4: conditional visibility / editability (GrowShowIf / GrowHideIf / GrowEnableIf / GrowDisableIf).
    /// <para>Condition expressions support: <c>field</c>, <c>!field</c>, <c>field == value</c> and <c>method()</c>
    /// (parameterless, returning bool).</para>
    /// </summary>
    [AddComponentMenu("Grow/Samples/Grow Editor Demo/04 Conditional Fields")]
    public sealed class ConditionalFieldDemo : AnnotationDemoBehaviour {
        public override string DemoSummary =>
            "Conditional fields: ShowIf/HideIf control visibility, EnableIf/DisableIf control editability; expressions accept a field's truthiness, ! negation, == equality (enum names included) and method() calls.";

        [GrowSeparator(1f, 4f)]
        [GrowLabel("Toggle field (referenced by several conditions)")]
        public bool Advanced = true;

        [GrowLabel("Difficulty (used by the == condition)")]
        public Difficulty Difficulty = Difficulty.Normal;

        [GrowLabel("Ready flag (used by the method condition)")]
        public bool Ready = true;

        [GrowGroup("Visibility (ShowIf / HideIf)")]
        [GrowShowIf("Advanced")]
        [GrowLabel("Expert tip (ShowIf Advanced)")]
        public string ExpertTip = "Shown only when Advanced == true";

        [GrowGroup("Visibility (ShowIf / HideIf)")]
        [GrowHideIf("Advanced")]
        [GrowLabel("Simple tip (HideIf Advanced)")]
        public string SimpleTip = "Shown only when Advanced == false";

        [GrowGroup("Editability (EnableIf / DisableIf)")]
        [GrowEnableIf("Advanced")]
        [GrowLabel("Move speed (EnableIf Advanced)")]
        public float MoveSpeed = 5f;

        [GrowGroup("Editability (EnableIf / DisableIf)")]
        [GrowDisableIf("Difficulty == Hard")]
        [GrowLabel("Hard-locked field (DisableIf Difficulty == Hard)")]
        public int HardLocked = 0;

        [GrowGroup("Method condition (method())")]
        [GrowShowIf("IsReady()")]
        [GrowLabel("Ready-only field (ShowIf IsReady())")]
        public string ReadyOnly = "Shown only when IsReady() returns true";

        private bool IsReady() {
            return Ready;
        }
    }
}
