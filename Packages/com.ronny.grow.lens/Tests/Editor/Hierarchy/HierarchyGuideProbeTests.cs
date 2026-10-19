// SPDX-License-Identifier: MIT

using Grow.Lens.Editor.Hierarchy;
using NUnit.Framework;

namespace Grow.Lens.Tests.Editor.Hierarchy {
    /// <summary>Verifies the internal Unity member names used by the guide probe.</summary>
    public class HierarchyGuideProbeTests {
        [Test]
        public void UnityInternalMembers_Resolve() {
            var error = HierarchyGuideProbe.TryValidateReflection();
            Assert.IsNull(error,
                "SceneHierarchyWindow/TreeViewGUI internal members changed; indent guides will fail closed. " +
                error);
        }
    }
}
