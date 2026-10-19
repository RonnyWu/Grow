// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using System.Linq;
using Grow.Core.Attributes;
using Grow.Editor.Inspector.Condition;
using Grow.Editor.Inspector.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Grow.Tests.Editor.Inspector {
    public sealed class CondProbe : ScriptableObject {
        public enum Level {
            Low,
            High
        }

        public bool Enable;
        public int Mode;
        public Level LevelValue;

        [GrowShowIf("Enable")] public int ShowOnEnable;
        [GrowShowIf("!Enable")] public int ShowOnDisable;
        [GrowShowIf("Mode == 2")] public int ShowOnMode2;
        [GrowShowIf("LevelValue == High")] public int ShowOnHighLevel;
        [GrowHideIf("Enable")] public int HideOnEnable;
        [GrowShowIf("HasTag()")] public int ShowOnMethod;

        [GrowShowIf("Enable")]
        [GrowGroup("General/Physics", GrowGroupKind.Box)]
        [GrowPropertyOrder(-10)]
        public float Early;

        [GrowGroup("General/Physics")] public float Later;
        [GrowPropertyOrder(-5)] public float OrderedFirst;

        public bool HasTag() {
            return LevelValue == Level.High;
        }
    }

    public sealed class TooltipProbe : ScriptableObject {
        [GrowLabel("Value")]
        [Tooltip("tooltip text")]
        public int Value;
    }

    /// <summary>
    /// ConditionResolver parsing and evaluation tests (pure logic, no GUI).
    /// </summary>
    [Category("Grow.Attributes")]
    public class ConditionResolverTests {
        [Test]
        public void Parse_NegationEqualityMethod() {
            var plain = ConditionResolver.Parse("Enable");
            Assert.IsFalse(plain.Negated);
            Assert.IsFalse(plain.IsEquality);
            Assert.AreEqual("Enable", plain.Target);

            var negated = ConditionResolver.Parse("!Enable");
            Assert.IsTrue(negated.Negated);

            var equal = ConditionResolver.Parse("Mode == 2");
            Assert.IsTrue(equal.IsEquality);
            Assert.AreEqual("Mode", equal.Target);
            Assert.AreEqual("2", equal.Literal);

            var method = ConditionResolver.Parse("HasTag()");
            Assert.IsTrue(method.IsMethodHint);
            Assert.AreEqual("HasTag", method.Target);
        }

        [Test]
        public void Evaluate_BoolTruthyAndNegation() {
            var probe = ScriptableObject.CreateInstance<CondProbe>();
            try {
                probe.Enable = true;
                Assert.IsTrue(ConditionResolver.Evaluate(probe, ConditionResolver.Parse("Enable")));
                Assert.IsFalse(ConditionResolver.Evaluate(probe, ConditionResolver.Parse("!Enable")));
                probe.Enable = false;
                Assert.IsFalse(ConditionResolver.Evaluate(probe, ConditionResolver.Parse("Enable")));
                Assert.IsTrue(ConditionResolver.Evaluate(probe, ConditionResolver.Parse("!Enable")));
            } finally {
                Object.DestroyImmediate(probe);
            }
        }

        [Test]
        public void Evaluate_IntegerEquality() {
            var probe = ScriptableObject.CreateInstance<CondProbe>();
            try {
                probe.Mode = 2;
                Assert.IsTrue(ConditionResolver.Evaluate(probe, ConditionResolver.Parse("Mode == 2")));
                Assert.IsFalse(ConditionResolver.Evaluate(probe, ConditionResolver.Parse("Mode == 3")));
            } finally {
                Object.DestroyImmediate(probe);
            }
        }

        [Test]
        public void Evaluate_EnumMemberNameEquality() {
            var probe = ScriptableObject.CreateInstance<CondProbe>();
            try {
                probe.LevelValue = CondProbe.Level.High;
                Assert.IsTrue(ConditionResolver.Evaluate(probe, ConditionResolver.Parse("LevelValue == High")));
                probe.LevelValue = CondProbe.Level.Low;
                Assert.IsFalse(ConditionResolver.Evaluate(probe, ConditionResolver.Parse("LevelValue == High")));
            } finally {
                Object.DestroyImmediate(probe);
            }
        }

        [Test]
        public void Evaluate_MethodCondition() {
            var probe = ScriptableObject.CreateInstance<CondProbe>();
            try {
                probe.LevelValue = CondProbe.Level.High;
                Assert.IsTrue(ConditionResolver.Evaluate(probe, ConditionResolver.Parse("HasTag()")));
                probe.LevelValue = CondProbe.Level.Low;
                Assert.IsFalse(ConditionResolver.Evaluate(probe, ConditionResolver.Parse("HasTag()")));
            } finally {
                Object.DestroyImmediate(probe);
            }
        }

        [Test]
        public void Evaluate_UnresolvedTarget_ReturnsNull() {
            var probe = ScriptableObject.CreateInstance<CondProbe>();
            try {
                Assert.IsNull(ConditionResolver.Evaluate(probe, ConditionResolver.Parse("NotExist")));
            } finally {
                Object.DestroyImmediate(probe);
            }
        }
    }

    /// <summary>
    /// InspectorTypeModel construction tests: ordering / grouping / condition metadata.
    /// </summary>
    [Category("Grow.Attributes")]
    public class InspectorTypeModelTests {
        private static InspectorTypeModel BuildModel() {
            InspectorTypeModel.ClearCache();
            var probe = ScriptableObject.CreateInstance<CondProbe>();
            try {
                return InspectorTypeModel.Build(new SerializedObject(probe));
            } finally {
                Object.DestroyImmediate(probe);
            }
        }

        [Test]
        public void Build_OrderingNestsPathAndInterleavesLooseField() {
            var model = BuildModel();

            // The first token should be the container group "General" (member Early order=-10,
            // containing the child group "Physics"), then OrderedFirst with PropertyOrder(-5)
            Assert.That(model.Tokens[0], Is.TypeOf<GroupToken>());
            Assert.That(model.Tokens[1], Is.TypeOf<FieldToken>());
            Assert.AreEqual("OrderedFirst", ((FieldToken)model.Tokens[1]).Field.Path);

            var general = ((GroupToken)model.Tokens[0]).Group;
            Assert.AreEqual("General", general.Title);
            Assert.AreEqual("General", general.Segment);
            Assert.AreEqual(GrowGroupKind.Foldout, general.Kind, "A container group with child groups should be a Foldout");

            // Inside "General": no direct members, only one nested child group "General/Physics" (a leaf, Box)
            var physics = general.Members.OfType<GroupToken>().Single().Group;
            Assert.AreEqual("General/Physics", physics.Title);
            Assert.AreEqual("Physics", physics.Segment);
            Assert.AreEqual(GrowGroupKind.Box, physics.Kind, "A leaf group is Box when any member declares Box");
            CollectionAssert.AreEqual(
                new[] { "Early", "Later" },
                physics.Members.OfType<FieldToken>().Select(f => f.Field.Path).ToArray(),
                "Members inside a group should be sorted by Order");
        }

        [Test]
        public void Build_ExcludesScriptAndKeepsAllVisibleFields() {
            var model = BuildModel();
            var allPaths = FlattenFieldPaths(model).ToList();

            Assert.IsFalse(allPaths.Contains("m_Script"));
            Assert.AreEqual(12, allPaths.Count, "Every serialized field of CondProbe (grouped ones included) should enter the model");
        }

        [Test]
        public void Build_CarriesShowHideClauses() {
            var model = BuildModel();
            var showOnEnable = FindField(model, "ShowOnEnable");
            Assert.AreEqual(1, showOnEnable.ShowClauses.Count);

            var hideOnEnable = FindField(model, "HideOnEnable");
            Assert.AreEqual(1, hideOnEnable.HideClauses.Count);

            var plain = FindField(model, "Enable");
            Assert.AreEqual(0, plain.ShowClauses.Count);
            Assert.AreEqual(0, plain.HideClauses.Count);
        }

        /// <summary>Flattens the token sequence into field paths (groups expanded recursively; a
        /// [GrowButton] counts as "[GrowButton]" plus the method name).</summary>
        private static IEnumerable<string> FlattenFieldPaths(InspectorTypeModel model) {
            foreach (var token in model.Tokens) {
                foreach (var path in FlattenToken(token)) {
                    yield return path;
                }
            }
        }

        private static IEnumerable<string> FlattenToken(LayoutToken token) {
            switch (token) {
                case FieldToken ft:
                    yield return ft.Field.Path;
                    break;
                case ButtonToken bt:
                    yield return "[GrowButton]" + bt.Button.Method.Name;
                    break;
                case GroupToken gt:
                    foreach (var member in gt.Group.Members) {
                        foreach (var path in FlattenToken(member)) {
                            yield return path;
                        }
                    }

                    break;
            }
        }

        [Test]
        public void Build_TooltipAttribute_IsPreservedInContent() {
            var probe = ScriptableObject.CreateInstance<TooltipProbe>();
            try {
                var model = InspectorTypeModel.Build(new SerializedObject(probe));
                var field = FindField(model, nameof(TooltipProbe.Value));
                Assert.IsNotNull(field);
                Assert.AreEqual("tooltip text", field.Content.tooltip);
            } finally {
                Object.DestroyImmediate(probe);
            }
        }

        private static FieldModel FindField(InspectorTypeModel model, string path) {
            foreach (var token in model.Tokens) {
                var found = FindInToken(token, path);
                if (found != null) {
                    return found;
                }
            }

            Assert.Fail($"Field {path} was not found in the model");
            return null;
        }

        private static FieldModel FindInToken(LayoutToken token, string path) {
            switch (token) {
                case FieldToken ft when ft.Field.Path == path:
                    return ft.Field;
                case GroupToken gt:
                    foreach (var member in gt.Group.Members) {
                        var found = FindInToken(member, path);
                        if (found != null) {
                            return found;
                        }
                    }

                    break;
            }

            return null;
        }
    }
}
