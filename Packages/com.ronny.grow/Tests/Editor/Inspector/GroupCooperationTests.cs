// SPDX-License-Identifier: MIT

using System.Linq;
using System.Text.RegularExpressions;
using Grow.Core.Attributes;
using Grow.Editor.Inspector.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Grow.Tests.Editor.Inspector {
    public sealed class InterleaveProbe : ScriptableObject {
        [GrowGroup("A")][GrowPropertyOrder(0)] public int A1;
        [GrowGroup("B")][GrowPropertyOrder(1)] public int B1;
        [GrowGroup("A")] public int A2;
        [GrowGroup("B")] public int B2;
    }

    public sealed class MixedGroupProbe : ScriptableObject {
        [GrowGroup("Ops")][GrowPropertyOrder(10)] public int OpCount;

        [GrowGroup("Ops")]
        [GrowButton("Run")]
        public void RunOps() {
        }
    }

    public sealed class ButtonBoxProbe : ScriptableObject {
        [GrowGroup("Ops")] public int Cfg;

        [GrowGroup("Ops", GrowGroupKind.Box)]
        [GrowButton("Go")]
        public void Go() {
        }
    }

    public sealed class OrderProbe : ScriptableObject {
        public int Late;

        [GrowButton]
        public void Zebra() {
        }

        [GrowButton]
        [GrowPropertyOrder(-10)]
        public void Early() {
        }
    }

    public sealed class ContradictProbe : ScriptableObject {
        public bool Flag;

        [GrowShowIf("Flag")][GrowHideIf("Flag")] public int Doomed;
    }

    public sealed class NestedProbe : ScriptableObject {
        [GrowGroup("P")][GrowPropertyOrder(-50)] public int PDirect;
        [GrowGroup("P/Fold")][GrowPropertyOrder(-40)] public int FoldFirst;

        [GrowGroup("P/Leaf", GrowGroupKind.Box)]
        [GrowPropertyOrder(-30)]
        public int LeafBox;

        [GrowGroup("P/Fold")][GrowPropertyOrder(-20)] public int FoldSecond;
        [GrowGroup("Q")] public int QField;
    }

    /// <summary>
    /// Annotation-cooperation model tests: single-instance / contiguous groups, fields and [GrowButton] merging into
    /// one group, Kind Box resolution, unified ordering (buttons sink by default / an explicit order interleaves
    /// them), and ShowIf+HideIf contradiction diagnostics.
    /// </summary>
    [Category("Grow.Attributes")]
    public class GroupCooperationTests {
        private static InspectorTypeModel BuildModel<T>() where T : ScriptableObject {
            InspectorTypeModel.ClearCache();
            var probe = ScriptableObject.CreateInstance<T>();
            try {
                return InspectorTypeModel.Build(new SerializedObject(probe));
            } finally {
                Object.DestroyImmediate(probe);
            }
        }

        private static string[] MemberPaths(GroupModel group) {
            return group.Members.Select(m => m is FieldToken ft ? ft.Field.Path : "[GrowButton]" + ((ButtonToken)m).Button.Method.Name).ToArray();
        }

        [Test]
        public void Build_InterleavedOrdersDoNotSplitGroup() {
            var model = BuildModel<InterleaveProbe>();

            var groups = model.Tokens.OfType<GroupToken>().ToList();
            Assert.AreEqual(2, groups.Count, "There should be exactly two single-instance group tokens");
            Assert.AreEqual("A", groups[0].Group.Title);
            Assert.AreEqual("B", groups[1].Group.Title);

            CollectionAssert.AreEqual(new[] { "A1", "A2" }, MemberPaths(groups[0].Group), "Group A should stay contiguous and contain all its members");
            CollectionAssert.AreEqual(new[] { "B1", "B2" }, MemberPaths(groups[1].Group), "Group B should stay contiguous and contain all its members");
        }

        [Test]
        public void Build_FieldAndButtonShareSingleGroupInstance() {
            var model = BuildModel<MixedGroupProbe>();

            var group = model.Tokens.OfType<GroupToken>().Single(g => g.Group.Title == "Ops");
            Assert.AreEqual(1, model.Tokens.OfType<GroupToken>().Count(t => t.Group.Title == "Ops"), "Fields and buttons sharing a path should merge into one group instance");

            Assert.AreEqual(1, group.Group.Members.Count(m => m is FieldToken));
            Assert.AreEqual(1, group.Group.Members.Count(m => m is ButtonToken));
            CollectionAssert.AreEqual(new[] { "OpCount", "[GrowButton]RunOps" }, MemberPaths(group.Group), "Within a group the unified ordering applies: at the same order fields come before buttons");
        }

        [Test]
        public void Build_ButtonGroupBoxKind() {
            var model = BuildModel<ButtonBoxProbe>();

            Assert.AreEqual(GrowGroupKind.Box, model.Buttons[0].GroupKind, "ButtonModel should carry the group Kind");

            var group = model.Tokens.OfType<GroupToken>().Single(g => g.Group.Title == "Ops");
            Assert.AreEqual(GrowGroupKind.Box, group.Group.Kind, "Any member declaring Box makes the group Box");
        }

        [Test]
        public void Build_ButtonOrder_DefaultBottomExplicitInterleaves() {
            var model = BuildModel<OrderProbe>();

            var kinds = model.Tokens.Select(t => t switch {
                FieldToken ft => "F:" + ft.Field.Path,
                ButtonToken bt => "B:" + bt.Button.Method.Name,
                GroupToken => "G",
                _ => "?"
            }).ToArray();

            CollectionAssert.AreEqual(
                new[] { "B:Early", "F:Late", "B:Zebra" },
                kinds,
                "A button with an explicit order can move ahead of fields; a default-order button sinks below the fields");

            CollectionAssert.AreEqual(
                new[] { "Early", "Zebra" },
                model.Buttons.Select(b => b.Method.Name).ToArray(),
                "The Buttons list itself is sorted by Order");
        }

        [Test]
        public void Build_WarnsOnShowHideContradiction() {
            LogAssert.Expect(LogType.Warning, new Regex("Doomed"));
            var model = BuildModel<ContradictProbe>();

            Assert.AreEqual(1, model.Tokens.OfType<FieldToken>().Count(t => t.Field.Path == "Doomed"));
        }

        [Test]
        public void Build_NestedPathCreatesGroupTree() {
            var model = BuildModel<NestedProbe>();

            // Top level: the two groups P and Q, with P anchored earliest (PDirect order=-50)
            var top = model.Tokens.OfType<GroupToken>().ToList();
            Assert.AreEqual(2, top.Count);
            Assert.AreEqual("P", top[0].Group.Title);
            Assert.AreEqual("Q", top[1].Group.Title);

            var p = top[0].Group;
            Assert.AreEqual(GrowGroupKind.Foldout, p.Kind, "A container group with child groups should be a Foldout");
            Assert.AreEqual("P", p.Segment);
            Assert.AreEqual(3, p.Members.Count, "P holds 1 direct member + 2 child groups");

            // Inside P: the direct member PDirect comes first, then P/Fold and P/Leaf ordered by
            // their anchors (-40 < -30)
            Assert.AreEqual("PDirect", ((FieldToken)p.Members[0]).Field.Path);
            var fold = ((GroupToken)p.Members[1]).Group;
            var leaf = ((GroupToken)p.Members[2]).Group;
            Assert.AreEqual("P/Fold", fold.Title);
            Assert.AreEqual("Fold", fold.Segment);
            Assert.AreEqual(GrowGroupKind.Foldout, fold.Kind);
            CollectionAssert.AreEqual(
                new[] { "FoldFirst", "FoldSecond" },
                fold.Members.OfType<FieldToken>().Select(f => f.Field.Path).ToArray(),
                "Members inside a child group should be sorted by Order");
            Assert.AreEqual("P/Leaf", leaf.Title);
            Assert.AreEqual("Leaf", leaf.Segment);
            Assert.AreEqual(GrowGroupKind.Box, leaf.Kind, "A leaf group is Box when any member declares Box");
        }

        [Test]
        public void Build_NestedGroupFlattenKeepsAllMembers() {
            var model = BuildModel<NestedProbe>();
            var paths = new System.Collections.Generic.List<string>();
            foreach (var token in model.Tokens) {
                CollectFieldPaths(token, paths);
            }

            CollectionAssert.AreEqual(
                new[] { "PDirect", "FoldFirst", "FoldSecond", "LeafBox", "QField" },
                paths,
                "After flattening the nesting, the unified order should cover every member");
        }

        private static void CollectFieldPaths(LayoutToken token, System.Collections.Generic.List<string> paths) {
            switch (token) {
                case FieldToken ft:
                    paths.Add(ft.Field.Path);
                    break;
                case ButtonToken bt:
                    paths.Add("[GrowButton]" + bt.Button.Method.Name);
                    break;
                case GroupToken gt:
                    foreach (var member in gt.Group.Members) {
                        CollectFieldPaths(member, paths);
                    }

                    break;
            }
        }
    }
}
