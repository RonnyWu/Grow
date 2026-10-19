// SPDX-License-Identifier: MIT

using System;
using System.Reflection;
using Grow.Editor.Inspector.Gui;
using NUnit.Framework;
using UnityEngine;

namespace Grow.Tests.Editor.Attributes {
    public enum ProbeMode {
        Auto = 0,
        Manual = 1
    }

    public sealed class ButtonSignatureProbe : ScriptableObject {
        public void Reset() {
        }

        public void AddToWatch(int delta) {
        }

        public void ApplyStats(float scale, bool clamp, string note, ProbeMode mode) {
        }

        public void Add(long value) {
        }

        public void Add(float value) {
        }
    }

    /// <summary>
    /// Tests for the pure-text ButtonGui helpers: type short names / method signatures / parameter label formatting,
    /// and that overloaded method signatures stay distinguishable.
    /// </summary>
    [Category("Grow.Attributes")]
    public class ButtonGuiTests {
        private static MethodInfo Method(string name, params Type[] parameterTypes) {
            return typeof(ButtonSignatureProbe).GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null, parameterTypes, null);
        }

        [Test]
        public void TypeShortName_MapsSupportedPrimitives() {
            Assert.AreEqual("int", ButtonGui.TypeShortName(typeof(int)));
            Assert.AreEqual("long", ButtonGui.TypeShortName(typeof(long)));
            Assert.AreEqual("float", ButtonGui.TypeShortName(typeof(float)));
            Assert.AreEqual("double", ButtonGui.TypeShortName(typeof(double)));
            Assert.AreEqual("bool", ButtonGui.TypeShortName(typeof(bool)));
            Assert.AreEqual("string", ButtonGui.TypeShortName(typeof(string)));
            Assert.AreEqual("ProbeMode", ButtonGui.TypeShortName(typeof(ProbeMode)), "An enum should return its type name rather than the fully qualified name");
            Assert.AreEqual(string.Empty, ButtonGui.TypeShortName(null));
        }

        [Test]
        public void BuildSignature_NoParams_YieldsNameAndParens() {
            Assert.AreEqual("Reset()", ButtonGui.BuildSignature(Method("Reset")));
        }

        [Test]
        public void BuildSignature_SingleAndMultipleParams_ListTypeAndName() {
            Assert.AreEqual("AddToWatch(int delta)", ButtonGui.BuildSignature(Method("AddToWatch", typeof(int))));
            Assert.AreEqual("ApplyStats(float scale, bool clamp, string note, ProbeMode mode)", ButtonGui.BuildSignature(Method("ApplyStats", typeof(float), typeof(bool), typeof(string), typeof(ProbeMode))));
            Assert.AreEqual(string.Empty, ButtonGui.BuildSignature(null));
        }

        [Test]
        public void BuildSignature_OverloadsRemainDistinguishable() {
            var longOverload = ButtonGui.BuildSignature(Method("Add", typeof(long)));
            var floatOverload = ButtonGui.BuildSignature(Method("Add", typeof(float)));
            Assert.AreEqual("Add(long value)", longOverload);
            Assert.AreEqual("Add(float value)", floatOverload);
            Assert.AreNotEqual(longOverload, floatOverload, "Overloaded signatures should include the parameter types, which is what keeps the persistence keys from colliding");
        }

        [Test]
        public void BuildParamLabel_PrefixesTypeShortName() {
            Assert.AreEqual("int delta", ButtonGui.BuildParamLabel(Method("AddToWatch", typeof(int)).GetParameters()[0]));
            Assert.AreEqual("ProbeMode mode", ButtonGui.BuildParamLabel(Method("ApplyStats", typeof(float), typeof(bool), typeof(string), typeof(ProbeMode)).GetParameters()[3]));
        }
    }
}
