// SPDX-License-Identifier: MIT

using Grow.Editor.Inspector;
using UnityEditor;

namespace Grow.Samples.GrowEditorDemo.Editor {
    /// <summary>
    /// One driving Editor covering every annotation demo component: <c>[CustomEditor(typeof(AnnotationDemoBehaviour), true)]</c>
    /// makes all derived types share this editor, so no per-topic Editor is needed.
    /// <para>The panel content is drawn entirely by the annotation model; only the bottom note
    /// mixing hand-written and
    /// annotated content is appended here.</para>
    /// </summary>
    [CustomEditor(typeof(AnnotationDemoBehaviour), true)]
    [CanEditMultipleObjects]
    public sealed class AnnotationDemoEditor : GrowEditor {
        protected override void OnDraw() {
            base.OnDraw();

            if (target is not AnnotationDemoBehaviour demo) {
                return;
            }

            if (string.IsNullOrEmpty(demo.DemoSummary)) {
                return;
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(demo.DemoSummary, MessageType.Info);
        }
    }
}
