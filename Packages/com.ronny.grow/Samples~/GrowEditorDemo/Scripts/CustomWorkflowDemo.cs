// SPDX-License-Identifier: MIT

using UnityEngine;

namespace Grow.Samples.GrowEditorDemo {
    /// <summary>
    /// Demo 7: fully custom drawing plus workflow (deriving directly from <c>GrowEditorBase</c> without the
    /// annotation model).
    /// <para>The companion <c>CustomWorkflowDemoEditor</c> demonstrates ForceWideMode, the OnSetup/OnCalc/OnChanged
    /// lifecycle and a manual <c>SaveAll()</c>.</para>
    /// </summary>
    [AddComponentMenu("Grow/Samples/Grow Editor Demo/07 Custom Workflow")]
    public sealed class CustomWorkflowDemo : MonoBehaviour {
        [Tooltip("Preview radius")]
        [Range(0f, 10f)]
        public float Radius = 2f;

        [Tooltip("Preview segment count")]
        [Range(3, 64)]
        public int Segments = 16;

        [Tooltip("Whether a manual SaveAll() also saves the containing scene (off by default to avoid saving scenes by accident)")]
        public bool SaveScene;

        [SerializeField]
        private string lastEvent = "Nothing yet";

        /// <summary>The latest event text written by the driving Editor (serialized so other views
        /// can inspect it).</summary>
        public string LastEvent {
            get => lastEvent;
            set => lastEvent = value;
        }
    }
}
