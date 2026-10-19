// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Grow.Editor.Common.MultiSelect;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Grow.Editor.Inspector.EnumFlags {
    /// <summary>
    /// A bit-flag backend over a "raw enum int mask": used where no SerializedProperty exists (such as
    /// [GrowButton] method parameters).
    /// <para>Apply is a no-op (the caller owns the value); it shares <see cref="IMultiSelectBackend"/> with
    /// <see cref="EnumFlagsBackend"/>, so the presentation is identical.</para>
    /// </summary>
    internal sealed class EnumValueBackend : IMultiSelectBackend {
        private int _mask;

        /// <summary>Builds the mask from an initial enum value.</summary>
        /// <param name="value">The initial enum value.</param>
        public EnumValueBackend(Enum value) {
            _mask = ToMask(value);
        }

        private static int ToMask(Enum value) {
            try {
                return unchecked((int)Convert.ToInt64(value));
            } catch (OverflowException) {
                return unchecked((int)Convert.ToUInt64(value));
            }
        }

        public bool IsMixed => false;

        public bool IsMixedAt(int value) {
            return false;
        }

        public bool Contains(int value) {
            return (_mask & value) != 0;
        }

        public void SetValue(int value, bool on) {
            if (on) {
                _mask |= value;
            } else {
                _mask &= ~value;
            }
        }

        public void Clear() {
            _mask = 0;
        }

        public void SelectAll(int[] allValues) {
            _mask = 0;
            foreach (var v in allValues) {
                _mask |= v;
            }
        }

        public void Apply() {
        }

        /// <summary>The current bit mask (callers read it between repaints to get the latest selection).</summary>
        public int Mask => _mask;
    }

    /// <summary>
    /// Bit-flag storage backend: maps the reads and writes of <see cref="IMultiSelectBackend"/> onto bit arithmetic
    /// over the enum field's integer value (intValue).
    /// </summary>
    internal class EnumFlagsBackend : IMultiSelectBackend, IDisposable {
        private readonly SerializedProperty _property;
        private readonly Dictionary<Object, SerializedObject> _targetCache = new();

        /// <summary>Binds to an enum field property.</summary>
        /// <param name="property">The SerializedProperty of the enum type.</param>
        public EnumFlagsBackend(SerializedProperty property) {
            _property = property;
        }

        private long Value => _property.intValue;

        private SerializedObject GetTarget(Object target) {
            if (!_targetCache.TryGetValue(target, out var serialized)) {
                serialized = new SerializedObject(target);
                _targetCache[target] = serialized;
            }

            return serialized;
        }

        private void RefreshTargets() {
            foreach (var serialized in _targetCache.Values) {
                serialized.Update();
            }
        }

        /// <summary>Releases the per-target SerializedObject cache.</summary>
        public void Dispose() {
            foreach (var serialized in _targetCache.Values) {
                serialized.Dispose();
            }

            _targetCache.Clear();
        }

        /// <summary>Multi-target editing with differing values (always false for a single target).</summary>
        public bool IsMixed => _property.hasMultipleDifferentValues;

        /// <summary>Whether a given bit differs across targets.</summary>
        public bool IsMixedAt(int value) {
            if (!IsMixed) {
                return false;
            }

            var any = false;
            var all = true;
            var path = _property.propertyPath;
            foreach (var target in _property.serializedObject.targetObjects) {
                if (target == null) {
                    continue;
                }

                var serialized = GetTarget(target);
                var perTarget = serialized.FindProperty(path);
                if (perTarget == null) {
                    continue;
                }

                var has = (perTarget.intValue & value) != 0;
                any |= has;
                all &= has;
            }

            return any && !all;
        }

        public bool Contains(int value) {
            if (!IsMixed) {
                return (Value & (long)value) != 0;
            }

            var path = _property.propertyPath;
            foreach (var target in _property.serializedObject.targetObjects) {
                if (target == null) {
                    return false;
                }

                var serialized = GetTarget(target);
                var perTarget = serialized.FindProperty(path);
                if (perTarget == null || (perTarget.intValue & value) == 0) {
                    return false;
                }
            }

            return true;
        }

        public void SetValue(int value, bool on) {
            if (!_property.hasMultipleDifferentValues) {
                if (on) {
                    _property.intValue |= value;
                } else {
                    _property.intValue &= ~value;
                }

                return;
            }

            var path = _property.propertyPath;
            var targets = _property.serializedObject.targetObjects;
            Undo.RecordObjects(targets, "Toggle Enum Flags");
            foreach (var target in targets) {
                if (target == null) {
                    continue;
                }

                var serialized = GetTarget(target);
                var perTarget = serialized.FindProperty(path);
                if (perTarget == null) {
                    continue;
                }

                if (on) {
                    perTarget.intValue |= value;
                } else {
                    perTarget.intValue &= ~value;
                }

                serialized.ApplyModifiedProperties();
                serialized.Update();
            }

            _property.serializedObject.Update();
        }

        public void Clear() {
            _property.intValue = 0;
        }

        public void SelectAll(int[] allValues) {
            long mask = 0;
            foreach (var v in allValues) {
                mask |= (long)v;
            }

            _property.intValue = (int)mask;
        }

        public void Apply() {
            _property.serializedObject.ApplyModifiedProperties();
            RefreshTargets();
        }
    }
}
