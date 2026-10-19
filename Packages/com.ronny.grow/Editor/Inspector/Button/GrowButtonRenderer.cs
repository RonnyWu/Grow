// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Reflection;
using Grow.Editor.Inspector.Data;
using Grow.Editor.Inspector.EnumFlags;
using Grow.Editor.Inspector.Gui;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Grow.Editor.Inspector.Button {
    /// <summary>
    /// Draws and invokes [GrowButton] method cards (one per Editor instance).
    /// <para>It owns instance-level parameter buffers and foldout state (no cross-instance static dictionaries) and
    /// is responsible for the card header (foldout title + Invoke button), per-parameter inputs when expanded,
    /// parameter persistence (EditorPrefs) and per-target reflection invocation.</para>
    /// <para>After an invocation the "refresh / mark dirty / repaint" work is handled by the owner through the
    /// <c>onInvoked</c> callback, which decouples this class from the Editor lifecycle.</para>
    /// </summary>
    internal sealed class GrowButtonRenderer {
        private const string ButtonPrefsPrefix = "Grow.AttributeInspector.Button";

        private readonly Func<string> _typeFullNameProvider;
        private readonly Func<Object[]> _targetsProvider;
        private readonly Action _onInvoked;

        /// <summary>The foldout state of the [GrowButton] parameter area (key = "type|method signature"; collapsed by default).</summary>
        private readonly Dictionary<string, bool> _foldStates = new();

        /// <summary>The [GrowButton] parameter buffer: key = "type|method signature|parameter index" (the signature is part of the key to separate overloads), value = the current input value.</summary>
        private readonly Dictionary<string, object> _paramCache = new();

        /// <summary>Constructs the renderer.</summary>
        /// <param name="typeFullNameProvider">Provides the target object's type full name (used in
        /// parameter cache keys / signatures).</param>
        /// <param name="targetsProvider">Provides the currently edited targets (used for per-target
        /// invocation).</param>
        /// <param name="onInvoked">Callback after a button invocation (mark dirty / flag the change /
        /// repaint).</param>
        public GrowButtonRenderer(Func<string> typeFullNameProvider, Func<Object[]> targetsProvider, Action onInvoked) {
            _typeFullNameProvider = typeFullNameProvider;
            _targetsProvider = targetsProvider;
            _onInvoked = onInvoked;
        }

        /// <summary>
        /// Draws a single [GrowButton] method card: a one-line header (display-label title plus a right-aligned fixed
        /// "Invoke" button).
        /// <para>Methods with parameters collapse to a single line by default (click the title area
        /// to expand and edit
        /// them); the invoke button works whether collapsed or expanded, and parameter values come from the
        /// persisted buffer (the type default the first time).</para>
        /// </summary>
        /// <param name="button">The button method model.</param>
        public void Draw(ButtonModel button) {
            var typeName = _typeFullNameProvider();
            var signature = ButtonGui.BuildSignature(button.Method);
            var keyPrefix = ButtonPrefsPrefix + "|" + typeName + "|" + signature + "|";
            var hasParams = button.Parameters.Length > 0;
            var foldKey = typeName + "|" + signature;

            var fold = hasParams && _foldStates.TryGetValue(foldKey, out var saved) && saved;

            LayoutGui.DrawCard(ButtonGui.CardStyle,
                () => {
                    fold = DrawHeader(button, keyPrefix, fold);
                    if (hasParams) {
                        _foldStates[foldKey] = fold;
                    }
                },
                () => {
                    if (fold) {
                        DrawParams(button, keyPrefix);
                    }
                }
            );
        }

        /// <summary>
        /// The one-line card header: on the left the foldout title (the [GrowButton.Label] display label, falling
        /// back to the method name; the arrow is rendered only for methods with parameters, and clicking the text
        /// also expands), on the right the fixed-width "Invoke" button; title and button share the row height and are
        /// vertically centered. Returns the latest foldout state.
        /// </summary>
        private bool DrawHeader(ButtonModel button, string keyPrefix, bool fold) {
            var rowRect = EditorGUILayout.GetControlRect(false, ButtonGui.RowHeight);
            var signature = ButtonGui.BuildSignature(button.Method);
            var title = string.IsNullOrEmpty(button.Label) ? button.Method.Name : button.Label;

            var buttonRect = new Rect(rowRect.xMax - ButtonGui.InvokeWidth, rowRect.y, ButtonGui.InvokeWidth, rowRect.height);
            var titleRect = new Rect(rowRect.x, rowRect.y, buttonRect.x - rowRect.x - 4f, rowRect.height);

            if (button.Parameters.Length > 0) {
                fold = EditorGUI.Foldout(titleRect, fold, title, true);
            } else {
                EditorGUI.LabelField(titleRect, title);
            }

            if (GUI.Button(buttonRect, new GUIContent(ButtonGui.InvokeLabel, signature))) {
                Invoke(button, keyPrefix);
            }

            return fold;
        }

        /// <summary>Card content: when expanded, draws one input control per parameter (with a
        /// type-prefixed label); label width and indentation come from the default drawing behavior
        /// so the controls line up with regular Inspector fields.</summary>
        private void DrawParams(ButtonModel button, string keyPrefix) {
            if (button.Parameters.Length == 0) {
                return;
            }

            for (var i = 0; i < button.Parameters.Length; i++) {
                var parameter = button.Parameters[i];
                var key = keyPrefix + i;
                var value = GetOrCreateParam(key, parameter.ParameterType);
                _paramCache[key] = DrawParamField(ButtonGui.BuildParamLabel(parameter), parameter.ParameterType, value);
            }
        }

        /// <summary>Assembles the arguments and invokes per target; fields are changed directly
        /// rather than through serialization, and <see cref="_onInvoked"/> then marks everything
        /// dirty and refreshes uniformly so the save pipeline captures the change and it shows
        /// immediately.</summary>
        private void Invoke(ButtonModel button, string keyPrefix) {
            var args = new object[button.Parameters.Length];
            for (var i = 0; i < button.Parameters.Length; i++) {
                var key = keyPrefix + i;
                var raw = _paramCache.TryGetValue(key, out var cached) ? cached : GetOrCreateParam(key, button.Parameters[i].ParameterType);
                args[i] = raw is EnumValueBackend backend ? Enum.ToObject(button.Parameters[i].ParameterType, backend.Mask) : raw;
                EditorPrefs.SetString(key, GrowButtonParamCodec.ToParamString(button.Parameters[i].ParameterType, args[i]));
            }

            foreach (var target in _targetsProvider()) {
                if (target == null) {
                    continue;
                }

                Undo.RecordObject(target, button.Method.Name);
                try {
                    button.Method.Invoke(target, args);
                } catch (TargetInvocationException ex) {
                    Debug.LogException(ex.InnerException ?? ex);
                } catch (Exception ex) {
                    Debug.LogException(ex);
                }
            }

            _onInvoked();
        }

        /// <summary>Reads or initializes the parameter cache: the EditorPrefs value takes
        /// precedence, otherwise the type default.
        /// A [Flags] enum parameter is cached as an <see cref="EnumValueBackend"/> so edits made in
        /// the popup multi-select survive repaints.</summary>
        private object GetOrCreateParam(string key, Type type) {
            if (_paramCache.TryGetValue(key, out var cached)) {
                return cached;
            }

            var fallback = GrowButtonParamCodec.GetDefaultValue(type);
            if (EditorPrefs.HasKey(key)) {
                if (GrowButtonParamCodec.TryParseParam(type, EditorPrefs.GetString(key), out var parsed)) {
                    fallback = parsed;
                }
            }

            fallback = GrowButtonParamCodec.NormalizeParam(type, fallback);
            _paramCache[key] = fallback;
            return fallback;
        }

        /// <summary>Draws the input control for a single [GrowButton] parameter and returns the
        /// latest value.</summary>
        /// <param name="label">The control label (type short name + parameter name).</param>
        /// <param name="type">The parameter type (int/long/float/double/bool/string/enum are supported).</param>
        /// <param name="value">The current value.</param>
        /// <returns>The latest value after drawing (returned unchanged for unmatched types).</returns>
        private static object DrawParamField(string label, Type type, object value) {
            if (type == typeof(int)) {
                return EditorGUILayout.IntField(label, value is int i ? i : 0);
            }

            if (type == typeof(long)) {
                return EditorGUILayout.LongField(label, value is long l ? l : 0L);
            }

            if (type == typeof(float)) {
                return EditorGUILayout.FloatField(label, value is float f ? f : 0f);
            }

            if (type == typeof(double)) {
                return EditorGUILayout.DoubleField(label, value is double d ? d : 0d);
            }

            if (type == typeof(bool)) {
                return EditorGUILayout.Toggle(label, value is bool b && b);
            }

            if (type == typeof(string)) {
                return EditorGUILayout.TextField(label, value as string ?? string.Empty);
            }

            if (type.IsEnum) {
                // A [Flags] enum parameter uses the same hierarchical multi-select as a field (shared EnumFlagsGui
                // cache plus the HierarchicalMultiSelectGui presentation layer), reusing the same EnumValueBackend
                // instance so edits in the popup survive repaints; a plain enum keeps the native single-choice Popup.
                if (type.IsDefined(typeof(FlagsAttribute), false)) {
                    var backend = value as EnumValueBackend ??
                        new EnumValueBackend((Enum)(value ?? GrowButtonParamCodec.GetDefaultValue(type)));
                    return EnumFlagsGui.DrawEnumFlagsValue(label, type, backend);
                }

                var current = value is Enum e ? e : (Enum)GrowButtonParamCodec.GetDefaultValue(type);
                return EditorGUILayout.EnumPopup(label, current);
            }

            return value;
        }
    }
}
