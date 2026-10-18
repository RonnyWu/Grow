// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Grow.Core.Log;
using Grow.Kernel.Loop;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.TestTools;

namespace Grow.Tests.Editor.Loop {
    public class PlayerLoopManagerTests {
        private sealed class SystemA { }

        private sealed class SystemB { }

        private sealed class SystemC { }

        private sealed class MissingAnchor { }

        [SetUp]
        public void SetUp() {
            GrowLog.Reset();
            PlayerLoopManager.ResetStaticState();
            RemoveType(typeof(SystemC));
        }

        [TearDown]
        public void TearDown() {
            PlayerLoopManager.RemoveInstalled();
            PlayerLoopManager.ResetStaticState();
            RemoveType(typeof(SystemC));
            GrowLog.Reset();
        }

        [Test]
        public void Install_AfterAnchor_InsertsOnce() {
            var installed = PlayerLoopManager.Install<SystemA>(
                () => { },
                typeof(UnityEngine.PlayerLoop.Update),
                InsertLocation.After);

            Assert.IsTrue(installed);
            Assert.AreEqual(1, CountType(typeof(SystemA)));
            Assert.IsTrue(PlayerLoopManager.IsModified);
        }

        [Test]
        public void Install_SameTypeTwice_KeepsOneNode() {
            Assert.IsTrue(PlayerLoopManager.Install<SystemA>(
                () => { },
                typeof(UnityEngine.PlayerLoop.Update),
                InsertLocation.After));
            Assert.IsTrue(PlayerLoopManager.Install<SystemA>(
                () => { },
                typeof(UnityEngine.PlayerLoop.FixedUpdate),
                InsertLocation.After));

            Assert.AreEqual(1, CountType(typeof(SystemA)));
        }

        [Test]
        public void Install_BeforeNestedAnchor() {
            var installed = PlayerLoopManager.Install<SystemA>(
                () => { },
                typeof(UnityEngine.PlayerLoop.Update.ScriptRunBehaviourUpdate),
                InsertLocation.Before);

            Assert.IsTrue(installed);
            Assert.AreEqual(1, CountType(typeof(SystemA)));
        }

        [Test]
        public void Install_UnderFixedUpdateScript_IsNestedInsideTheFixedUpdateGroup() {
            Assert.IsTrue(PlayerLoopManager.Install<SystemA>(
                () => { },
                typeof(UnityEngine.PlayerLoop.FixedUpdate.ScriptRunBehaviourFixedUpdate),
                InsertLocation.After));

            var loop = PlayerLoop.GetCurrentPlayerLoop();
            var group = Array.Find(
                loop.subSystemList,
                system => system.type == typeof(UnityEngine.PlayerLoop.FixedUpdate));

            Assert.AreEqual(typeof(UnityEngine.PlayerLoop.FixedUpdate), group.type);
            Assert.NotNull(group.subSystemList);
            Assert.AreEqual(1, Count(group.subSystemList, typeof(SystemA)));
            Assert.IsFalse(
                Array.Exists(loop.subSystemList, system => system.type == typeof(SystemA)),
                "a fixed-step system must not be a top-level sibling");
        }

        [Test]
        public void Remove_RemovesManagedNode() {
            Assert.IsTrue(PlayerLoopManager.Install<SystemA>(
                () => { },
                typeof(UnityEngine.PlayerLoop.Update),
                InsertLocation.After));

            PlayerLoopManager.Remove<SystemA>();

            Assert.AreEqual(0, CountType(typeof(SystemA)));
            Assert.IsFalse(PlayerLoopManager.IsModified);
        }

        [Test]
        public void RemoveInstalled_LeavesUnmanagedNode() {
            Assert.IsTrue(PlayerLoopManager.Install<SystemA>(
                () => { },
                typeof(UnityEngine.PlayerLoop.Update),
                InsertLocation.After));
            Assert.IsTrue(PlayerLoopManager.Install<SystemB>(
                () => { },
                typeof(UnityEngine.PlayerLoop.Update),
                InsertLocation.After));
            AddType(typeof(SystemC));

            PlayerLoopManager.RemoveInstalled();

            Assert.AreEqual(0, CountType(typeof(SystemA)));
            Assert.AreEqual(0, CountType(typeof(SystemB)));
            Assert.AreEqual(1, CountType(typeof(SystemC)), "a node inserted by other code must survive");
        }

        [Test]
        public void MissingAnchor_ReturnsFalseAndDoesNotInsert() {
            LogAssert.Expect(LogType.Error, new Regex("anchor"));

            var installed = PlayerLoopManager.Install<SystemA>(
                () => { },
                typeof(MissingAnchor),
                InsertLocation.After);

            Assert.IsFalse(installed);
            Assert.AreEqual(0, CountType(typeof(SystemA)));
            Assert.IsFalse(PlayerLoopManager.IsModified);
        }

        [Test]
        public void RestoreOriginal_RestoresTheSnapshot() {
            var before = TopLevelTypes();
            Assert.IsTrue(PlayerLoopManager.Install<SystemA>(
                () => { },
                typeof(UnityEngine.PlayerLoop.Update),
                InsertLocation.After));

            PlayerLoopManager.RestoreOriginal();

            CollectionAssert.AreEqual(before, TopLevelTypes());
            Assert.AreEqual(0, CountType(typeof(SystemA)));
            Assert.IsFalse(PlayerLoopManager.IsModified);
        }

        [Test]
        public void ResetStaticState_RemovesManagedNodes() {
            Assert.IsTrue(PlayerLoopManager.Install<SystemA>(
                () => { },
                typeof(UnityEngine.PlayerLoop.Update),
                InsertLocation.After));

            PlayerLoopManager.ResetStaticState();

            Assert.AreEqual(0, CountType(typeof(SystemA)));
            Assert.IsFalse(PlayerLoopManager.IsModified);
        }

        private static int CountType(Type type) {
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            return Count(loop.subSystemList, type);
        }

        private static int Count(PlayerLoopSystem[] systems, Type type) {
            if (systems == null) {
                return 0;
            }

            var count = 0;
            foreach (var system in systems) {
                if (system.type == type) {
                    count++;
                }

                count += Count(system.subSystemList, type);
            }

            return count;
        }

        private static List<Type> TopLevelTypes() {
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            var types = new List<Type>();
            if (loop.subSystemList == null) {
                return types;
            }

            foreach (var system in loop.subSystemList) {
                types.Add(system.type);
            }

            return types;
        }

        private static void AddType(Type type) {
            var current = PlayerLoop.GetCurrentPlayerLoop();
            var systems = PlayerLoopGraph.CloneArray(current.subSystemList);
            Assert.IsTrue(PlayerLoopGraph.TryInsert(
                ref systems,
                typeof(UnityEngine.PlayerLoop.Update),
                new PlayerLoopSystem { type = type },
                InsertLocation.After));
            current.subSystemList = systems;
            PlayerLoop.SetPlayerLoop(current);
        }

        private static void RemoveType(Type type) {
            var current = PlayerLoop.GetCurrentPlayerLoop();
            var systems = PlayerLoopGraph.CloneArray(current.subSystemList);
            var changed = false;
            while (PlayerLoopGraph.TryRemove(ref systems, type)) {
                changed = true;
            }

            if (!changed) {
                return;
            }

            current.subSystemList = systems;
            PlayerLoop.SetPlayerLoop(current);
        }
    }
}
