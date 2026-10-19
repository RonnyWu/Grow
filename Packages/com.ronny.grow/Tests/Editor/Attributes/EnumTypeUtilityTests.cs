// SPDX-License-Identifier: MIT

using System.Linq;
using System.Text.RegularExpressions;
using Grow.Editor.Common.Enumeration;
using Grow.Editor.Inspector.EnumFlags;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Grow.Tests.Editor.Attributes {
    /// <summary>Boundary tests for enum metadata used by the flags multiselect.</summary>
    public class EnumTypeUtilityTests {
        private enum WideFlags : uint {
            Low = 1,
            High = 0x80000000u
        }

        [SetUp]
        public void SetUp() {
            EnumTypeUtility.ResetOverflowWarningsForTests();
        }

        [System.Flags]
        private enum HugeFlags : ulong {
            Low = 1,
            High = 1UL << 63
        }

        [Test]
        public void Parse_HighBitOutsideInt32_IsSkippedWithoutThrowing() {
            LogAssert.Expect(LogType.Warning, new Regex("32-bit flags range"));
            var infos = EnumTypeUtility.ParseEnumInfos(typeof(WideFlags));
            Assert.AreEqual(1, infos.Length);
            Assert.AreEqual("Low", infos[0].EnumName);
        }

        [Test]
        public void BuildFlags_UlongHighBit_DoesNotThrow() {
            var cache = EnumFlagsCache.BuildFlagsCache(typeof(HugeFlags));
            Assert.IsTrue(cache.Values.All(value => value == (int)HugeFlags.Low));
        }
    }
}
