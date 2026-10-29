// SPDX-License-Identifier: MIT

using Grow.Lens.Editor.Hierarchy;
using NUnit.Framework;
using UnityEngine;

namespace Grow.Lens.Tests.Editor.Hierarchy {
    /// <summary>Layout tests for the right-edge active toggle.</summary>
    public class ItemActiveToggleTests {
        [Test]
        public void Toggle_IsPinnedToTheRightEdge() {
            var row = new Rect(10f, 20f, 100f, 16f);
            var rect = ItemActiveToggle.GetRect(row);

            Assert.AreEqual(92f, rect.x);
            Assert.AreEqual(20f, rect.y);
            Assert.AreEqual(16f, rect.width);
            Assert.AreEqual(16f, rect.height);
            Assert.AreEqual(ItemActiveToggle.ReservedWidth, row.xMax - rect.x);
        }
    }
}
