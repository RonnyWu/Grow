// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Grow.Editor.Common.Enumeration;
using UnityEngine;

namespace Grow.Editor.Common.MultiSelect {
    /// <summary>How enum values are interpreted into multi-select display data.</summary>
    internal enum EnumMultiSelectMode {
        /// <summary>Flags: the enum value is the bit itself (1, 2, 4 ...); only single-bit members
        /// are selectable and combination alias rows are supported.</summary>
        Flags,

        /// <summary>Bits: the enum value is a bit index (0, 1, 2 ...), bounded by the mask capacity.</summary>
        Bits,

        /// <summary>Value: the enum value is a sparse business value with no bit semantics.</summary>
        Value
    }

    /// <summary>
    /// Unified builder from an enum to <see cref="MultiSelectData"/> (Flags / Bits / Value semantics).
    /// <para>Consolidates the duplicated construction logic previously kept separately in FixedBitMaskDrawer /
    /// EnumFlagsCache / ListSpMultiSelectPainter: reflection parsing, the label format
    /// ("{value} : {Name} : {Label}"), [Obsolete] detection and Nothing / Everything semantics all live here.</para>
    /// <para>Results are cached by (enum type, mode, capacity, include combos); only named factories are public so
    /// invalid combinations such as passing includeCombos to Bits are impossible.</para>
    /// </summary>
    internal static class EnumMultiSelectDataBuilder {
        private const string Nothing = "Nothing";
        private const string Everything = "Everything";

        /// <summary>Cache key: differing in any dimension means a different filtering /
        /// presentation result.</summary>
        private readonly struct MemoKey : IEquatable<MemoKey> {
            private readonly Type _enumType;
            private readonly EnumMultiSelectMode _mode;
            private readonly int _capacity;
            private readonly bool _includeCombos;

            public MemoKey(Type enumType, EnumMultiSelectMode mode, int capacity, bool includeCombos) {
                _enumType = enumType;
                _mode = mode;
                _capacity = capacity;
                _includeCombos = includeCombos;
            }

            public bool Equals(MemoKey other) {
                return _enumType == other._enumType && _mode == other._mode && _capacity == other._capacity && _includeCombos == other._includeCombos;
            }

            public override bool Equals(object obj) {
                return obj is MemoKey other && Equals(other);
            }

            public override int GetHashCode() {
                unchecked {
                    var hash = _enumType != null ? _enumType.GetHashCode() : 0;
                    hash = (hash * 397) ^ (int)_mode;
                    hash = (hash * 397) ^ _capacity;
                    hash = (hash * 397) ^ (_includeCombos ? 1 : 0);
                    return hash;
                }
            }
        }

        private static readonly Dictionary<MemoKey, MultiSelectData> Cache = new();

        /// <summary>Builds the display data for a [Flags] enum (only single-bit members are
        /// selectable; combination alias rows are optional).</summary>
        /// <param name="enumType">The enum type.</param>
        /// <param name="includeCombos">Whether to include combination alias rows (such as AB = A|B).</param>
        /// <returns>The flags display data.</returns>
        public static MultiSelectData BuildFlags(Type enumType, bool includeCombos = true) {
            return GetOrAdd(new MemoKey(enumType, EnumMultiSelectMode.Flags, 0, includeCombos), () => BuildFlagsCore(enumType, includeCombos));
        }

        /// <summary>Builds the display data for a bit-index (Bits) enum: only members with 0 ≤
        /// value &lt; capacity are kept.</summary>
        /// <param name="enumType">The enum type.</param>
        /// <param name="capacity">Mask capacity, i.e. the exclusive upper bound of valid bit
        /// indices (32 / 64 / 128 / 256).</param>
        /// <returns>The bit-index display data.</returns>
        public static MultiSelectData BuildBits(Type enumType, int capacity) {
            return GetOrAdd(new MemoKey(enumType, EnumMultiSelectMode.Bits, capacity, false), () => BuildBitsCore(enumType, capacity));
        }

        /// <summary>Builds the display data for a plain-value (Value) enum: explicit Nothing /
        /// Everything semantic members are excluded.</summary>
        /// <param name="enumType">The enum type.</param>
        /// <returns>The plain-value display data.</returns>
        public static MultiSelectData BuildValues(Type enumType) {
            return GetOrAdd(new MemoKey(enumType, EnumMultiSelectMode.Value, 0, false), () => BuildValuesCore(enumType));
        }

        private static MultiSelectData GetOrAdd(MemoKey key, Func<MultiSelectData> build) {
            if (Cache.TryGetValue(key, out var cached)) {
                return cached;
            }

            var data = build();
            Cache[key] = data;
            return data;
        }

        /// <summary>Unified option display text format: "{value} : {Name} : {Label}" (the Label may
        /// use "/" to nest levels).</summary>
        private static string FormatLabel(int value, EnumInfo info) {
            return $"{value} : {info.EnumName} : {info.EnumLabel}";
        }

        private static MultiSelectData BuildFlagsCore(Type enumType, bool includeCombos) {
            if (!enumType.IsDefined(typeof(FlagsAttribute), false)) {
                Debug.LogWarning(
                    $"[GrowEnumFlags] Enum '{enumType}' is not marked [Flags]; the flags UI may not behave as intended.");
            }

            var infos = EnumTypeUtility.GetEnumInfos(enumType);
            var (nothingLabel, nothingInfo, everythingLabel, everythingInfo, everythingValue, _) =
                EnumSpecialLabelUtil.ResolveSpecialLabels(enumType, Nothing, Everything, EnumSpecialLabelUtil.BitMaskValueMode.Flags);

            // Only single-bit members are selectable (zero / negative / composite values are filtered out); input is
            // sorted by value, and for equal values the first occurrence wins.
            var values = new List<int>();
            var labels = new List<string>();
            var obsoletes = new List<bool>();
            var added = new HashSet<long>();

            foreach (var info in infos) {
                var flag = (long)info.EnumValue;
                if (flag <= 0) {
                    continue;
                }

                if ((flag & (flag - 1)) != 0) {
                    continue;
                }

                if (!added.Add(flag)) {
                    continue;
                }

                values.Add(info.EnumValue);
                labels.Add(FormatLabel(info.EnumValue, info));
                obsoletes.Add(info.IsObsolete);
            }

            // Defined member value → display name (combination aliases included; used for alias-hit matching in the
            // button label, first occurrence wins for equal values)
            var valueToName = new Dictionary<int, string>();
            foreach (var info in infos) {
                if (!valueToName.ContainsKey(info.EnumValue)) {
                    valueToName[info.EnumValue] = info.EnumName;
                }
            }

            // Combination alias rows: multi-bit defined members (AB = A|B) toggle as a whole; Everything semantic
            // members, combinations containing unpresented bits and duplicate values are excluded
            var comboValues = new List<int>();
            var comboLabels = new List<string>();
            if (includeCombos) {
                long leafMask = 0;
                foreach (var v in values) {
                    leafMask |= (long)v;
                }

                var comboSeen = new HashSet<long>();
                foreach (var info in infos) {
                    var flag = (long)info.EnumValue;
                    if (flag <= 0) {
                        continue;
                    }

                    if ((flag & (flag - 1)) == 0) {
                        continue;
                    }

                    if (flag == everythingValue) {
                        continue;
                    }

                    if ((flag & ~leafMask) != 0) {
                        continue;
                    }

                    if (!comboSeen.Add(flag)) {
                        continue;
                    }

                    comboValues.Add(info.EnumValue);
                    comboLabels.Add(FormatLabel(info.EnumValue, info));
                }
            }

            return new MultiSelectData {
                Values = values.ToArray(),
                Labels = labels.ToArray(),
                Obsoletes = obsoletes.ToArray(),
                NothingLabel = nothingLabel,
                EverythingLabel = everythingLabel,
                NothingInfo = nothingInfo,
                EverythingInfo = everythingInfo,
                ComboValues = comboValues.ToArray(),
                ComboLabels = comboLabels.ToArray(),
                ValueToNameMap = valueToName,
                SupportsAliasMatch = true
            };
        }

        private static MultiSelectData BuildBitsCore(Type enumType, int capacity) {
            var infos = EnumTypeUtility.GetEnumInfos(enumType);
            var (nothingLabel, nothingInfo, everythingLabel, everythingInfo, _, _) =
                EnumSpecialLabelUtil.ResolveSpecialLabels(enumType, Nothing, Everything, EnumSpecialLabelUtil.BitMaskValueMode.Bits);

            var values = new List<int>();
            var labels = new List<string>();
            var obsoletes = new List<bool>();

            foreach (var info in infos) {
                if (info.EnumValue < 0 || info.EnumValue >= capacity) {
                    continue; // The enum value is a bit index; bits outside the capacity are not presented
                }

                values.Add(info.EnumValue);
                labels.Add(FormatLabel(info.EnumValue, info));
                obsoletes.Add(info.IsObsolete);
            }

            return new MultiSelectData {
                Values = values.ToArray(),
                Labels = labels.ToArray(),
                Obsoletes = obsoletes.ToArray(),
                NothingLabel = nothingLabel,
                EverythingLabel = everythingLabel,
                NothingInfo = nothingInfo,
                EverythingInfo = everythingInfo
            };
        }

        private static MultiSelectData BuildValuesCore(Type enumType) {
            var infos = EnumTypeUtility.GetEnumInfos(enumType);
            var (nothingLabel, nothingInfo, everythingLabel, everythingInfo, everythingValue, nothingValue) =
                EnumSpecialLabelUtil.ResolveSpecialLabels(enumType, Nothing, Everything, EnumSpecialLabelUtil.BitMaskValueMode.Value);

            // Explicitly defined Nothing / Everything members must be excluded from the plain leaf list; otherwise
            // selecting them would be treated as an ordinary value and could never be presented as Nothing.
            var hasEverything = everythingInfo != null;
            var values = new List<int>();
            var labels = new List<string>();
            var obsoletes = new List<bool>();

            foreach (var info in infos) {
                if (info.EnumValue == nothingValue) {
                    continue;
                }

                if (hasEverything && info.EnumValue == (int)everythingValue) {
                    continue;
                }

                values.Add(info.EnumValue);
                labels.Add(FormatLabel(info.EnumValue, info));
                obsoletes.Add(info.IsObsolete);
            }

            return new MultiSelectData {
                Values = values.ToArray(),
                Labels = labels.ToArray(),
                Obsoletes = obsoletes.ToArray(),
                NothingLabel = nothingLabel,
                EverythingLabel = everythingLabel,
                NothingInfo = nothingInfo,
                EverythingInfo = everythingInfo
            };
        }
    }
}
