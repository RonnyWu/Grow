// SPDX-License-Identifier: MIT

using Grow.Editor.Inspector.Workflow;
using UnityEditor;
using UnityEngine;

namespace Grow.Samples.GrowEditorDemo.Editor {
    /// <summary>
    /// A <c>GrowEditorBase</c> custom-workflow demo: no annotation model, a hand-written <see cref="OnDraw"/>.
    /// <para>Demonstrates ForceWideMode, the OnCalc/OnChanged lifecycle and a manual <c>SaveAll()</c>.</para>
    /// </summary>
    [CustomEditor(typeof(CustomWorkflowDemo))]
    [CanEditMultipleObjects]
    public sealed class CustomWorkflowDemoEditor : GrowEditorBase {
        private int _layoutTicks;
        private int _changeCount;

        private CustomWorkflowDemo Target => target as CustomWorkflowDemo;

        /// <summary>Custom panels use wide mode by default (label and field on the same line).</summary>
        protected override bool ForceWideMode => true;

        /// <summary>Whether a manual SaveAll() also saves the scene (reads the switch on the component).</summary>
        protected override bool SaveScene => Target != null && Target.SaveScene;

        /// <summary>Accumulated per Layout call, used to show that OnCalc runs.</summary>
        protected override void OnCalc() {
            _layoutTicks++;
        }

        /// <summary>Records an event once an actual change happened this frame.</summary>
        protected override void OnChanged() {
            _changeCount++;
            var demo = Target;
            if (demo != null) {
                demo.LastEvent = $"Change #{_changeCount} (frame {Time.frameCount})";
            }
        }

        protected override void OnDraw() {
            EditorGUILayout.LabelField("GrowEditorBase custom drawing", EditorStyles.boldLabel);

            var radius = serializedObject.FindProperty(nameof(CustomWorkflowDemo.Radius));
            var segments = serializedObject.FindProperty(nameof(CustomWorkflowDemo.Segments));
            var saveScene = serializedObject.FindProperty(nameof(CustomWorkflowDemo.SaveScene));
            var demo = Target;

            EditorGUILayout.PropertyField(radius);
            EditorGUILayout.PropertyField(segments);
            EditorGUILayout.PropertyField(saveScene);

            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                $"Preview: radius = {(demo != null ? demo.Radius : 0f):F2}, segments = {(demo != null ? demo.Segments : 0)}\n" +
                $"Layout frames = {_layoutTicks}, changes this session = {_changeCount}\n" +
                $"Last event: {(demo != null ? demo.LastEvent : "-")}",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Reset to defaults")) {
                    radius.floatValue = 2f;
                    segments.intValue = 16;
                }

                if (GUILayout.Button("Save now (SaveAll)")) {
                    SaveAll();
                }
            }
        }
    }
}
