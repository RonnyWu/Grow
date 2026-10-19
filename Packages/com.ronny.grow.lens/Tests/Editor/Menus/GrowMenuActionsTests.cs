// SPDX-License-Identifier: MIT

using Grow.Lens.Editor.Menus;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Grow.Lens.Tests.Editor.Menus {
    /// <summary>Tests for the path helpers used by the context menus.</summary>
    public class GrowMenuActionsTests {
        [Test]
        public void GetHierarchyPath_ReturnsRootToLeaf() {
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            var grandchild = new GameObject("Grand");
            child.transform.SetParent(root.transform);
            grandchild.transform.SetParent(child.transform);
            try {
                Assert.AreEqual("Root/Child/Grand", GrowMenuActions.GetHierarchyPath(grandchild));
            } finally {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GetHierarchyPath_Null_IsEmpty() {
            Assert.AreEqual(string.Empty, GrowMenuActions.GetHierarchyPath(null));
        }
    }
}
