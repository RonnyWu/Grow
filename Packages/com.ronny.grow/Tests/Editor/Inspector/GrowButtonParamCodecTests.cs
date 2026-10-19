// SPDX-License-Identifier: MIT

using Grow.Editor.Inspector.Button;
using Grow.Editor.Inspector.EnumFlags;
using NUnit.Framework;

namespace Grow.Tests.Editor.Inspector {
    /// <summary>[Flags] parameter codec sample.</summary>
    [System.Flags]
    public enum CodecFlags {
        None = 0,
        A = 1,
        B = 2
    }

    /// <summary>Plain enum parameter codec sample.</summary>
    public enum CodecMode {
        Low = 0,
        High = 1
    }

    /// <summary>
    /// Tests for <see cref="GrowButtonParamCodec"/>: default values / normalization / EditorPrefs text round trips.
    /// </summary>
    [Category("Grow.Attributes")]
    public class GrowButtonParamCodecTests {
        [Test]
        public void DefaultValue_Enum_FirstMember() {
            Assert.AreEqual(CodecMode.Low, GrowButtonParamCodec.GetDefaultValue(typeof(CodecMode)));
        }

        [Test]
        public void DefaultValue_String_Empty() {
            Assert.AreEqual(string.Empty, GrowButtonParamCodec.GetDefaultValue(typeof(string)));
        }

        [Test]
        public void DefaultValue_LongAndDouble_Zero() {
            Assert.AreEqual(0L, GrowButtonParamCodec.GetDefaultValue(typeof(long)));
            Assert.AreEqual(0d, GrowButtonParamCodec.GetDefaultValue(typeof(double)));
        }

        [Test]
        public void RoundTrip_Int() {
            var text = GrowButtonParamCodec.ToParamString(typeof(int), 42);
            Assert.IsTrue(GrowButtonParamCodec.TryParseParam(typeof(int), text, out var value));
            Assert.AreEqual(42, value);
        }

        [Test]
        public void RoundTrip_BoolAndString() {
            Assert.IsTrue(GrowButtonParamCodec.TryParseParam(typeof(bool), GrowButtonParamCodec.ToParamString(typeof(bool), true), out var b));
            Assert.AreEqual(true, b);

            const string s = "hello world";
            Assert.IsTrue(GrowButtonParamCodec.TryParseParam(typeof(string), GrowButtonParamCodec.ToParamString(typeof(string), s), out var parsed));
            Assert.AreEqual(s, parsed);
        }

        [Test]
        public void RoundTrip_Float_UsesInvariantRoundTrip() {
            const float f = 1.25f;
            var text = GrowButtonParamCodec.ToParamString(typeof(float), f);
            Assert.IsTrue(GrowButtonParamCodec.TryParseParam(typeof(float), text, out var value));
            Assert.AreEqual(f, (float)value);
        }

        [Test]
        public void RoundTrip_FlagsEnum_CompositeValue() {
            var mask = CodecFlags.A | CodecFlags.B;
            var text = GrowButtonParamCodec.ToParamString(typeof(CodecFlags), mask);
            Assert.IsTrue(GrowButtonParamCodec.TryParseParam(typeof(CodecFlags), text, out var value));
            Assert.AreEqual(mask, value);
        }

        [Test]
        public void TryParse_InvalidText_ReturnsFalse() {
            Assert.IsFalse(GrowButtonParamCodec.TryParseParam(typeof(int), "not-a-number", out _));
        }

        [Test]
        public void NormalizeParam_FlagsEnum_WrapsValueBackend() {
            var normalized = GrowButtonParamCodec.NormalizeParam(typeof(CodecFlags), CodecFlags.A);
            Assert.IsInstanceOf<EnumValueBackend>(normalized);
            Assert.AreEqual(1, ((EnumValueBackend)normalized).Mask);
        }

        [Test]
        public void NormalizeParam_PlainEnum_Unchanged() {
            Assert.AreEqual(CodecMode.High, GrowButtonParamCodec.NormalizeParam(typeof(CodecMode), CodecMode.High));
        }
    }
}
