// SPDX-License-Identifier: MIT

using System.Linq;
using Grow.Editor.Common.MultiSelect;
using Grow.Editor.Inspector.EnumFlags;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Grow.Tests.Editor.Attributes {
    /// <summary>
    /// Behaviour tests for combination alias rows, the dropdown label (alias hit / Mixed…) and the multi-target
    /// mixed state (IsMixed).
    /// </summary>
    [Category("Grow.Attributes")]
    public class EnumFlagsDisplayTests {
        private sealed class Holder : ScriptableObject {
            public SampleFlags Value;
        }

        private static (IMultiSelectBackend backend, MultiSelectData cache) Create(SampleFlags value, Holder holder) {
            holder.Value = value;
            var property = new SerializedObject(holder).FindProperty("Value");
            return (EnumFlagsGui.CreateBackend(property), EnumFlagsCache.GetOrCreateFlagsCache(typeof(SampleFlags)));
        }

        private static T Run<T>(SampleFlags value, System.Func<IMultiSelectBackend, MultiSelectData, T> body) {
            var holder = ScriptableObject.CreateInstance<Holder>();
            try {
                var (backend, cache) = Create(value, holder);
                return body(backend, cache);
            } finally {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void BuildFlagsCache_CombosIncludeCompositeExcludeEverything() {
            var cache = EnumFlagsCache.BuildFlagsCache(typeof(SampleFlags));

            CollectionAssert.AreEqual(new[] { 3 }, cache.ComboValues, "AB = 3 should appear as a combination alias row (All = 7 carries Everything semantics and is excluded)");
            CollectionAssert.AreEqual(new[] { 1, 2, 4 }, cache.Values, "Single-bit leaves are unchanged");
            StringAssert.StartsWith("3 : AB : AB", cache.ComboLabels[0]);
        }

        [Test]
        public void BuildFlagsCache_CombosDisabled_Empty() {
            var cache = EnumFlagsCache.BuildFlagsCache(typeof(SampleFlags), false);
            Assert.IsEmpty(cache.ComboValues);
        }

        [Test]
        public void BuildFlagsCache_NoDefinedCombos_Empty() {
            var cache = EnumFlagsCache.BuildFlagsCache(typeof(ObsoleteFlags));
            Assert.IsEmpty(cache.ComboValues, "Without multi-bit members there should be no combination rows");
        }

        [Test]
        public void DisplayLabel_SingleMatch_ShowsName() {
            Run(SampleFlags.A, (backend, cache) => {
                Assert.AreEqual("A", HierarchicalMultiSelectGui.GetDisplayLabel(backend, cache));
                return true;
            });
        }

        [Test]
        public void DisplayLabel_CompositeAliasMatch_ShowsAliasName() {
            Run(SampleFlags.A | SampleFlags.B, (backend, cache) => {
                Assert.AreEqual("AB", HierarchicalMultiSelectGui.GetDisplayLabel(backend, cache), "When the selected set ORs to the defined combination AB it should show the alias");
                return true;
            });
        }

        [Test]
        public void DisplayLabel_NoAliasMatch_ShowsMixed() {
            Run(SampleFlags.A | SampleFlags.C, (backend, cache) => {
                Assert.AreEqual("Mixed…", HierarchicalMultiSelectGui.GetDisplayLabel(backend, cache), "When the selected set ORs to 5 with no defined member it should show Mixed…");
                return true;
            });
        }

        [Test]
        public void DisplayLabel_NothingAndEverything() {
            Run(SampleFlags.None, (backend, cache) => {
                Assert.AreEqual("None", HierarchicalMultiSelectGui.GetDisplayLabel(backend, cache));
                return true;
            });
            Run(SampleFlags.All, (backend, cache) => {
                Assert.AreEqual("All", HierarchicalMultiSelectGui.GetDisplayLabel(backend, cache));
                return true;
            });
        }

        [Test]
        public void Backend_MixedMultiObject_SharedBitIsCheckedMixedBitIsNot() {
            var h1 = ScriptableObject.CreateInstance<Holder>();
            var h2 = ScriptableObject.CreateInstance<Holder>();
            var editor = UnityEditor.Editor.CreateEditor(new Object[] { h1, h2 });
            try {
                h1.Value = SampleFlags.A;
                h2.Value = SampleFlags.A | SampleFlags.B;
                editor.serializedObject.Update();
                var property = editor.serializedObject.FindProperty("Value");

                var backend = EnumFlagsGui.CreateBackend(property);
                Assert.IsTrue(backend.IsMixed, "Differing values across objects should be marked as the mixed state");
                Assert.IsTrue(backend.Contains(1), "A bit present on every target should show as checked");
                Assert.IsFalse(backend.IsMixedAt(1), "A shared bit is not a mixed bit");
                Assert.IsFalse(backend.Contains(2), "A bit present on only some targets should not show as checked");
                Assert.IsTrue(backend.IsMixedAt(2), "A bit present on only some targets should show as a mixed bit");

                // In the mixed state writes are absolute assignments (matching native popup
                // semantics) applied to every target
                backend.Clear();
                backend.Apply();
                editor.serializedObject.Update();
                Assert.AreEqual(SampleFlags.None, h1.Value);
                Assert.AreEqual(SampleFlags.None, h2.Value);
            } finally {
                if (editor != null) {
                    Object.DestroyImmediate(editor);
                }

                Object.DestroyImmediate(h1);
                Object.DestroyImmediate(h2);
            }
        }

        [Test]
        public void Backend_MixedMultiObject_CommonBit_CanBeCleared() {
            var first = ScriptableObject.CreateInstance<Holder>();
            var second = ScriptableObject.CreateInstance<Holder>();
            first.Value = SampleFlags.A;
            second.Value = SampleFlags.A | SampleFlags.B;
            try {
                var serialized = new SerializedObject(new Object[] { first, second });
                var backend = new EnumFlagsBackend(serialized.FindProperty("Value"));
                backend.SetValue((int)SampleFlags.A, false);
                backend.Apply();

                Assert.AreEqual(SampleFlags.None, first.Value);
                Assert.AreEqual(SampleFlags.B, second.Value);
            } finally {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void MultiSelectTreeView_SearchKeepsGroupsAndHidesSpecialRows() {
            var cache = new MultiSelectData {
                Values = new[] { 1, 2, 4 },
                Labels = new[] {
                    "1 : A : Group/Alpha",
                    "2 : B : Group/Beta",
                    "4 : C : Other/Gamma"
                },
                NothingLabel = "Nothing",
                EverythingLabel = "Everything"
            };
            var view = new MultiSelectTreeView(new TreeViewState(), new EnumValueBackend(SampleFlags.None), cache);
            view.Reload();
            view.ExpandAll();
            view.SetSearch("Alpha");

            var rows = view.GetRows().Cast<TreeViewItem>().Select(row => row.displayName).ToList();
            CollectionAssert.Contains(rows, "Group", "the matching leaf's parent group must remain visible");
            CollectionAssert.Contains(rows, "Alpha");
            CollectionAssert.DoesNotContain(rows, "Beta");
            CollectionAssert.DoesNotContain(rows, "Other");
            CollectionAssert.DoesNotContain(rows, "Nothing", "special rows are not search results");
            CollectionAssert.DoesNotContain(rows, "Everything", "special rows are not search results");
        }

        [Test]
        public void Backend_ThreeTargets_CachePreservesMixedStateAndWrites() {
            var first = ScriptableObject.CreateInstance<Holder>();
            var second = ScriptableObject.CreateInstance<Holder>();
            var third = ScriptableObject.CreateInstance<Holder>();
            first.Value = SampleFlags.A | SampleFlags.B;
            second.Value = SampleFlags.A | SampleFlags.B | SampleFlags.C;
            third.Value = SampleFlags.B | SampleFlags.C;
            var serialized = new SerializedObject(new Object[] { first, second, third });
            var backend = new EnumFlagsBackend(serialized.FindProperty("Value"));
            try {
                Assert.IsTrue(backend.Contains((int)SampleFlags.B), "all targets share B");
                Assert.IsFalse(backend.Contains((int)SampleFlags.A), "A is present on only two targets");
                Assert.IsTrue(backend.IsMixedAt((int)SampleFlags.A));
                Assert.IsFalse(backend.Contains((int)SampleFlags.C), "C is present on only two targets");
                Assert.IsTrue(backend.IsMixedAt((int)SampleFlags.C));

                backend.SetValue((int)SampleFlags.C, true);
                backend.Apply();

                Assert.AreEqual(SampleFlags.A | SampleFlags.B | SampleFlags.C, first.Value);
                Assert.AreEqual(SampleFlags.A | SampleFlags.B | SampleFlags.C, second.Value);
                Assert.AreEqual(SampleFlags.B | SampleFlags.C, third.Value);
            } finally {
                backend.Dispose();
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(third);
            }
        }

        [Test]
        public void Backend_SingleObject_NotMixed() {
            Run(SampleFlags.A | SampleFlags.B, (backend, _) => {
                Assert.IsFalse(backend.IsMixed);
                return true;
            });
        }

        [Test]
        public void ValueBackend_RoundTripsMask() {
            var b = new EnumValueBackend(SampleFlags.A | SampleFlags.B);
            Assert.IsTrue(b.Contains(1));
            Assert.IsTrue(b.Contains(2));
            Assert.IsFalse(b.Contains(4));
            Assert.IsFalse(b.IsMixed);

            b.SetValue(4, true);
            Assert.AreEqual(7, b.Mask);
            b.SetValue(4, false);
            Assert.AreEqual(3, b.Mask);

            b.Clear();
            Assert.AreEqual(0, b.Mask);

            b.SelectAll(new[] { 1, 2, 4 });
            Assert.AreEqual(7, b.Mask);
        }

        [Test]
        public void ValueBackend_DisplayLabel_MatchesPropertyBackend() {
            // The value backend used for [GrowButton] method parameters should produce exactly the
            // same label and rules as the field's SerializedProperty backend
            var vb = new EnumValueBackend(SampleFlags.A | SampleFlags.B);
            var cache = EnumFlagsCache.GetOrCreateFlagsCache(typeof(SampleFlags));
            Assert.AreEqual("AB", HierarchicalMultiSelectGui.GetDisplayLabel(vb, cache));

            var holder = ScriptableObject.CreateInstance<Holder>();
            try {
                holder.Value = SampleFlags.A | SampleFlags.B;
                var pb = EnumFlagsGui.CreateBackend(new SerializedObject(holder).FindProperty("Value"));
                Assert.AreEqual(HierarchicalMultiSelectGui.GetDisplayLabel(pb, cache), HierarchicalMultiSelectGui.GetDisplayLabel(vb, cache));
            } finally {
                Object.DestroyImmediate(holder);
            }
        }
        [Test]
        public void Backend_MultiObject_SetValue_PreservesOtherTargetsBits() {
            var first = ScriptableObject.CreateInstance<Holder>();
            var second = ScriptableObject.CreateInstance<Holder>();
            first.Value = SampleFlags.A;
            second.Value = SampleFlags.A | SampleFlags.B;
            try {
                var serialized = new SerializedObject(new Object[] { first, second });
                var backend = new EnumFlagsBackend(serialized.FindProperty("Value"));
                backend.SetValue((int)SampleFlags.C, true);
                backend.Apply();

                Assert.AreEqual(SampleFlags.A | SampleFlags.C, first.Value);
                Assert.AreEqual(SampleFlags.A | SampleFlags.B | SampleFlags.C, second.Value,
                    "toggling a bit must preserve the other target's bits");
            } finally {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }
    }
}
