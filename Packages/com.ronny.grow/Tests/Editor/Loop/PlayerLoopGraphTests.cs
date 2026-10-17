// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Grow.Kernel.Loop;
using NUnit.Framework;
using UnityEngine.LowLevel;

namespace Grow.Tests.Editor.Loop {
    public class PlayerLoopGraphTests {
        private static class RootA { }

        private static class RootB { }

        private static class ChildA { }

        private static class ChildB { }

        private static PlayerLoopSystem[] CreateTree() {
            return new[] {
                new PlayerLoopSystem {
                    type = typeof(RootA),
                    subSystemList = new[] {
                        new PlayerLoopSystem { type = typeof(ChildA) },
                        new PlayerLoopSystem { type = typeof(ChildB) }
                    }
                },
                new PlayerLoopSystem { type = typeof(RootB) }
            };
        }

        [Test]
        public void FindIndex_FindsTopLevelType() {
            var systems = CreateTree();

            Assert.AreEqual(0, PlayerLoopGraph.FindIndex(systems, typeof(RootA)));
            Assert.AreEqual(1, PlayerLoopGraph.FindIndex(systems, typeof(RootB)));
            Assert.AreEqual(-1, PlayerLoopGraph.FindIndex(systems, typeof(ChildA)));
        }

        [Test]
        public void ContainsType_FindsNestedType() {
            var systems = CreateTree();

            Assert.IsTrue(PlayerLoopGraph.ContainsType(systems, typeof(ChildB)));
            Assert.IsFalse(PlayerLoopGraph.ContainsType(systems, typeof(string)));
        }

        [Test]
        public void CloneArray_DeepCopiesSubTrees() {
            var systems = CreateTree();

            var clone = PlayerLoopGraph.CloneArray(systems);
            clone[0].subSystemList[0] = new PlayerLoopSystem { type = typeof(string) };

            Assert.AreEqual(typeof(ChildA), systems[0].subSystemList[0].type);
            Assert.AreNotSame(systems[0].subSystemList, clone[0].subSystemList);
        }

        [Test]
        public void TryInsert_AfterTopLevelAnchor() {
            var systems = CreateTree();
            var node = new PlayerLoopSystem { type = typeof(ChildA) };

            var inserted = PlayerLoopGraph.TryInsert(
                ref systems,
                typeof(RootA),
                node,
                InsertLocation.After);

            Assert.IsTrue(inserted);
            Assert.AreEqual(typeof(RootA), systems[0].type);
            Assert.AreEqual(typeof(ChildA), systems[1].type);
            Assert.AreEqual(typeof(RootB), systems[2].type);
        }

        [Test]
        public void TryInsert_BeforeNestedAnchor() {
            var systems = CreateTree();
            var node = new PlayerLoopSystem { type = typeof(string) };

            var inserted = PlayerLoopGraph.TryInsert(
                ref systems,
                typeof(ChildB),
                node,
                InsertLocation.Before);

            Assert.IsTrue(inserted);
            Assert.AreEqual(typeof(ChildA), systems[0].subSystemList[0].type);
            Assert.AreEqual(typeof(string), systems[0].subSystemList[1].type);
            Assert.AreEqual(typeof(ChildB), systems[0].subSystemList[2].type);
        }

        [Test]
        public void TryInsert_MissingAnchor_LeavesInputUnchanged() {
            var systems = CreateTree();
            var before = systems;

            var inserted = PlayerLoopGraph.TryInsert(
                ref systems,
                typeof(string),
                new PlayerLoopSystem { type = typeof(string) },
                InsertLocation.After);

            Assert.IsFalse(inserted);
            Assert.AreSame(before, systems);
        }

        [Test]
        public void TryRemove_RemovesNestedType() {
            var systems = CreateTree();

            var removed = PlayerLoopGraph.TryRemove(ref systems, typeof(ChildA));

            Assert.IsTrue(removed);
            Assert.AreEqual(1, systems[0].subSystemList.Length);
            Assert.AreEqual(typeof(ChildB), systems[0].subSystemList[0].type);
        }

        [Test]
        public void TryRemove_MissingType_LeavesInputUnchanged() {
            var systems = CreateTree();
            var before = systems;

            var removed = PlayerLoopGraph.TryRemove(ref systems, typeof(string));

            Assert.IsFalse(removed);
            Assert.AreSame(before, systems);
        }

        [Test]
        public void TryRemove_TopLevelType() {
            var systems = CreateTree();

            var removed = PlayerLoopGraph.TryRemove(ref systems, typeof(RootA));

            Assert.IsTrue(removed);
            Assert.AreEqual(1, systems.Length);
            Assert.AreEqual(typeof(RootB), systems[0].type);
        }

        [Test]
        public void CloneArray_HandlesNull() {
            Assert.IsNull(PlayerLoopGraph.CloneArray(null));
            Assert.IsFalse(PlayerLoopGraph.ContainsType(null, typeof(RootA)));
            Assert.AreEqual(-1, PlayerLoopGraph.FindIndex(null, typeof(RootA)));
        }

        [Test]
        public void Types_AreStableInOrder() {
            var systems = CreateTree();
            var types = new List<Type>();
            foreach (var system in systems) types.Add(system.type);

            CollectionAssert.AreEqual(new[] { typeof(RootA), typeof(RootB) }, types);
        }
    }
}
