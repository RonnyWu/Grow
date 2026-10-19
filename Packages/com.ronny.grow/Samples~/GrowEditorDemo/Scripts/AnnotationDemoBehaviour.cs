// SPDX-License-Identifier: MIT

using UnityEngine;

namespace Grow.Samples.GrowEditorDemo {
    /// <summary>
    /// Abstract base class for the annotation Inspector demo components: it only exists so the driving Editor can
    /// cover every derived demo component at once through
    /// <c>[CustomEditor(typeof(AnnotationDemoBehaviour), true)]</c>, sparing each themed component its own Editor.
    /// The base declares no fields and cannot be attached directly.
    /// </summary>
    public abstract class AnnotationDemoBehaviour : MonoBehaviour {
        /// <summary>A one-line description of this demo topic (shown as a HelpBox at the bottom of
        /// the panel by the driving Editor).</summary>
        public virtual string DemoSummary => string.Empty;
    }
}
