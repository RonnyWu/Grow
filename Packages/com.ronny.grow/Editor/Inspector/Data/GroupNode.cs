// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using Grow.Core.Attributes;

namespace Grow.Editor.Inspector.Data {
    /// <summary>
    /// Internal node used while building the group tree: path / segment name / direct members / child nodes /
    /// subtree anchor / presentation kind.
    /// <para>Used only by the <see cref="InspectorTypeModel"/> tokenization pass (built once, not part of the
    /// model's public structure).</para>
    /// </summary>
    internal sealed class GroupNode {
        /// <summary>The normalized full group path (such as "General/Physics").</summary>
        public readonly string Path;

        /// <summary>The segment name at this level (the last path segment, such as "Physics").</summary>
        public readonly string Segment;

        /// <summary>Members belonging directly to this group (excluding child groups).</summary>
        public readonly List<MemberModel> Direct = new();

        /// <summary>Child group nodes.</summary>
        public readonly List<GroupNode> Children = new();

        /// <summary>The earliest-ordered member in the subtree (used for interleaving groups and
        /// anchoring tokens).</summary>
        public MemberModel Anchor;

        /// <summary>Group-level presentation kind (has child groups → Foldout; leaf group with any
        /// Box member → Box).</summary>
        public GrowGroupKind Kind;

        /// <summary>Builds a group tree node.</summary>
        /// <param name="path">The normalized full path.</param>
        /// <param name="segment">The segment name at this level.</param>
        public GroupNode(string path, string segment) {
            Path = path;
            Segment = segment;
        }
    }
}
