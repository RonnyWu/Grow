// SPDX-License-Identifier: MIT

using System.Linq;
using Grow.Editor.Common.Enumeration;
using Grow.Editor.Common.MultiSelect;
using Grow.Editor.Inspector.EnumFlags;
using NUnit.Framework;

namespace Grow.Tests.Editor.Attributes {
    /// <summary>Bit-index (Bits) semantics sample: the enum value is the bit index.</summary>
    public enum BitIndexKinds {
        Zero = 0,
        One = 1,
        Two = 2,
        Three = 3
    }

    /// <summary>Capacity filtering sample: a value of 9 exceeds the capacity and must be dropped.</summary>
    public enum WideBitsKinds {
        B0 = 0,
        B1 = 1,
        B4 = 4,
        B9 = 9
    }

    /// <summary>Plain-value semantics sample: contains an explicit None / All.</summary>
    public enum PlainKinds {
        None = 0,
        Alpha = 1,
        Beta = 2,
        All = -1
    }

    /// <summary>Plain-value semantics sample: no explicit Nothing / Everything member.</summary>
    public enum NoSpecialKinds {
        Alpha = 0,
        Beta = 1,
        Gamma = 2
    }

    /// <summary>
    /// Behaviour-locking tests for <see cref="EnumMultiSelectDataBuilder"/>: filtering, ordering, the label format,
    /// [Obsolete] handling and Nothing / Everything semantics across the Flags / Bits / Value modes, plus
    /// <see cref="EnumInfo.IsObsolete"/> resolution.
    /// </summary>
    [Category("Grow.Attributes")]
    public class EnumMultiSelectDataBuilderTests {
        [Test]
        public void BuildFlags_SingleBitValuesCombosAndMap() {
            var data = EnumMultiSelectDataBuilder.BuildFlags(typeof(SampleFlags));

            CollectionAssert.AreEqual(new[] { 1, 2, 4 }, data.Values, "Only single-bit members are selectable");
            CollectionAssert.AreEqual(new[] { 3 }, data.ComboValues, "AB should appear as a combination alias row while All is excluded as Everything");
            Assert.AreEqual("1 : A : A", data.Labels[0]);
            Assert.AreEqual("3 : AB : AB", data.ComboLabels[0]);
            Assert.IsTrue(data.SupportsAliasMatch);
            Assert.AreEqual("All", data.ValueToNameMap[7]);
        }

        [Test]
        public void BuildFlags_ObsoletesFromEnumInfo() {
            var data = EnumMultiSelectDataBuilder.BuildFlags(typeof(ObsoleteFlags));
            CollectionAssert.AreEqual(new[] { false, true, false }, data.Obsoletes, "The [Obsolete] marking on B should be reflected in Obsoletes");
        }

        [Test]
        public void BuildFlags_IncludeCombosFalse_EmptyCombos() {
            var data = EnumMultiSelectDataBuilder.BuildFlags(typeof(SampleFlags), false);
            Assert.IsEmpty(data.ComboValues);
        }

        [Test]
        public void BuildBits_FilterByCapacityAndKeepIndexOrder() {
            var data = EnumMultiSelectDataBuilder.BuildBits(typeof(WideBitsKinds), 5);
            CollectionAssert.AreEqual(new[] { 0, 1, 4 }, data.Values, "Only bit indices with 0 <= value < 5 are kept");
            Assert.AreEqual("4 : B4 : B4", data.Labels[2]);
            Assert.IsNull(data.ComboValues, "Bits semantics produce no combination alias rows");
            Assert.IsFalse(data.SupportsAliasMatch);
        }

        [Test]
        public void BuildBits_ZeroIsValidBit_NoImplicitNothing() {
            var data = EnumMultiSelectDataBuilder.BuildBits(typeof(BitIndexKinds), 4);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, data.Values, "Under Bits, 0 is a valid bit index (Nothing needs an explicit none/nothing member)");
            Assert.AreEqual("Nothing", data.NothingLabel);
            Assert.IsNull(data.NothingInfo);
        }

        [Test]
        public void BuildValues_ExcludesNothingAndEverythingMembers() {
            var data = EnumMultiSelectDataBuilder.BuildValues(typeof(PlainKinds));
            CollectionAssert.AreEqual(new[] { 1, 2 }, data.Values, "None / All should be excluded as Nothing / Everything");
            Assert.AreEqual("1 : Alpha : Alpha", data.Labels[0]);
            Assert.AreEqual("None", data.NothingLabel);
            Assert.AreEqual("All", data.EverythingLabel);
        }

        [Test]
        public void BuildValues_NoSpecialMembers_KeepsZero() {
            var data = EnumMultiSelectDataBuilder.BuildValues(typeof(NoSpecialKinds));
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, data.Values, "Without an explicit None, 0 is an ordinary value and must not be excluded");
        }

        [Test]
        public void EnumInfo_CarriesIsObsoleteFlag() {
            var infos = EnumTypeUtility.GetEnumInfos(typeof(ObsoleteFlags));
            Assert.IsFalse(infos.Single(i => i.EnumName == "A").IsObsolete);
            Assert.IsTrue(infos.Single(i => i.EnumName == "B").IsObsolete);
            Assert.IsFalse(infos.Single(i => i.EnumName == "C").IsObsolete);
        }

        [Test]
        public void EnumFlagsCacheEntry_DelegatesToBuilder() {
            var viaCache = EnumFlagsCache.BuildFlagsCache(typeof(SampleFlags));
            var viaBuilder = EnumMultiSelectDataBuilder.BuildFlags(typeof(SampleFlags));
            CollectionAssert.AreEqual(viaBuilder.Values, viaCache.Values);
            CollectionAssert.AreEqual(viaBuilder.ComboValues, viaCache.ComboValues);
        }
    }
}
