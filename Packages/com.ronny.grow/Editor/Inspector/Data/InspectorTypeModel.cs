// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Grow.Core.Attributes;
using Grow.Editor.Inspector.Condition;
using UnityEditor;
using UnityEngine;

namespace Grow.Editor.Inspector.Data {
    /// <summary>
    /// Per-type cached drawing model: built from the root-level visible properties of a SerializedObject plus the
    /// type's [GrowButton] methods, applying the PropertyOrder / Group (fields and buttons merged into one
    /// instance) / ShowIf+HideIf / EnableIf+DisableIf / OnValueChanged annotations to produce an ordered sequence of
    /// draw tokens (button tokens included).
    /// <para>The build result is cached per target type and reset automatically on domain reload (static fields);
    /// tests or explicit invalidation can call <see cref="ClearCache"/>.</para>
    /// </summary>
    internal sealed class InspectorTypeModel {
        /// <summary>The ordered draw tokens (loose fields / loose buttons / groups interleaved in order).</summary>
        public readonly IReadOnlyList<LayoutToken> Tokens;

        /// <summary>The [GrowButton] method list (same source as the token stream; sorted by Order
        /// and method name).</summary>
        public readonly IReadOnlyList<ButtonModel> Buttons;

        /// <summary>Constructs a model instance (normally created through <see cref="GetFor"/> or
        /// <see cref="Build"/>).</summary>
        /// <param name="tokens">The ordered draw tokens.</param>
        /// <param name="buttons">The button method list.</param>
        public InspectorTypeModel(List<LayoutToken> tokens, List<ButtonModel> buttons) {
            Tokens = tokens;
            Buttons = buttons;
        }

        private static readonly Dictionary<Type, InspectorTypeModel> ModelCache = new();

        /// <summary>Gets (building on demand) the drawing model for the given SerializedObject,
        /// cached per target type.</summary>
        /// <param name="serializedObject">The target serialized object.</param>
        /// <returns>The drawing model for that type (an empty model when the target type cannot be
        /// resolved).</returns>
        public static InspectorTypeModel GetFor(SerializedObject serializedObject) {
            var keyType = serializedObject.targetObject?.GetType();
            if (keyType == null) {
                return new InspectorTypeModel(new List<LayoutToken>(), new List<ButtonModel>());
            }

            if (!ModelCache.TryGetValue(keyType, out var model)) {
                model = Build(serializedObject);
                ModelCache[keyType] = model;
            }

            return model;
        }

        /// <summary>Clears the model cache (static fields reset automatically on domain reload;
        /// this is for tests and explicit invalidation).</summary>
        public static void ClearCache() {
            ModelCache.Clear();
        }

        /// <summary>
        /// Builds the model from a SerializedObject: walks the root-level visible properties (excluding m_Script)
        /// and collects the [GrowButton] methods, reads the annotations by reflection, then sorts everything and
        /// tokenizes it (fields and buttons share one ordering / grouping space).
        /// </summary>
        /// <param name="serializedObject">The target serialized object.</param>
        /// <returns>The completed drawing model.</returns>
        public static InspectorTypeModel Build(SerializedObject serializedObject) {
            var fields = new List<FieldModel>();
            var declarationIndex = 0;

            var iterator = serializedObject.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren)) {
                enterChildren = false;
                if (iterator.depth != 0) {
                    continue;
                }

                if (iterator.propertyPath == "m_Script") {
                    continue;
                }

                var field = BuildField(iterator, declarationIndex);
                if (field != null) {
                    fields.Add(field);
                    declarationIndex++;
                }
            }

            var buttons = BuildButtons(serializedObject.targetObject?.GetType());

            var sortedMembers = fields.Cast<MemberModel>().Concat(buttons).ToList();
            sortedMembers.Sort(MemberOrderComparer.Instance);

            var tokens = Tokenize(sortedMembers);
            return new InspectorTypeModel(tokens, buttons);
        }

        /// <summary>Reads a single root field's annotations and builds its FieldModel; returns null
        /// when reflection cannot reach it.</summary>
        /// <param name="property">The root field property.</param>
        /// <param name="declarationIndex">The declaration order (serialization iteration order).</param>
        /// <returns>The field model; null when it cannot be resolved.</returns>
        private static FieldModel BuildField(SerializedProperty property, int declarationIndex) {
            var path = property.propertyPath;
            var label = SerializedPropertyUtil.GetLabel(property);

            var showClauses = new List<ConditionClause>();
            var hideClauses = new List<ConditionClause>();
            var enableClauses = new List<ConditionClause>();
            var disableClauses = new List<ConditionClause>();
            var onChangedMethod = string.Empty;
            var tooltip = string.Empty;
            var order = int.MaxValue;
            var hasGroup = false;
            var groupTitle = string.Empty;
            var groupKind = GrowGroupKind.Foldout;

            if (SerializedPropertyUtil.TryGetFieldInfo(property, out var fieldInfo)) {
                order = fieldInfo.GetCustomAttribute<GrowPropertyOrderAttribute>(true)?.Order ?? int.MaxValue;

                var showAttrs = fieldInfo.GetCustomAttributes<GrowShowIfAttribute>(true).ToList();
                var hideAttrs = fieldInfo.GetCustomAttributes<GrowHideIfAttribute>(true).ToList();
                var enableAttrs = fieldInfo.GetCustomAttributes<GrowEnableIfAttribute>(true).ToList();
                var disableAttrs = fieldInfo.GetCustomAttributes<GrowDisableIfAttribute>(true).ToList();

                foreach (var showIf in showAttrs) {
                    showClauses.Add(ConditionResolver.Parse(showIf.Condition));
                }

                foreach (var hideIf in hideAttrs) {
                    hideClauses.Add(ConditionResolver.Parse(hideIf.Condition));
                }

                foreach (var enableIf in enableAttrs) {
                    enableClauses.Add(ConditionResolver.Parse(enableIf.Condition));
                }

                foreach (var disableIf in disableAttrs) {
                    disableClauses.Add(ConditionResolver.Parse(disableIf.Condition));
                }

                onChangedMethod = fieldInfo.GetCustomAttribute<GrowOnValueChangedAttribute>(true)?.MethodName ?? string.Empty;
                tooltip = fieldInfo.GetCustomAttribute<TooltipAttribute>(true)?.tooltip ?? string.Empty;
                if (!string.IsNullOrEmpty(onChangedMethod)) {
                    var targetType = property.serializedObject.targetObject?.GetType();
                    var callback = targetType?.GetMethod(onChangedMethod,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                        Type.EmptyTypes, null);
                    if (callback == null) {
                        Debug.LogWarning(
                            $"[GrowEditor] Field '{path}' names OnValueChanged method '{onChangedMethod}', but " +
                            "no parameterless method exists on the edited type.");
                    }
                }

                var group = fieldInfo.GetCustomAttribute<GrowGroupAttribute>(true);
                if (group != null && !string.IsNullOrEmpty(group.Path)) {
                    hasGroup = true;
                    groupTitle = group.Path;
                    groupKind = group.Kind;
                }

                DiagnoseVisibilityContradiction(path, showAttrs, hideAttrs);
            }

            return new FieldModel(path, label, showClauses, hideClauses, enableClauses, disableClauses, onChangedMethod,
                order, declarationIndex, hasGroup, groupTitle, groupKind, tooltip);
        }

        /// <summary>Collects the [GrowButton] methods (only int/long/float/double/bool/string/enum
        /// parameters are supported; others are skipped with a warning).</summary>
        /// <param name="targetType">The target type.</param>
        /// <returns>The button method models (sorted by Order + method name).</returns>
        private static List<ButtonModel> BuildButtons(Type targetType) {
            var buttons = new List<ButtonModel>();
            if (targetType == null) {
                return buttons;
            }

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var method in targetType.GetMethods(flags)) {
                var button = method.GetCustomAttribute<GrowButtonAttribute>(true);
                if (button == null || method.IsStatic || method.ContainsGenericParameters) {
                    continue;
                }

                if (method.ReturnType != typeof(void)) {
                    Debug.LogWarning(
                        $"[GrowButton] Method '{targetType.Name}.{method.Name}' must return void; skipped.");
                    continue;
                }

                var parameters = method.GetParameters();
                var unsupported = parameters.FirstOrDefault(p => !IsSupportedParamType(p.ParameterType));
                if (unsupported != null) {
                    Debug.LogWarning(
                        $"[GrowButton] Method '{targetType.Name}.{method.Name}' has unsupported parameter type " +
                        $"'{unsupported.ParameterType}'; skipped.");
                    continue;
                }

                var order = method.GetCustomAttribute<GrowPropertyOrderAttribute>(true)?.Order ?? int.MaxValue;
                var group = method.GetCustomAttribute<GrowGroupAttribute>(true);
                var label = !string.IsNullOrEmpty(button.Label)
                    ? button.Label
                    : method.Name;

                buttons.Add(new ButtonModel(label, order,
                    group != null && !string.IsNullOrEmpty(group.Path), group?.Path ?? string.Empty,
                    group?.Kind ?? GrowGroupKind.Foldout, method, parameters));
            }

            buttons.Sort(MemberOrderComparer.Instance);
            return buttons;
        }

        /// <summary>Whether a [GrowButton] parameter type is supported (simple scalar types and
        /// enums only).</summary>
        /// <param name="type">The parameter type.</param>
        /// <returns>Whether the type is supported.</returns>
        private static bool IsSupportedParamType(Type type) {
            return type == typeof(int) || type == typeof(long) || type == typeof(float) || type == typeof(double) ||
                   type == typeof(bool) || type == typeof(string) || type.IsEnum;
        }

        /// <summary>
        /// Converts the sorted members into a token sequence: groups form a hierarchy from their normalized Path
        /// ("/" segments); one logical group merges into a single instance; a group is anchored at the earliest
        /// member of its subtree and stays contiguous (it never splits into several runs). A leaf group is Box when
        /// any member is Box; a container group with child groups is always Foldout.
        /// </summary>
        /// <param name="sortedMembers">The sorted member list.</param>
        /// <returns>The ordered token sequence.</returns>
        private static List<LayoutToken> Tokenize(List<MemberModel> sortedMembers) {
            var loose = new List<MemberModel>();
            var roots = new List<GroupNode>();
            var byPath = new Dictionary<string, GroupNode>();

            foreach (var member in sortedMembers) {
                if (!member.HasGroup || string.IsNullOrEmpty(member.GroupTitle)) {
                    loose.Add(member);
                    continue;
                }

                var segments = SplitGroupPath(member.GroupTitle);
                if (segments.Length == 0) {
                    loose.Add(member);
                    continue;
                }

                GroupNode parent = null;
                var prefix = string.Empty;
                GroupNode leaf = null;
                for (var i = 0; i < segments.Length; i++) {
                    prefix = i == 0 ? segments[i] : prefix + "/" + segments[i];
                    if (!byPath.TryGetValue(prefix, out var node)) {
                        node = new GroupNode(prefix, segments[i]);
                        byPath[prefix] = node;
                        if (parent == null) {
                            roots.Add(node);
                        } else {
                            parent.Children.Add(node);
                        }
                    }

                    parent = node;
                    leaf = node;
                }

                leaf.Direct.Add(member);
            }

            foreach (var root in roots) {
                ResolveNode(root);
            }

            return EmitItems(loose, roots);
        }

        /// <summary>Normalizes a group path: trims leading/trailing whitespace, splits on "/" and
        /// drops empty segments.</summary>
        /// <param name="path">The raw group path.</param>
        /// <returns>The array of non-empty segments.</returns>
        private static string[] SplitGroupPath(string path) {
            var raw = path.Split('/');
            var segments = new List<string>(raw.Length);
            foreach (var segment in raw) {
                var trimmed = segment.Trim();
                if (trimmed.Length > 0) {
                    segments.Add(trimmed);
                }
            }

            return segments.ToArray();
        }

        /// <summary>Post-order node resolution: anchor = the earliest member of the subtree; Kind:
        /// child groups → Foldout, otherwise any direct Box member → Box.</summary>
        /// <param name="node">The node to resolve.</param>
        private static void ResolveNode(GroupNode node) {
            MemberModel best = null;
            foreach (var member in node.Direct) {
                best = BestOf(best, member);
            }

            foreach (var child in node.Children) {
                ResolveNode(child);
                best = BestOf(best, child.Anchor);
            }

            node.Anchor = best;
            node.Kind = node.Children.Count > 0
                ? GrowGroupKind.Foldout
                : node.Direct.Any(m => m.GroupKind == GrowGroupKind.Box)
                    ? GrowGroupKind.Box
                    : GrowGroupKind.Foldout;
        }

        /// <summary>Returns the member that sorts first (the smaller of a and b under the unified
        /// comparer).</summary>
        /// <param name="a">Candidate A.</param>
        /// <param name="b">Candidate B.</param>
        /// <returns>The member that sorts first.</returns>
        private static MemberModel BestOf(MemberModel a, MemberModel b) {
            if (a == null) {
                return b;
            }

            return b != null && MemberOrderComparer.Instance.Compare(b, a) < 0 ? b : a;
        }

        /// <summary>Sorts one level's members and child nodes by their anchors, then interleaves
        /// them (each node stays contiguous; subtrees never split).</summary>
        /// <param name="members">The loose members.</param>
        /// <param name="childNodes">The child nodes.</param>
        /// <returns>The ordered token sequence.</returns>
        private static List<LayoutToken> EmitItems(List<MemberModel> members, List<GroupNode> childNodes) {
            var items = new List<MemberModel>();
            foreach (var member in members) {
                items.Add(member);
            }

            foreach (var child in childNodes) {
                items.Add(child.Anchor);
            }
            // Sort by anchor member (anchors are unique, so the comparison is stable)
            items.Sort(MemberOrderComparer.Instance);

            var tokens = new List<LayoutToken>(items.Count);
            var nodeByAnchor = new Dictionary<MemberModel, GroupNode>();
            foreach (var child in childNodes) {
                nodeByAnchor[child.Anchor] = child;
            }

            foreach (var item in items) {
                if (nodeByAnchor.TryGetValue(item, out var node)) {
                    tokens.Add(new GroupToken(BuildGroup(node)));
                } else {
                    tokens.Add(MemberToToken(item));
                }
            }

            return tokens;
        }

        /// <summary>Recursively builds a group node into a GroupModel (members = child-group
        /// anchors + direct members, sorted uniformly).</summary>
        /// <param name="node">The group node.</param>
        /// <returns>The group model.</returns>
        private static GroupModel BuildGroup(GroupNode node) {
            var members = new List<MemberModel>();
            foreach (var child in node.Children) {
                members.Add(child.Anchor);
            }

            foreach (var direct in node.Direct) {
                members.Add(direct);
            }
            // Sort by anchor member (anchors are unique, so the comparison is stable)
            members.Sort(MemberOrderComparer.Instance);

            var tokens = new List<LayoutToken>(members.Count);
            var nodeByAnchor = new Dictionary<MemberModel, GroupNode>();
            foreach (var child in node.Children) {
                nodeByAnchor[child.Anchor] = child;
            }

            foreach (var item in members) {
                if (nodeByAnchor.TryGetValue(item, out var childNode)) {
                    tokens.Add(new GroupToken(BuildGroup(childNode)));
                } else {
                    tokens.Add(MemberToToken(item));
                }
            }

            return new GroupModel(node.Path, node.Segment, node.Kind, tokens);
        }

        /// <summary>Converts a single member into its token (field → FieldToken, button → ButtonToken).</summary>
        /// <param name="member">The member model.</param>
        /// <returns>The corresponding token.</returns>
        private static LayoutToken MemberToToken(MemberModel member) {
            return member is FieldModel field ? (LayoutToken)new FieldToken(field) : new ButtonToken((ButtonModel)member);
        }

        /// <summary>Build-time diagnostic: ShowIf and HideIf declare the same expression → the
        /// field can never be visible (once per type).</summary>
        /// <param name="path">The field path (log identifier).</param>
        /// <param name="shows">The ShowIf annotation list.</param>
        /// <param name="hides">The HideIf annotation list.</param>
        private static void DiagnoseVisibilityContradiction(string path, IReadOnlyCollection<GrowShowIfAttribute> shows, IReadOnlyCollection<GrowHideIfAttribute> hides) {
            if (shows.Count == 0 || hides.Count == 0) {
                return;
            }

            var hidden = shows.Select(s => s.Condition).Intersect(hides.Select(h => h.Condition), StringComparer.Ordinal).FirstOrDefault();
            if (hidden != null) {
                Debug.LogWarning(
                    $"[GrowEditor] Field '{path}' declares both [GrowShowIf(\"{hidden}\")] and " +
                    $"[GrowHideIf(\"{hidden}\")]; the field can never be visible.");
            }
        }
    }
}
