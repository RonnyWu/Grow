// SPDX-License-Identifier: MIT

using Grow.Core.Attributes;
using UnityEngine;

namespace Grow.Samples.GrowEditorDemo {
    /// <summary>A [Flags] enum for the demo: '/' inside a member's [GrowLabel] groups automatically
    /// in the multi-select tree.</summary>
    [System.Flags]
    public enum BattleFlags {
        [GrowLabel("Basic/None")] None = 0,
        [GrowLabel("Gameplay/Normal")] Normal = 1,
        [GrowLabel("Gameplay/Online")] Online = 1 << 1,
        [GrowLabel("Debug/Log")] VerboseLog = 1 << 2,
        [GrowLabel("All")] All = Normal | Online | VerboseLog,
    }

    /// <summary>A plain (non-[Flags]) enum for the demo: keeps the native single-choice dropdown.</summary>
    public enum ServerRegion {
        [GrowLabel("Asia")] Asia,
        [GrowLabel("Europe")] Europe,
        [GrowLabel("North America")] America,
    }

    /// <summary>
    /// Demo 2: enum drawing.
    /// <para><c>[GrowEnumFlags]</c> draws a <c>[Flags]</c> enum as a bit-flag hierarchical multi-select tree; a
    /// <c>[Flags]</c> enum without the annotation keeps Unity's native drawing (the native EnumFlagsField); a plain
    /// enum keeps the native single-choice Popup.</para>
    /// </summary>
    [AddComponentMenu("Grow/Samples/Grow Editor Demo/02 Enum Flags")]
    public sealed class EnumFlagsDemo : AnnotationDemoBehaviour {
        public override string DemoSummary =>
            "Enum multi-select: [GrowEnumFlags] replaces the single-choice dropdown with a bit-flag multi-select tree, and '/' in a member's [GrowLabel] groups automatically; an unannotated [Flags] enum keeps Unity's native drawing and a plain enum stays single-choice.";

        [GrowLabel("Feature bits (explicit [GrowEnumFlags])")]
        [GrowEnumFlags]
        [Tooltip("An explicit [GrowEnumFlags] can go further and use showCombos to control the combination alias rows")]
        public BattleFlags Features = BattleFlags.Normal | BattleFlags.Online;

        [GrowLabel("Feature bits (unannotated, Unity native drawing)")]
        public BattleFlags FeaturesByDefault = BattleFlags.VerboseLog;

        [GrowLabel("Server region (plain enum, single choice)")]
        public ServerRegion Region = ServerRegion.Asia;
    }
}
