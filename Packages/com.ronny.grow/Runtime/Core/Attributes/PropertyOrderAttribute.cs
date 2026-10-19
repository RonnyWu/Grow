// SPDX-License-Identifier: MIT

using System;

namespace Grow.Core.Attributes {
    /// <summary>
    /// Sets the drawing order of a serialized field or a <see cref="GrowButtonAttribute"/> method in
    /// Grow's Inspector. Smaller values are drawn first; members without the attribute use
    /// <see cref="int.MaxValue"/>.
    /// <para>At equal order, fields are drawn before buttons. Fields keep their serialized order, while
    /// buttons are ordered by method name. Unity's default Inspector ignores the attribute.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
    public sealed class GrowPropertyOrderAttribute : Attribute {
        /// <summary>Sort key; smaller values are drawn first.</summary>
        public readonly int Order;

        /// <summary>Creates an order attribute.</summary>
        /// <param name="order">Sort key; smaller values are drawn first.</param>
        public GrowPropertyOrderAttribute(int order) {
            Order = order;
        }
    }
}
