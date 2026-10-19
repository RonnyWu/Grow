// SPDX-License-Identifier: MIT

using System;

namespace Grow.Core.Attributes {
    /// <summary>How a Grow group is presented in the Inspector.</summary>
    public enum GrowGroupKind {
        /// <summary>Collapsible group (default): a header that expands and collapses.</summary>
        Foldout = 0,

        /// <summary>Boxed group: a card that frames its members.</summary>
        Box = 1
    }

    /// <summary>
    /// Places a serialized field or a <see cref="GrowButtonAttribute"/> method in a named group.
    /// <para>The path is split on <c>/</c>, so <c>"General/Physics"</c> nests a Physics group inside
    /// General. Members inside a group are ordered by <see cref="GrowPropertyOrderAttribute"/>.</para>
    /// <para>Grouping only affects Grow's editor; Unity's default Inspector ignores the attribute.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
    public sealed class GrowGroupAttribute : Attribute {
        /// <summary>Group path; <c>/</c> separates nested levels. An empty path means no group.</summary>
        public readonly string Path;

        /// <summary>Group presentation (Foldout or Box).</summary>
        public readonly GrowGroupKind Kind;

        /// <summary>Creates a group attribute.</summary>
        /// <param name="path">Group path; empty means no group.</param>
        /// <param name="kind">Group presentation.</param>
        public GrowGroupAttribute(string path, GrowGroupKind kind = GrowGroupKind.Foldout) {
            Path = path ?? string.Empty;
            Kind = kind;
        }
    }
}
