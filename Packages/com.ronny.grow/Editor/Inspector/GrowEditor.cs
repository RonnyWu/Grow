// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Reflection;
using Grow.Core.Attributes;
using Grow.Editor.Inspector.Button;
using Grow.Editor.Inspector.Condition;
using Grow.Editor.Inspector.Data;
using Grow.Editor.Inspector.Gui;
using Grow.Editor.Inspector.Workflow;
using UnityEditor;
using UnityEngine;

namespace Grow.Editor.Inspector {
    /// <summary>
    /// The annotation-driven Grow editor base class: on top of the workflow base <see cref="GrowEditorBase"/> it
    /// automatically draws the root-level fields and [GrowButton] methods according to the type model.
    /// <para>Supported annotations: GrowPropertyOrder / GrowGroup / GrowLabel / GrowShowIf / GrowHideIf /
    /// GrowEnableIf / GrowDisableIf / GrowOnValueChanged / GrowButton.
    /// Usage:
    /// <code>
    /// [CustomEditor(typeof(MyConfig))]
    /// public sealed class MyConfigEditor : GrowEditor { }
    /// </code>
    /// To inject hand-written content, override <see cref="OnDraw"/>: call base.OnDraw() to draw the automatic
    /// fields first, then append custom GUI; or use <see cref="DrawAnnotatedField(string)"/> to draw an annotated
    /// field on its own.</para>
    /// <para>For the drawing pipeline, change tracking and auto-save see <see cref="GrowEditorBase"/>; fully custom
    /// drawing can derive from the workflow base directly without the annotation model.</para>
    /// </summary>
    [CanEditMultipleObjects]
    public abstract class GrowEditor : GrowEditorBase {
        /// <summary>Foldout state of groups (view state, per instance; rebuilt with the instance on
        /// domain reload, so it resets automatically).</summary>
        private readonly Dictionary<string, bool> _foldoutStates = new();

        /// <summary>The annotated type model (lazy, cached per type).</summary>
        private InspectorTypeModel _model;

        /// <summary>The [GrowButton] renderer (lazy, per instance; owns the parameter buffers and
        /// foldout state).</summary>
        private GrowButtonRenderer _buttonRenderer;

        /// <summary>The annotated type model (built lazily).</summary>
        private InspectorTypeModel Model => _model ??= InspectorTypeModel.GetFor(serializedObject);

        /// <summary>The [GrowButton] renderer (built lazily; the target type full name comes from
        /// the current target).</summary>
        private GrowButtonRenderer ButtonRenderer => _buttonRenderer ??= new GrowButtonRenderer(
            () => TargetObjectOrNull != null ? TargetObjectOrNull.GetType().FullName ?? string.Empty : string.Empty,
            () => targets,
            OnButtonInvoked);

        /// <summary>The main drawing entry: draws the annotation model by default (field/button
        /// tokens plus groups); subclasses may override and inject hand-written GUI.</summary>
        protected override void OnDraw() {
            DrawTokens(Model.Tokens);
        }

        /// <summary>Draws one annotated root field on its own (applying conditions / labels /
        /// callbacks) and reports whether it was actually drawn.</summary>
        /// <param name="propertyPath">The root property path.</param>
        /// <returns>true when the field exists and was actually drawn; false when the path is
        /// missing or the field is hidden by a condition.</returns>
        protected bool DrawAnnotatedField(string propertyPath) {
            var property = serializedObject.FindProperty(propertyPath);
            return property != null && DrawAnnotatedField(property);
        }

        /// <summary>Draws an annotated field and reports whether it was actually drawn.</summary>
        /// <param name="property">The target property.</param>
        /// <returns>true when the field was actually drawn (falls back to native drawing when no
        /// model can be resolved).</returns>
        protected bool DrawAnnotatedField(SerializedProperty property) {
            if (property == null) {
                return false;
            }

            var field = FindFieldModel(property.propertyPath);
            if (field == null) {
                EditorGUILayout.PropertyField(property, new GUIContent(SerializedPropertyUtil.GetLabel(property)), property.hasChildren);
                return true;
            }

            return DrawFieldCore(property, field);
        }

        /// <summary>Finds the matching <see cref="FieldModel"/> for a property path in the model's
        /// token tree.</summary>
        /// <param name="path">The property path.</param>
        /// <returns>The field model found, or null.</returns>
        private FieldModel FindFieldModel(string path) {
            foreach (var token in Model.Tokens) {
                var found = FindFieldInToken(token, path);
                if (found != null) {
                    return found;
                }
            }

            return null;
        }

        /// <summary>Finds a field model by path within a single token (and its nested groups).</summary>
        /// <param name="token">The token.</param>
        /// <param name="path">The property path.</param>
        /// <returns>The field model found, or null.</returns>
        private static FieldModel FindFieldInToken(LayoutToken token, string path) {
            switch (token) {
                case FieldToken ft when ft.Field.Path == path:
                    return ft.Field;
                case GroupToken gt:
                    foreach (var member in gt.Group.Members) {
                        var found = FindFieldInToken(member, path);
                        if (found != null) {
                            return found;
                        }
                    }

                    break;
            }

            return null;
        }

        /// <summary>Draws a token sequence (loose fields/buttons or groups; groups start expanded
        /// and an empty group is hidden entirely).</summary>
        /// <param name="tokens">The top-level token sequence.</param>
        private void DrawTokens(IReadOnlyList<LayoutToken> tokens) {
            foreach (var token in tokens) {
                switch (token) {
                    case FieldToken fieldToken:
                        DrawTokenField(fieldToken);
                        break;
                    case ButtonToken buttonToken:
                        DrawButton(buttonToken.Button);
                        break;
                    case GroupToken groupToken:
                        DrawTokenGroup(groupToken);
                        break;
                }
            }
        }

        /// <summary>Draws a single field token (looks up the property by path, then runs the core drawing).</summary>
        /// <param name="token">The field token.</param>
        private void DrawTokenField(FieldToken token) {
            var property = serializedObject.FindProperty(token.Field.Path);
            if (property == null) {
                return;
            }

            DrawFieldCore(property, token.Field);
        }

        /// <summary>Core field drawing: visibility → disable → change callback.</summary>
        /// <param name="property">The target property.</param>
        /// <param name="field">The field model.</param>
        /// <returns>Whether the field was actually drawn (false when hidden by a condition).</returns>
        private bool DrawFieldCore(SerializedProperty property, FieldModel field) {
            if (!ShouldShow(field, out var showMixed)) {
                return false;
            }

            var disabled = showMixed || !ShouldEnable(field);

            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledScope(disabled)) {
                EditorGUILayout.PropertyField(property, field.Content, property.hasChildren);
            }

            if (EditorGUI.EndChangeCheck()) {
                serializedObject.ApplyModifiedProperties();
                InvokeOnChanged(field);
                Repaint();
            }

            return true;
        }

        /// <summary>Draws a group token: Box renders as a card, anything else as a foldout (nested
        /// groups recurse).</summary>
        /// <param name="token">The group token.</param>
        private void DrawTokenGroup(GroupToken token) {
            if (!GroupHasVisibleContent(token.Group)) {
                return;
            }

            var targetObject = TargetObjectOrNull;
            if (targetObject == null) {
                return;
            }

            var key = $"{targetObject.GetType().Name}|{token.Group.Title}";
            var open = _foldoutStates.TryGetValue(key, out var current) ? current : true;

            void DrawMembers() {
                foreach (var member in token.Group.Members) {
                    switch (member) {
                        case FieldToken fieldToken:
                            DrawTokenField(fieldToken);
                            break;
                        case ButtonToken buttonToken:
                            DrawButton(buttonToken.Button);
                            break;
                        case GroupToken nestedGroup:
                            DrawTokenGroup(nestedGroup);
                            break;
                    }
                }
            }

            switch (token.Group.Kind) {
                case GrowGroupKind.Box:
                    LayoutGui.DrawCard(token.Group.Segment, DrawMembers);
                    break;
                default:
                    open = LayoutGui.DrawFoldout(open, token.Group.Segment, DrawMembers);
                    _foldoutStates[key] = open;
                    break;
            }
        }

        /// <summary>The whole group is drawn only when at least one member is currently visible
        /// (after multi-target aggregation) or any child group has visible content; buttons are
        /// always visible.</summary>
        /// <param name="group">The group model.</param>
        /// <returns>Whether the group has visible content.</returns>
        private bool GroupHasVisibleContent(GroupModel group) {
            foreach (var member in group.Members) {
                switch (member) {
                    case FieldToken fieldToken:
                        if (ShouldShow(fieldToken.Field, out _)) {
                            return true;
                        }

                        break;
                    case ButtonToken:
                        return true;
                    case GroupToken nestedGroup:
                        if (GroupHasVisibleContent(nestedGroup.Group)) {
                            return true;
                        }

                        break;
                }
            }

            return false;
        }

        /// <summary>Draws a single [GrowButton] method card (delegated to the instance-level renderer).</summary>
        /// <param name="button">The button method model.</param>
        private void DrawButton(ButtonModel button) {
            ButtonRenderer.Draw(button);
        }

        /// <summary>Cleanup after a [GrowButton] invocation: refresh serialization, mark every
        /// target dirty, flag the change and repaint.</summary>
        private void OnButtonInvoked() {
            serializedObject.Update();
            foreach (var target in targets) {
                if (target != null) {
                    EditorUtility.SetDirty(target);
                }
            }

            MarkChanged();
            Repaint();
        }

        /// <summary>Multi-target aggregation of visibility (ShowIf/HideIf); see
        /// <see cref="FieldConditionEvaluator.ShouldShow"/>.</summary>
        private bool ShouldShow(FieldModel field, out bool mixed) {
            return FieldConditionEvaluator.ShouldShow(field, targets, out mixed);
        }

        /// <summary>Multi-target aggregation of editability (EnableIf/DisableIf); see
        /// <see cref="FieldConditionEvaluator.ShouldEnable"/>.</summary>
        private bool ShouldEnable(FieldModel field) {
            return FieldConditionEvaluator.ShouldEnable(field, targets);
        }

        /// <summary>Invokes the parameterless method named by [GrowOnValueChanged] after a field
        /// value changes (per target, exceptions logged to the Console).</summary>
        /// <param name="field">The field model.</param>
        private void InvokeOnChanged(FieldModel field) {
            if (string.IsNullOrEmpty(field.OnChangedMethod)) {
                return;
            }

            foreach (var target in targets) {
                if (target == null) {
                    continue;
                }

                var method = target.GetType().GetMethod(field.OnChangedMethod,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                if (method == null) {
                    continue;
                }

                try {
                    Undo.RecordObject(target, $"OnValueChanged {field.OnChangedMethod}");
                    method.Invoke(target, null);
                } catch (TargetInvocationException ex) {
                    Debug.LogException(ex.InnerException ?? ex);
                } catch (Exception ex) {
                    Debug.LogException(ex);
                }
            }

            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
            foreach (var target in targets) {
                if (target != null) {
                    EditorUtility.SetDirty(target);
                }
            }
        }
    }
}
