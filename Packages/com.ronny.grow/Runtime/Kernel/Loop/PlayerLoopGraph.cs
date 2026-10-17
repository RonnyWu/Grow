// SPDX-License-Identifier: MIT

using System;
using UnityEngine.LowLevel;

namespace Grow.Kernel.Loop {
    /// <summary>Where to insert a system relative to its anchor.</summary>
    internal enum InsertLocation {
        /// <summary>Before the anchor.</summary>
        Before,

        /// <summary>After the anchor.</summary>
        After
    }

    /// <summary>
    /// Pure operations on a <see cref="PlayerLoopSystem"/> tree.
    /// </summary>
    /// <remarks>
    /// Every operation copies before it rebuilds, so a failed lookup never partially mutates the
    /// caller's array. Anchors are searched at the top level first and then recursively inside
    /// sub-groups, which is what lets a system be placed after a concrete phase such as
    /// <c>Update.ScriptRunBehaviourUpdate</c>.
    /// <para>
    /// The type does not know any Grow-specific system type, so it can be tested on a synthetic tree
    /// without touching the real player loop.
    /// </para>
    /// </remarks>
    internal static class PlayerLoopGraph {
        /// <summary>Finds the first top-level system of <paramref name="type"/>; returns -1 when absent.</summary>
        public static int FindIndex(PlayerLoopSystem[] systems, Type type) {
            if (systems == null || type == null) return -1;

            for (var i = 0; i < systems.Length; i++) {
                if (systems[i].type == type) return i;
            }

            return -1;
        }

        /// <summary>Deep-clones a system node, including its whole sub-tree.</summary>
        public static PlayerLoopSystem Clone(PlayerLoopSystem system) {
            var clone = system;
            clone.subSystemList = CloneArray(system.subSystemList);
            return clone;
        }

        /// <summary>Deep-clones a system list, including every sub-tree.</summary>
        public static PlayerLoopSystem[] CloneArray(PlayerLoopSystem[] systems) {
            if (systems == null) return null;

            var clone = new PlayerLoopSystem[systems.Length];
            for (var i = 0; i < systems.Length; i++) clone[i] = Clone(systems[i]);
            return clone;
        }

        /// <summary>True when the type exists anywhere in the tree.</summary>
        public static bool ContainsType(PlayerLoopSystem[] systems, Type type) {
            if (systems == null || type == null) return false;

            for (var i = 0; i < systems.Length; i++) {
                var system = systems[i];
                if (system.type == type) return true;
                if (system.subSystemList != null && ContainsType(system.subSystemList, type)) return true;
            }

            return false;
        }

        /// <summary>
        /// Inserts <paramref name="system"/> before or after the first system of
        /// <paramref name="anchorType"/>, searching the top level first and then sub-groups.
        /// </summary>
        /// <returns>False when the anchor is absent; the input is then left unchanged.</returns>
        public static bool TryInsert(
            ref PlayerLoopSystem[] systems,
            Type anchorType,
            PlayerLoopSystem system,
            InsertLocation location) {
            if (anchorType == null) return false;
            if (systems == null) systems = Array.Empty<PlayerLoopSystem>();

            var index = FindIndex(systems, anchorType);
            if (index >= 0) {
                var position = location == InsertLocation.Before ? index : index + 1;
                systems = InsertAt(systems, position, system);
                return true;
            }

            for (var i = 0; i < systems.Length; i++) {
                var child = systems[i].subSystemList;
                if (child == null || child.Length == 0) continue;
                if (!TryInsert(ref child, anchorType, system, location)) continue;

                var parent = systems[i];
                parent.subSystemList = child;
                systems[i] = parent;
                return true;
            }

            return false;
        }

        /// <summary>Removes the first system of <paramref name="type"/>, searching sub-groups too.</summary>
        /// <returns>False when the type is absent; the input is then left unchanged.</returns>
        public static bool TryRemove(ref PlayerLoopSystem[] systems, Type type) {
            if (systems == null || type == null) return false;

            var index = FindIndex(systems, type);
            if (index >= 0) {
                systems = RemoveAt(systems, index);
                return true;
            }

            for (var i = 0; i < systems.Length; i++) {
                var child = systems[i].subSystemList;
                if (child == null || child.Length == 0) continue;
                if (!TryRemove(ref child, type)) continue;

                var parent = systems[i];
                parent.subSystemList = child;
                systems[i] = parent;
                return true;
            }

            return false;
        }

        private static PlayerLoopSystem[] InsertAt(
            PlayerLoopSystem[] source,
            int index,
            PlayerLoopSystem item) {
            var array = new PlayerLoopSystem[source.Length + 1];
            if (index > 0) Array.Copy(source, 0, array, 0, index);
            array[index] = item;
            if (index < source.Length) Array.Copy(source, index, array, index + 1, source.Length - index);
            return array;
        }

        private static PlayerLoopSystem[] RemoveAt(PlayerLoopSystem[] source, int index) {
            var array = new PlayerLoopSystem[source.Length - 1];
            if (index > 0) Array.Copy(source, 0, array, 0, index);
            if (index < source.Length - 1) Array.Copy(source, index + 1, array, index, source.Length - index - 1);
            return array;
        }
    }
}
