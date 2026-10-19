// SPDX-License-Identifier: MIT

using System.Linq;
using Grow.Core.Attributes;
using Grow.Editor.Inspector.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Grow.Tests.Editor.Inspector {
    public sealed class ActionProbe : ScriptableObject {
        public bool Guard;
        public int Count;

        [GrowEnableIf("Guard")] public int OnlyWhenGuard;
        [GrowDisableIf("Guard")] public int NeverWhenGuard;

        [GrowOnValueChanged("OnWatchedChanged")] public int Watched;
        public int WatchCount;

        [GrowButton("Reset All")]
        public void Reset() {
            Count = 0;
        }

        [GrowButton]
        public void Add(int amount) {
            Count += amount;
        }

        [GrowButton]
        public void SetText(string text) {
            /* Only the parameter signature is verified here */
        }
    }

    /// <summary>
    /// Annotation metadata tests: EnableIf/DisableIf / OnValueChanged / button collection.
    /// </summary>
    [Category("Grow.Attributes")]
    public class ActionModelTests {
        private static InspectorTypeModel BuildModel() {
            InspectorTypeModel.ClearCache();
            var probe = ScriptableObject.CreateInstance<ActionProbe>();
            try {
                return InspectorTypeModel.Build(new SerializedObject(probe));
            } finally {
                Object.DestroyImmediate(probe);
            }
        }

        private static FieldModel FindField(InspectorTypeModel model, string path) {
            foreach (var token in model.Tokens) {
                if (token is FieldToken ft && ft.Field.Path == path) {
                    return ft.Field;
                }

                if (token is GroupToken gt) {
                    foreach (var member in gt.Group.Members) {
                        if (member is FieldToken mft && mft.Field.Path == path) {
                            return mft.Field;
                        }
                    }
                }
            }

            Assert.Fail($"Field {path} was not found in the model");
            return null;
        }

        [Test]
        public void Build_CarriesEnableDisableClauses() {
            var model = BuildModel();

            var onlyWhenGuard = FindField(model, "OnlyWhenGuard");
            Assert.AreEqual(1, onlyWhenGuard.EnableClauses.Count);

            var neverWhenGuard = FindField(model, "NeverWhenGuard");
            Assert.AreEqual(1, neverWhenGuard.DisableClauses.Count);
        }

        [Test]
        public void Build_CarriesOnValueChangedMethod() {
            var model = BuildModel();
            var watched = FindField(model, "Watched");
            Assert.AreEqual("OnWatchedChanged", watched.OnChangedMethod);

            var plain = FindField(model, "Count");
            Assert.AreEqual(string.Empty, plain.OnChangedMethod);
        }

        [Test]
        public void Build_CollectsButtonsSortedWithParams() {
            var model = BuildModel();

            CollectionAssert.AreEqual(
                new[] { "Add", "Reset", "SetText" },
                model.Buttons.Select(b => b.Method.Name).ToArray(),
                "[GrowButton] methods should be sorted by Order (default Max) + method name");

            Assert.AreEqual("Reset All", model.Buttons.First(b => b.Method.Name == "Reset").Label, "Button.Label should take precedence");

            var add = model.Buttons.First(b => b.Method.Name == "Add");
            Assert.AreEqual(1, add.Parameters.Length);
            Assert.AreEqual(typeof(int), add.Parameters[0].ParameterType);
        }
    }
}
