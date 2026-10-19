// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using System.Reflection;
using Grow.Core.Attributes;
using Grow.Editor.Inspector.Condition;
using UnityEngine;

namespace Grow.Editor.Inspector.Data {
    /// <summary>
    /// Common base for drawable members (serialized fields / [GrowButton] methods) carrying the shared facets:
    /// ordering, grouping and labels.
    /// <para>Fields and buttons share one ordering and grouping space, which is what lets the annotations work
    /// together.</para>
    /// </summary>
    internal abstract class MemberModel {
        /// <summary>Display name (fields: [GrowLabel] preferred; buttons: [GrowButton.Label] /
        /// method name).</summary>
        public readonly string Label;

        /// <summary>Unity <c>[Tooltip]</c> text, empty when the field has none.</summary>
        public readonly string Tooltip;

        /// <summary>The explicit [GrowPropertyOrder] value; <see cref="int.MaxValue"/> when unspecified.</summary>
        public readonly int Order;

        /// <summary>Declaration-order stability tie-break (fields: serialization iteration order;
        /// buttons: unused, falling back to the method name).</summary>
        public readonly int DeclarationIndex;

        /// <summary>Whether the member belongs to a group.</summary>
        public readonly bool HasGroup;

        /// <summary>The group path (full text; "/" segments are normalized to build the hierarchy).</summary>
        public readonly string GroupTitle;

        /// <summary>The initially declared group presentation kind (a group is Box when any member is Box).</summary>
        public readonly GrowGroupKind GroupKind;

        /// <summary>Constructor for derived models sharing the ordering / grouping / label facets.</summary>
        protected MemberModel(string label, int order, int declarationIndex, bool hasGroup, string groupTitle, GrowGroupKind groupKind,
            string tooltip = "") {
            Label = label;
            Tooltip = tooltip;
            Order = order;
            DeclarationIndex = declarationIndex;
            HasGroup = hasGroup;
            GroupTitle = groupTitle;
            GroupKind = groupKind;
        }
    }

    /// <summary>Draw metadata for a [GrowButton] method.</summary>
    internal sealed class ButtonModel : MemberModel {
        /// <summary>The reflected target method.</summary>
        public readonly MethodInfo Method;

        /// <summary>The method parameters (supported simple types only; see
        /// InspectorTypeModel.IsSupportedParamType).</summary>
        public readonly ParameterInfo[] Parameters;

        public ButtonModel(string label, int order, bool hasGroup, string groupTitle, GrowGroupKind groupKind, MethodInfo method, ParameterInfo[] parameters)
            : base(label, order, 0, hasGroup, groupTitle, groupKind) {
            Method = method;
            Parameters = parameters;
        }
    }

    /// <summary>Draw metadata for a single serialized field.</summary>
    internal sealed class FieldModel : MemberModel {
        /// <summary>The root property path (as used by SerializedObject.FindProperty; usually the
        /// field name).</summary>
        public readonly string Path;

        /// <summary>The ShowIf clause list.</summary>
        public readonly IReadOnlyList<ConditionClause> ShowClauses;

        /// <summary>The HideIf clause list.</summary>
        public readonly IReadOnlyList<ConditionClause> HideClauses;

        /// <summary>The EnableIf clause list (all must hold for the field to be editable).</summary>
        public readonly IReadOnlyList<ConditionClause> EnableClauses;

        /// <summary>The DisableIf clause list (any one holding disables the field).</summary>
        public readonly IReadOnlyList<ConditionClause> DisableClauses;

        /// <summary>The OnValueChanged callback method name; empty means no callback.</summary>
        public readonly string OnChangedMethod;

        private GUIContent _content;

        /// <summary>Cached GUIContent for the field label (avoids per-repaint allocations); built lazily.</summary>
        public GUIContent Content => _content ??= string.IsNullOrEmpty(Tooltip)
            ? new GUIContent(Label)
            : new GUIContent(Label, Tooltip);

        public FieldModel(string path, string label,
            IReadOnlyList<ConditionClause> showClauses, IReadOnlyList<ConditionClause> hideClauses,
            IReadOnlyList<ConditionClause> enableClauses, IReadOnlyList<ConditionClause> disableClauses,
            string onChangedMethod,
            int order, int declarationIndex, bool hasGroup, string groupTitle, GrowGroupKind groupKind,
            string tooltip = "")
            : base(label, order, declarationIndex, hasGroup, groupTitle, groupKind, tooltip) {
            Path = path;
            ShowClauses = showClauses;
            HideClauses = hideClauses;
            EnableClauses = enableClauses;
            DisableClauses = disableClauses;
            OnChangedMethod = onChangedMethod;
        }
    }

    /// <summary>
    /// Draw metadata for a group (members already follow the unified ordering; they may be fields, buttons or
    /// nested child groups).
    /// </summary>
    internal sealed class GroupModel {
        /// <summary>The full group path (normalized "/" join; also part of the foldout state key).</summary>
        public readonly string Title;

        /// <summary>The title of this level (the last path segment, used as the header of nested drawing).</summary>
        public readonly string Segment;

        /// <summary>Group-level presentation kind: a leaf group is Box when any member declares
        /// Box; a container group with child groups is always Foldout.</summary>
        public readonly GrowGroupKind Kind;

        /// <summary>The tokens inside the group (FieldToken / ButtonToken / nested GroupToken),
        /// already ordered.</summary>
        public readonly IReadOnlyList<LayoutToken> Members;

        public GroupModel(string title, string segment, GrowGroupKind kind, IReadOnlyList<LayoutToken> members) {
            Title = title;
            Segment = segment;
            Kind = kind;
            Members = members;
        }
    }
}
