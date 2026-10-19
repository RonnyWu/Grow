// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Grow.Core.Attributes;
using Grow.Editor.Inspector.Condition;
using Grow.Editor.Inspector.Data;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Grow.Tests.Editor.Inspector {
    /// <summary>
    /// Multi-target aggregation tests for <see cref="FieldConditionEvaluator"/>: visibility (ShowIf/HideIf) and
    /// editability (EnableIf/DisableIf).
    /// </summary>
    [Category("Grow.Attributes")]
    public class FieldConditionEvaluatorTests {
        private static IReadOnlyList<ConditionClause> Clause(string expr) {
            return string.IsNullOrEmpty(expr) ? Array.Empty<ConditionClause>() : new[] { ConditionResolver.Parse(expr) };
        }

        private static FieldModel Field(string show = null, string hide = null, string enable = null, string disable = null) {
            return new FieldModel("Field", "Field",
                Clause(show), Clause(hide), Clause(enable), Clause(disable),
                null, 0, 0, false, null, GrowGroupKind.Foldout);
        }

        private static CondProbe Probe(bool enable) {
            var probe = ScriptableObject.CreateInstance<CondProbe>();
            probe.Enable = enable;
            return probe;
        }

        [Test]
        public void ShouldShow_AllTargetsVisible_NotMixed() {
            var a = Probe(true);
            var b = Probe(true);
            try {
                Assert.IsTrue(FieldConditionEvaluator.ShouldShow(Field("Enable"), new object[] { a, b }, out var mixed));
                Assert.IsFalse(mixed);
            } finally {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void ShouldShow_PartiallyVisible_MixedAndShown() {
            var a = Probe(true);
            var b = Probe(false);
            try {
                Assert.IsTrue(FieldConditionEvaluator.ShouldShow(Field("Enable"), new object[] { a, b }, out var mixed));
                Assert.IsTrue(mixed, "Some targets visible should be the mixed state (shown but disabled)");
            } finally {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void ShouldShow_AllTargetsHidden_NotShown() {
            var a = Probe(false);
            var b = Probe(false);
            try {
                Assert.IsFalse(FieldConditionEvaluator.ShouldShow(Field("Enable"), new object[] { a, b }, out var mixed));
                Assert.IsFalse(mixed);
            } finally {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void ShouldShow_UnresolvableClause_TreatedAsVisible() {
            var a = Probe(false);
            try {
                Assert.IsTrue(FieldConditionEvaluator.ShouldShow(Field("MissingMember"), new object[] { a }, out var mixed));
                Assert.IsFalse(mixed);
            } finally {
                Object.DestroyImmediate(a);
            }
        }

        [Test]
        public void ShouldShow_HideClause_InvertsVisibility() {
            var a = Probe(true);
            try {
                Assert.IsFalse(FieldConditionEvaluator.ShouldShow(Field(hide: "Enable"), new object[] { a }, out _));
            } finally {
                Object.DestroyImmediate(a);
            }
        }

        [Test]
        public void ShouldEnable_RequiresAllTargetsEnabled() {
            var a = Probe(true);
            var b = Probe(true);
            var c = Probe(false);
            try {
                Assert.IsTrue(FieldConditionEvaluator.ShouldEnable(Field(enable: "Enable"), new object[] { a, b }));
                Assert.IsFalse(FieldConditionEvaluator.ShouldEnable(Field(enable: "Enable"), new object[] { a, c }), "Any disabled target dims the field as a whole");
            } finally {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
                Object.DestroyImmediate(c);
            }
        }
    }
}
