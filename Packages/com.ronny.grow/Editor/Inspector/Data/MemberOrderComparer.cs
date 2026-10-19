// SPDX-License-Identifier: MIT

using System.Collections.Generic;

namespace Grow.Editor.Inspector.Data {
    /// <summary>
    /// Unified sort-key comparer: Order → fields before buttons → (field declaration order / button method name).
    /// <para>Fields and [GrowButton] methods share this ordering space so an explicit [GrowPropertyOrder] can place
    /// a button among the fields, while buttons with the default order (int.MaxValue) naturally sink to the
    /// bottom.</para>
    /// </summary>
    internal sealed class MemberOrderComparer : IComparer<MemberModel> {
        /// <summary>The shared singleton comparer.</summary>
        public static readonly MemberOrderComparer Instance = new();

        /// <summary>Compares two members: Order first, then "fields before buttons", then
        /// declaration order / method name.</summary>
        /// <param name="a">The left member.</param>
        /// <param name="b">The right member.</param>
        /// <returns>The comparison result.</returns>
        public int Compare(MemberModel a, MemberModel b) {
            var orderCompare = a.Order.CompareTo(b.Order);
            if (orderCompare != 0) {
                return orderCompare;
            }

            var aIsField = a is FieldModel;
            if (aIsField != b is FieldModel) {
                return aIsField ? -1 : 1;
            }

            if (aIsField) {
                return a.DeclarationIndex.CompareTo(b.DeclarationIndex);
            }

            return string.CompareOrdinal(((ButtonModel)a).Method.Name, ((ButtonModel)b).Method.Name);
        }
    }
}
