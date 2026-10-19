// SPDX-License-Identifier: MIT

using Grow.Core.Attributes;
using UnityEngine;

namespace Grow.Samples.GrowEditorDemo {
    /// <summary>
    /// Demo 3: grouping and ordering (GrowGroup Foldout / Box, '/' nested paths, GrowPropertyOrder within a group).
    /// <para>These annotations only take effect under a <c>GrowEditor</c>.</para>
    /// </summary>
    [AddComponentMenu("Grow/Samples/Grow Editor Demo/03 Group Layout")]
    public sealed class GroupLayoutDemo : AnnotationDemoBehaviour {
        public override string DemoSummary =>
            "Group layout: [GrowGroup] supports Foldout (default) and Box; '/' in the path expresses nesting; members inside a group are ordered by GrowPropertyOrder, and unannotated members fall back to declaration order.";

        [GrowPropertyOrder(-10)]
        [GrowLabel("Pinned field (PropertyOrder -10)")]
        [Tooltip("Lower PropertyOrder values come first")]
        public int Pinned = 1;

        [GrowGroup("Basic Attributes")]
        [GrowPropertyOrder(0)]
        [GrowLabel("Attack")]
        public int Attack = 10;

        [GrowGroup("Basic Attributes")]
        [GrowPropertyOrder(1)]
        [GrowLabel("Defense")]
        public int Defense = 5;

        [GrowGroup("Advanced Attributes", GrowGroupKind.Box)]
        [GrowLabel("Critical chance")]
        public float CritChance = 0.15f;

        [GrowGroup("Advanced Attributes", GrowGroupKind.Box)]
        [GrowLabel("Can dodge")]
        public bool CanDodge = true;

        [GrowGroup("Nesting Demo")]
        [GrowLabel("Enable skin (first-level Foldout)")]
        public bool UseSkin = true;

        [GrowGroup("Nesting Demo/Appearance", GrowGroupKind.Box)]
        [GrowLabel("Model scale (second-level Box)")]
        public float ModelScale = 1f;

        [GrowGroup("Nesting Demo/Appearance", GrowGroupKind.Box)]
        [GrowLabel("Model name")]
        public string ModelName = "Hero";
    }
}
