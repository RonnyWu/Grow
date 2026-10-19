// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Grow.Core.Attributes;
using Grow.Editor.Inspector.EnumFlags;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Grow.Tests.Editor.Attributes {
    [Flags]
    public enum SampleFlags {
        None = 0,
        A = 1,
        B = 2,
        C = 4,
        AB = A | B,
        All = A | B | C
    }

    [Flags]
    public enum ObsoleteFlags {
        A = 1,
        [Obsolete] B = 2,
        C = 4
    }

    [Flags]
    public enum DupZeroFlags {
        Zero = 0,
        A = 1,
        A2 = 1,
        B = 2
    }

    [Flags]
    public enum MinusOneAllFlags {
        A = 1,
        B = 2,
        All = -1
    }

    public sealed class FlagsHolder : ScriptableObject {
        [GrowEnumFlags] public SampleFlags Value;
    }

    /// <summary>
    /// Tests for the EnumFlagsGui display cache and bit-level backend: verified without a GUI, covering
    /// single-bit filtering / [Obsolete] marking / Nothing-Everything semantics / de-duplication /
    /// property round trips.
    /// </summary>
    [Category("Grow.Attributes")]
    public class EnumFlagsGuiTests {
        [Test]
        public void BuildFlagsCache_OnlySingleBitsParticipate() {
            var cache = EnumFlagsCache.BuildFlagsCache(typeof(SampleFlags));

            CollectionAssert.AreEqual(new[] { 1, 2, 4 }, cache.Values, "Only single-bit members should be selectable");
            Assert.AreEqual(3, cache.Values.Length, "Composite values (AB=3) and zero values (None=0) should not appear in Values");
            StringAssert.StartsWith("1 : A : A", cache.Labels[0]);
            StringAssert.StartsWith("2 : B : B", cache.Labels[1]);
            StringAssert.StartsWith("4 : C : C", cache.Labels[2]);
        }

        [Test]
        public void BuildFlagsCache_ObsoleteMemberIsMarked() {
            var cache = EnumFlagsCache.BuildFlagsCache(typeof(ObsoleteFlags));

            Assert.AreEqual(3, cache.Values.Length);
            Assert.IsTrue(cache.Obsoletes[1], "The [Obsolete] member with value 2 should be marked disabled");
            Assert.IsFalse(cache.Obsoletes[0]);
            Assert.IsFalse(cache.Obsoletes[2]);
        }

        [Test]
        public void BuildFlagsCache_SemanticMembersDriveNothingEverything() {
            var cache = EnumFlagsCache.BuildFlagsCache(typeof(SampleFlags));

            Assert.AreEqual("None", cache.NothingLabel, "An explicit None member should drive the Nothing label");
            Assert.AreEqual("None = 0", cache.NothingInfo);
            Assert.AreEqual("All", cache.EverythingLabel, "The all-bits composite member should drive the Everything label");
            Assert.AreEqual("All = 7", cache.EverythingInfo);
        }

        [Test]
        public void BuildFlagsCache_NoSemanticMembers_FallsBackToDefaults() {
            var cache = EnumFlagsCache.BuildFlagsCache(typeof(ObsoleteFlags));

            Assert.AreEqual("Nothing", cache.NothingLabel);
            Assert.AreEqual("Everything", cache.EverythingLabel);
            Assert.IsNull(cache.NothingInfo);
            Assert.IsNull(cache.EverythingInfo);
        }

        [Test]
        public void BuildFlagsCache_SkipsZeroAndDedupsDuplicatedBits() {
            var cache = EnumFlagsCache.BuildFlagsCache(typeof(DupZeroFlags));

            CollectionAssert.AreEqual(new[] { 1, 2 }, cache.Values, "Among equal-valued members (A/A2=1) only the first is kept, and zero-valued members are excluded");
        }

        [Test]
        public void BuildFlagsCache_MinusOneAllResolvesAsEverything() {
            var cache = EnumFlagsCache.BuildFlagsCache(typeof(MinusOneAllFlags));

            CollectionAssert.AreEqual(new[] { 1, 2 }, cache.Values, "A -1 member should not become a selectable single-bit entry");
            Assert.AreEqual("All", cache.EverythingLabel, "A -1 (All) member should be recognized as Everything");
        }

        [Test]
        public void Backend_SetClearSelectAll_RoundTripsToProperty() {
            var holder = ScriptableObject.CreateInstance<FlagsHolder>();
            try {
                holder.Value = SampleFlags.A | SampleFlags.B;
                var serialized = new SerializedObject(holder);
                var property = serialized.FindProperty("Value");
                Assert.IsNotNull(property);

                var backend = EnumFlagsGui.CreateBackend(property);

                Assert.IsTrue(backend.Contains(1));
                Assert.IsTrue(backend.Contains(2));
                Assert.IsFalse(backend.Contains(4));

                backend.SetValue(4, true);
                Assert.IsTrue(backend.Contains(4), "SetValue should write the bit straight to the property");
                backend.Apply();
                serialized.Update();
                Assert.AreEqual(SampleFlags.A | SampleFlags.B | SampleFlags.C, holder.Value, "Apply should write back to the target object");

                backend.Clear();
                Assert.AreEqual(0, property.intValue, "Clear should reset every bit");

                backend.SelectAll(new[] { 1, 2, 4 });
                Assert.AreEqual(7, property.intValue, "SelectAll should set every given bit");
                backend.Apply();
                serialized.Update();
                Assert.AreEqual(SampleFlags.All, holder.Value, "Apply after SelectAll should write back to the target object");
            } finally {
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void GetEnumType_ResolvesSingleEnumAndContainers() {
            Assert.AreEqual(typeof(SampleFlags), EnumFlagsCache.GetEnumType(typeof(SampleFlags)));
            Assert.AreEqual(typeof(SampleFlags), EnumFlagsCache.GetEnumType(typeof(SampleFlags[])));
            Assert.AreEqual(typeof(SampleFlags), EnumFlagsCache.GetEnumType(typeof(List<SampleFlags>)));
            Assert.IsNull(EnumFlagsCache.GetEnumType(typeof(int)));
            Assert.IsNull(EnumFlagsCache.GetEnumType(typeof(List<int>)));
            Assert.IsNull(EnumFlagsCache.GetEnumType(null));
        }
    }

    /// <summary>
    /// Tests for the existence and semantic metadata of the Tier A annotations themselves.
    /// </summary>
    [Category("Grow.Attributes")]
    public class InspectorAttributeMetaTests {
        [Test]
        public void HelpBoxAttribute_CarriesMessageAndKind() {
            var attr = new GrowHelpBoxAttribute("hello", GrowHelpBoxKind.Warning);
            Assert.AreEqual("hello", attr.Message);
            Assert.AreEqual(GrowHelpBoxKind.Warning, attr.Kind);

            var info = new GrowHelpBoxAttribute("info");
            Assert.AreEqual(GrowHelpBoxKind.Info, info.Kind, "The default help box kind should be Info");
            Assert.That(typeof(GrowHelpBoxAttribute).IsSubclassOf(typeof(UnityEngine.PropertyAttribute)), "A decorator attribute should derive from PropertyAttribute so the default Inspector picks it up");
        }

        [Test]
        public void SeparatorAttribute_ClampsNegativeValues() {
            var attr = new GrowSeparatorAttribute(-5f, -2f);
            Assert.AreEqual(0f, attr.Thickness);
            Assert.AreEqual(0f, attr.Margin);

            var def = new GrowSeparatorAttribute();
            Assert.AreEqual(1f, def.Thickness);
            Assert.AreEqual(6f, def.Margin);
        }

        [Test]
        public void EnumFlagsAttribute_TargetsFieldsOnly() {
            var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(typeof(GrowEnumFlagsAttribute), typeof(AttributeUsageAttribute));
            Assert.IsNotNull(usage);
            Assert.AreEqual(AttributeTargets.Field, usage.ValidOn, "EnumFlags may only be applied to fields");
        }
    }
}
