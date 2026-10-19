// SPDX-License-Identifier: MIT

using Grow.Core.Attributes;
using UnityEngine;

namespace Grow.Samples.GrowEditorDemo {
    /// <summary>
    /// Demo 1: field decoration annotations (GrowLabel / Tooltip / Range / GrowReadOnly / GrowHelpBox /
    /// GrowSeparator).
    /// <para>This component <b>deliberately has no driving Editor</b>: these annotations are implemented by
    /// PropertyDrawer / DecoratorDrawer and already work under Unity's default Inspector (Tier A), which makes it a
    /// reference for how annotations degrade gracefully outside GrowEditor.</para>
    /// </summary>
    [AddComponentMenu("Grow/Samples/Grow Editor Demo/01 Field Decoration")]
    public sealed class FieldDecorationDemo : MonoBehaviour {
        [GrowHelpBox("HelpBox offers Info / Warning / Error levels, commonly used for notes above a field (also works in the default Inspector).", GrowHelpBoxKind.Info)]
        [GrowLabel("Nickname")]
        [Tooltip("Label replaces the field's default display name; Tooltip provides the mouse-over hint")]
        public string Nickname = "Grow Editor";

        [GrowReadOnly]
        [Tooltip("ReadOnly only affects drawing (the field keeps its default name); the underlying value can still be written by code and serialization. GrowLabel and GrowReadOnly can be combined: they are drawn as a chain by order")]
        public string Creator = "Grow.Samples";

        [GrowSeparator(1f, 6f)]
        [Range(1, 99)]
        [Tooltip("A plain [Range] field: uses Unity's native slider in the default Inspector")]
        public int Level = 8;

        [Range(0f, 100f)]
        [Tooltip("A plain [Range] float field")]
        public float Health = 88.5f;

        [GrowLabel("Note (Label used on its own)")]
        [Tooltip("When not combined with other drawing annotations, Label simply replaces the label text")]
        public string Note = "Tier A annotations: GrowLabel / GrowReadOnly / GrowHelpBox / GrowSeparator / GrowEnumFlags.";
    }
}
