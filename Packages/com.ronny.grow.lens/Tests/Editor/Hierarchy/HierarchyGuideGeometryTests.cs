// SPDX-License-Identifier: MIT

using Grow.Lens.Editor.Hierarchy;
using NUnit.Framework;

namespace Grow.Lens.Tests.Editor.Hierarchy {
    /// <summary>Pure geometry tests for the Hierarchy indent guides.</summary>
    public class HierarchyGuideGeometryTests {
        [Test]
        public void StandardHierarchy_DepthAndAncestorCenters() {
            Assert.IsTrue(HierarchyGuideGeometry.TryComputeDepth(44f, 32f, 14f, 12f, 0f, out var root));
            Assert.AreEqual(0, root);

            Assert.IsTrue(HierarchyGuideGeometry.TryComputeDepth(58f, 32f, 14f, 12f, 0f, out var child));
            Assert.AreEqual(1, child);
            Assert.AreEqual(38f, HierarchyGuideGeometry.GuideX(32f, 14f, 12f, 0));

            Assert.IsTrue(HierarchyGuideGeometry.TryComputeDepth(72f, 32f, 14f, 12f, 0f, out var grandchild));
            Assert.AreEqual(2, grandchild);
            Assert.AreEqual(38f, HierarchyGuideGeometry.GuideX(32f, 14f, 12f, 0));
            Assert.AreEqual(52f, HierarchyGuideGeometry.GuideX(32f, 14f, 12f, 1));
        }

        [Test]
        public void NoSceneHeader_ShiftsBaseIndent() {
            Assert.IsTrue(HierarchyGuideGeometry.TryComputeDepth(58f, 46f, 14f, 12f, 0f, out var root));
            Assert.AreEqual(0, root);
            Assert.IsFalse(root > 0);

            Assert.IsTrue(HierarchyGuideGeometry.TryComputeDepth(72f, 46f, 14f, 12f, 0f, out var child));
            Assert.AreEqual(1, child);
            Assert.AreEqual(52f, HierarchyGuideGeometry.GuideX(46f, 14f, 12f, 0));
        }

        [Test]
        public void CustomIndentAndFoldoutWidth_AreHonored() {
            const float baseIndent = 32f;
            const float indentWidth = 16f;
            const float foldoutWidth = 16f;
            const float marginLeft = 2f;
            const float contentBase = baseIndent + foldoutWidth + marginLeft;

            Assert.IsTrue(HierarchyGuideGeometry.TryComputeDepth(contentBase, baseIndent, indentWidth,
                foldoutWidth, marginLeft, out var root));
            Assert.AreEqual(0, root);

            Assert.IsTrue(HierarchyGuideGeometry.TryComputeDepth(contentBase + indentWidth, baseIndent,
                indentWidth, foldoutWidth, marginLeft, out var child));
            Assert.AreEqual(1, child);
            Assert.AreEqual(40f, HierarchyGuideGeometry.GuideX(baseIndent, indentWidth, foldoutWidth, 0));
        }

        [Test]
        public void OffColumn_IsRejected() {
            Assert.IsFalse(HierarchyGuideGeometry.TryComputeDepth(59f, 32f, 14f, 12f, 0f, out _));
            Assert.IsFalse(HierarchyGuideGeometry.TryComputeDepth(40f, 32f, 14f, 12f, 0f, out _));
        }

        [Test]
        public void InvalidIndent_IsRejected() {
            Assert.IsFalse(HierarchyGuideGeometry.TryComputeDepth(58f, 32f, 0f, 12f, 0f, out _));
            Assert.IsFalse(HierarchyGuideGeometry.TryComputeDepth(58f, 32f, -1f, 12f, 0f, out _));
        }

        [Test]
        public void FollowingSibling_RespectsTreeDepth() {
            // 0 / 1 A / 2 A1 / 2 A2 / 1 B / 2 B1 / 2 B2 / 1 C
            var depths = new[] { 0, 1, 2, 2, 1, 2, 2, 1 };

            Assert.IsTrue(HierarchyGuideGeometry.HasFollowingSibling(depths, 2, 2));
            Assert.IsFalse(HierarchyGuideGeometry.HasFollowingSibling(depths, 3, 2));
            Assert.IsTrue(HierarchyGuideGeometry.HasFollowingSibling(depths, 3, 1));
            Assert.IsTrue(HierarchyGuideGeometry.HasFollowingSibling(depths, 5, 2));
            Assert.IsFalse(HierarchyGuideGeometry.HasFollowingSibling(depths, 6, 2));
            Assert.IsTrue(HierarchyGuideGeometry.HasFollowingSibling(depths, 4, 1));
            Assert.IsFalse(HierarchyGuideGeometry.HasFollowingSibling(depths, 7, 1));
        }

        [Test]
        public void FollowingSibling_SkipsDescendants() {
            // 0 / 1 / 2 / 3 (child) / 2 (next sibling) / 1
            var depths = new[] { 0, 1, 2, 3, 2, 1 };

            Assert.IsTrue(HierarchyGuideGeometry.HasFollowingSibling(depths, 2, 2));
            Assert.IsFalse(HierarchyGuideGeometry.HasFollowingSibling(depths, 3, 3));
            Assert.IsFalse(HierarchyGuideGeometry.HasFollowingSibling(depths, 4, 2));
            Assert.IsFalse(HierarchyGuideGeometry.HasFollowingSibling(depths, 0, 0));
            Assert.IsFalse(HierarchyGuideGeometry.HasFollowingSibling(depths, -1, 1));
            Assert.IsFalse(HierarchyGuideGeometry.HasFollowingSibling(depths, 6, 1));
        }
    }
}
