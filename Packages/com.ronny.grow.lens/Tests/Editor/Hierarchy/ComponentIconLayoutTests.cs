// SPDX-License-Identifier: MIT

using Grow.Lens.Editor.Hierarchy;
using NUnit.Framework;
using UnityEngine;

namespace Grow.Lens.Tests.Editor.Hierarchy {
    /// <summary>Pure layout tests for the Hierarchy component icons.</summary>
    public class ComponentIconLayoutTests {
        [Test]
        public void Icons_AreRightAlignedAndClipped() {
            var row = new Rect(10f, 20f, 100f, 16f);

            Assert.AreEqual(4, ComponentIconLayout.GetVisibleCount(row, 20f, 10, 16f));
            Assert.AreEqual(new Rect(94f, 20f, 16f, 16f), ComponentIconLayout.GetRect(row, 0, 16f));
            Assert.AreEqual(new Rect(78f, 20f, 16f, 16f), ComponentIconLayout.GetRect(row, 1, 16f));
            Assert.AreEqual(new Rect(62f, 20f, 16f, 16f), ComponentIconLayout.GetRect(row, 2, 16f));
            Assert.AreEqual(new Rect(46f, 20f, 16f, 16f), ComponentIconLayout.GetRect(row, 3, 16f));
        }

        [Test]
        public void Icons_StopAtNameWidth() {
            var row = new Rect(10f, 20f, 100f, 16f);

            Assert.AreEqual(0, ComponentIconLayout.GetVisibleCount(row, 90f, 10, 16f));
            Assert.AreEqual(1, ComponentIconLayout.GetVisibleCount(row, 75f, 10, 16f));
            Assert.AreEqual(2, ComponentIconLayout.GetVisibleCount(row, 20f, 2, 16f));
        }

        [Test]
        public void InvalidLayout_HasNoIcons() {
            Assert.AreEqual(0, ComponentIconLayout.GetVisibleCount(new Rect(0f, 0f, 0f, 16f), 0f, 3, 16f));
            Assert.AreEqual(0, ComponentIconLayout.GetVisibleCount(new Rect(0f, 0f, 100f, 16f), 0f, 3, 0f));
            Assert.AreEqual(0, ComponentIconLayout.GetVisibleCount(new Rect(0f, 0f, 100f, 16f), 0f, 0, 16f));
        }

        [Test]
        public void FocusedInspectorMembers_Resolve() {
            var error = ItemComponentIcon.TryValidateFocusedInspector();
            Assert.IsNull(error,
                "UnityEditor.PropertyEditor.OpenPropertyEditor changed; icon clicks will use the fallback. " +
                error);
        }
    }
}
