// SPDX-License-Identifier: MIT

using Grow.Lens.Editor.Hierarchy;
using NUnit.Framework;
using UnityEngine;

namespace Grow.Lens.Tests.Editor.Hierarchy {
    /// <summary>Pure parser tests for the formatted Hierarchy separators.</summary>
    public class HierarchySeparatorTests {
        [Test]
        public void ParsesBackgroundAndText() {
            Assert.IsTrue(HierarchySeparator.TryParse("##2E7D32 Level A", out var spec));
            Assert.IsTrue(spec.Background.HasValue);
            Assert.AreEqual(new Color32(0x2E, 0x7D, 0x32, 0xFF), (Color32)spec.Background.Value);
            Assert.IsFalse(spec.TextColor.HasValue);
            Assert.AreEqual("Level A", spec.DisplayText);
        }

        [Test]
        public void ParsesOptionalTextColorAndThreeDigitColors() {
            Assert.IsTrue(HierarchySeparator.TryParse("##F00#FFF Title", out var spec));
            Assert.AreEqual(new Color32(0xFF, 0x00, 0x00, 0xFF), (Color32)spec.Background.Value);
            Assert.AreEqual(new Color32(0xFF, 0xFF, 0xFF, 0xFF), (Color32)spec.TextColor.Value);
            Assert.AreEqual("Title", spec.DisplayText);
        }

        [Test]
        public void BuildName_RoundTripsDialogValues() {
            var background = new Color32(0x2E, 0x7D, 0x32, 0xFF);
            var text = new Color32(0xFF, 0xFF, 0xFF, 0xFF);

            Assert.AreEqual("##2E7D32#FFFFFF Level A",
                HierarchySeparator.BuildName(" Level A ", background, text));
            Assert.AreEqual("##2E7D32",
                HierarchySeparator.BuildName(string.Empty, background));
        }

        [Test]
        public void SupportsThemeBackgroundAndEmptyText() {
            Assert.IsTrue(HierarchySeparator.TryParse("##- Note", out var theme));
            Assert.IsFalse(theme.Background.HasValue);
            Assert.AreEqual("Note", theme.DisplayText);

            Assert.IsTrue(HierarchySeparator.TryParse("##FF0000", out var bandOnly));
            Assert.AreEqual(string.Empty, bandOnly.DisplayText);
        }

        [Test]
        public void KeepsTextThatStartsAfterAColor() {
            Assert.IsTrue(HierarchySeparator.TryParse("##FF0000 #not-a-color", out var spec));
            Assert.AreEqual("#not-a-color", spec.DisplayText);
        }

        [Test]
        public void RejectsInvalidOrOldStyleNames() {
            Assert.IsFalse(HierarchySeparator.TryParse("#3A3A3A old format", out _));
            Assert.IsFalse(HierarchySeparator.TryParse("##FF000", out _));
            Assert.IsFalse(HierarchySeparator.TryParse("##FF0000#FF", out _));
            Assert.IsFalse(HierarchySeparator.TryParse("##FF0000#GG", out _));
            Assert.IsFalse(HierarchySeparator.TryParse("A##FF0000", out _));
            Assert.IsFalse(HierarchySeparator.TryParse("##", out _));
            Assert.IsFalse(HierarchySeparator.TryParse(string.Empty, out _));
        }

        [Test]
        public void CacheReturnsTheSameSpec() {
            Assert.IsTrue(HierarchySeparator.TryGetSpec("##383838 Section", out var first));
            Assert.IsTrue(HierarchySeparator.TryGetSpec("##383838 Section", out var second));
            Assert.AreEqual(first.DisplayText, second.DisplayText);
            Assert.IsFalse(HierarchySeparator.IsSeparator("#383838 Section"));
        }
    }
}
