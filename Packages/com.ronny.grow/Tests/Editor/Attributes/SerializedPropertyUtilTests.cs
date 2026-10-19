// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Grow.Core.Attributes;
using Grow.Editor.Inspector;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Grow.Tests.Editor.Attributes {
    [Serializable]
    public class NestedData {
        public int Deep;
        [GrowLabel("Nested string")] public string Text;
    }

    public sealed class PathProbe : ScriptableObject {
        public int Plain;
        [GrowLabel("Renamed")] public string Renamed;
        public NestedData Nested;
        public NestedData[] Arr;
        public List<NestedData> List;
    }

    public class BaseProbe : ScriptableObject {
        [GrowLabel("Base private field")]
        [SerializeField]
        private int inherited;
    }

    public sealed class InheritedProbe : BaseProbe {
    }

    /// <summary>
    /// SerializedPropertyUtil tests for path resolution / attribute reading / display names (no GUI environment).
    /// </summary>
    [Category("Grow.Attributes")]
    public class SerializedPropertyUtilTests {
        [Test]
        public void TryGetFieldInfo_PlainField_Resolves() {
            WithProbe((probe, so) => {
                var property = so.FindProperty("Plain");
                Assert.IsTrue(SerializedPropertyUtil.TryGetFieldInfo(property, out var field));
                Assert.AreEqual("Plain", field.Name);
                Assert.AreEqual(typeof(int), field.FieldType);
            });
        }

        [Test]
        public void TryGetFieldInfo_NestedStructField_Resolves() {
            WithProbe((probe, so) => {
                var property = so.FindProperty("Nested.Deep");
                Assert.IsTrue(SerializedPropertyUtil.TryGetFieldInfo(property, out var field));
                Assert.AreEqual("Deep", field.Name);
            });
        }

        [Test]
        public void TryGetFieldInfo_ArrayElementField_Resolves() {
            WithProbe((probe, so) => {
                var property = so.FindProperty("Arr.Array.data[0].Deep");
                Assert.IsNotNull(property, "The array element path should be addressable");
                Assert.IsTrue(SerializedPropertyUtil.TryGetFieldInfo(property, out var field));
                Assert.AreEqual("Deep", field.Name);
                Assert.AreEqual(typeof(int), field.FieldType);
            }, true);
        }

        [Test]
        public void TryGetFieldInfo_ListElementField_Resolves() {
            WithProbe((probe, so) => {
                var property = so.FindProperty("List.Array.data[0].Text");
                Assert.IsNotNull(property, "The List element path should be addressable");
                Assert.IsTrue(SerializedPropertyUtil.TryGetFieldInfo(property, out var field));
                Assert.AreEqual("Text", field.Name);
                Assert.AreEqual(typeof(string), field.FieldType);
            }, withList: true);
        }

        [Test]
        public void GetLabel_PrefersLabelAttribute() {
            WithProbe((probe, so) => {
                var renamed = so.FindProperty("Renamed");
                Assert.AreEqual("Renamed", SerializedPropertyUtil.GetLabel(renamed), "[GrowLabel] should override displayName");

                var plain = so.FindProperty("Plain");
                Assert.AreEqual("Plain", SerializedPropertyUtil.GetLabel(plain), "Without [GrowLabel] it should fall back to displayName");
            });
        }

        [Test]
        public void TryGetAttribute_ReadsLabelOnField() {
            WithProbe((probe, so) => {
                var renamed = so.FindProperty("Renamed");
                Assert.IsTrue(SerializedPropertyUtil.TryGetAttribute<GrowLabelAttribute>(renamed, out var label));
                Assert.AreEqual("Renamed", label.Label);
            });
        }

        [Test]
        public void TryGetFieldInfo_InheritedPrivateField_Resolves() {
            var probe = ScriptableObject.CreateInstance<InheritedProbe>();
            try {
                var property = new SerializedObject(probe).FindProperty("inherited");
                Assert.IsTrue(SerializedPropertyUtil.TryGetFieldInfo(property, out var field));
                Assert.AreEqual("inherited", field.Name);
                Assert.AreEqual("Base private field", SerializedPropertyUtil.GetLabel(property));
            } finally {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        private static void WithProbe(Action<PathProbe, SerializedObject> body, bool withArray = false, bool withList = false) {
            var probe = ScriptableObject.CreateInstance<PathProbe>();
            try {
                if (withArray) {
                    probe.Arr = new[] { new NestedData { Deep = 1 } };
                }

                if (withList) {
                    probe.List = new List<NestedData> { new() { Text = "x" } };
                }

                var so = new SerializedObject(probe);
                body(probe, so);
            } finally {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }
    }
}
