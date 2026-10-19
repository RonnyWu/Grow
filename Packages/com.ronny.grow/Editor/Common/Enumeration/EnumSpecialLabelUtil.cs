// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Linq;
using Grow.Editor.Common.Extensions;
using static System.Reflection.BindingFlags;

namespace Grow.Editor.Common.Enumeration {
    /// <summary>
    /// Resolves the special Nothing / Everything semantic members of an enum (shared by property drawing and
    /// the enum popup).
    /// <para>Members named none/nothing/all/everything, or whose value matches the semantic (0 / all bits / -1),
    /// are recognized as "Nothing / Everything" members; their display name and trailing detail are returned so a
    /// multi-select control can present them as fixed rows.</para>
    /// </summary>
    internal static class EnumSpecialLabelUtil {
        /// <summary>How enum values are interpreted as a bit space.</summary>
        internal enum BitMaskValueMode {
            /// <summary>Flags: each value is itself a bit, e.g. 1, 2, 4, 8 ...</summary>
            Flags,

            /// <summary>Bits: each value is a bit index, e.g. 0, 1, 2 ... 31 ...</summary>
            Bits,

            /// <summary>Neither Flags bits nor Bits indices — a plain business value.</summary>
            Value
        }

        /// <summary>Infers the bit semantics from whether [Flags] is present.</summary>
        /// <param name="enumType">The enum type.</param>
        /// <returns><see cref="BitMaskValueMode.Flags"/> when [Flags] is present, otherwise
        /// <see cref="BitMaskValueMode.Bits"/>.</returns>
        public static BitMaskValueMode GetBitMaskValueMode(Type enumType) {
            return enumType.IsDefined(typeof(FlagsAttribute), false) ? BitMaskValueMode.Flags : BitMaskValueMode.Bits;
        }

        /// <summary>
        /// Converts a raw enum value into the "flag space".
        /// <para>Flags mode returns it unchanged; Bits mode shifts one left by the bit index (out of range or
        /// negative yields 0); Value mode returns it unchanged (plain values have no bit semantics, and applying
        /// the shift would wrongly turn 0 into 1).</para>
        /// </summary>
        /// <param name="raw">The raw enum value.</param>
        /// <param name="mode">The bit semantics mode.</param>
        /// <returns>The converted flag value.</returns>
        public static long ToFlagValue(long raw, BitMaskValueMode mode) {
            return mode switch {
                BitMaskValueMode.Flags => raw,
                BitMaskValueMode.Bits => raw is >= 0 and < 64 ? 1L << (int)raw : 0L,
                BitMaskValueMode.Value => raw,
                _ => 0L
            };
        }

        /// <summary>
        /// Resolves whether the enum explicitly defines Nothing / Everything semantic members.
        /// <para>Flags / Bits modes: judged uniformly in "flag space", with name semantics (none/nothing,
        /// all/everything) taking precedence over flag matching (0 / the theoretical all-bits mask / -1);
        /// ordinary flag bits already defined as valid members are not mistaken for special rows.</para>
        /// <para>Value mode: enum values are sparse plain business values with no "all-bits mask" concept, so no
        /// bit arithmetic is applied — Nothing is recognized only by name semantics or by a raw value of 0, and
        /// Everything only by name semantics, avoiding a meaningless OR-ed "all bits" result.</para>
        /// A hit returns the member's [GrowLabel] (falling back to the enum name) and the dimmed detail
        /// "Name = Value" (Value being the raw enum value); a miss returns the supplied defaults with null detail.
        /// The converted Everything flag value is also returned (its raw value in Value mode) for callers to reuse.
        /// </summary>
        /// <param name="enumType">The enum type.</param>
        /// <param name="defaultNothing">Fallback label when no Nothing semantic member is defined.</param>
        /// <param name="defaultEverything">Fallback label when no Everything semantic member is defined.</param>
        /// <param name="mode">The bit semantics mode; inferred from [Flags] when omitted.</param>
        /// <returns>A six-tuple: nothingLabel / nothingInfo / everythingLabel / everythingInfo /
        /// everythingValue (flag OR / raw value) / nothingValue (the raw Nothing member value, null
        /// when undefined).</returns>
        public static (string nothingLabel, string nothingInfo, string everythingLabel, string everythingInfo, long everythingValue, int? nothingValue)
            ResolveSpecialLabels(Type enumType, string defaultNothing, string defaultEverything, BitMaskValueMode? mode = null) {
            var valueMode = mode ?? GetBitMaskValueMode(enumType);

            string nothingLabel = defaultNothing, nothingInfo = null;
            string everythingLabel = defaultEverything, everythingInfo = null;
            int? nothingValue = null;

            var names = Enum.GetNames(enumType);
            var values = Enum.GetValues(enumType).Cast<object>().Select(ToInt64).ToArray();

            // Value mode: plain business values with no bit semantics get a dedicated branch free of bit arithmetic.
            if (valueMode == BitMaskValueMode.Value) {
                long everythingRaw = 0;

                for (var i = 0; i < names.Length; i++) {
                    var name = names[i];
                    var lower = name.ToLowerInvariant();
                    var raw = values[i];

                    var field = enumType.GetField(name, Public | Static);
                    var display = field?.GetLabel() ?? name;

                    if (lower is "none" or "nothing") {
                        nothingLabel = display;
                        nothingInfo = $"{name} = {raw}";
                        nothingValue = (int)raw;
                    } else if (lower is "all" or "everything") {
                        everythingLabel = display;
                        everythingInfo = $"{name} = {raw}";
                        everythingRaw = raw;
                    }
                }

                return (nothingLabel, nothingInfo, everythingLabel, everythingInfo, everythingRaw, nothingValue);
            }

            var normalFlags = new HashSet<long>();
            long everythingValue = 0;
            for (var i = 0; i < names.Length; i++) {
                var lower = names[i].ToLowerInvariant();
                if (lower is "none" or "nothing" or "all" or "everything") {
                    continue;
                }

                var flag = ToFlagValue(values[i], valueMode);
                if (flag == 0) {
                    continue;
                }

                normalFlags.Add(flag);
                everythingValue |= flag;
            }

            for (var i = 0; i < names.Length; i++) {
                var name = names[i];
                var lower = name.ToLowerInvariant();
                var raw = values[i];
                var flag = ToFlagValue(raw, valueMode);
                var isSemanticName = lower is "none" or "nothing" or "all" or "everything";

                if (!isSemanticName && flag != 0 && normalFlags.Contains(flag)) {
                    continue;
                }

                var field = enumType.GetField(name, Public | Static);
                var display = field?.GetLabel() ?? name;

                if (lower is "none" or "nothing" || (flag == 0 && !normalFlags.Contains(0))) {
                    nothingLabel = display;
                    nothingInfo = $"{name} = {raw}";
                    nothingValue = (int)raw;
                } else if (lower is "all" or "everything" || raw == -1 || (flag != 0 && flag == everythingValue)) {
                    everythingLabel = display;
                    everythingInfo = $"{name} = {raw}";
                }
            }

            return (nothingLabel, nothingInfo, everythingLabel, everythingInfo, everythingValue, nothingValue);
        }

        private static long ToInt64(object raw) {
            try {
                return Convert.ToInt64(raw);
            } catch (OverflowException) {
                // ulong values above long.MaxValue keep their bit pattern in the signed long.
                return unchecked((long)Convert.ToUInt64(raw));
            }
        }

    }
}
