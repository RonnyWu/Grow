// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Text.RegularExpressions;
using Grow.Lens.Editor.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Grow.Lens.Tests.Editor.Core {
    /// <summary>Tests for the per-project settings file.</summary>
    public class GrowLensSettingsTests {
        private string _directory;
        private string _path;

        [SetUp]
        public void SetUp() {
            _directory = Path.Combine(Path.GetTempPath(), "GrowLensTests", Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_directory, "Lens.json");
            GrowLensSettings.SetPathForTests(_path);
        }

        [TearDown]
        public void TearDown() {
            GrowLensSettings.Enabled = false;
            GrowLensSettings.ResetPathForTests();
            GrowLensHost.ResetForTests();
            if (Directory.Exists(_directory)) {
                Directory.Delete(_directory, true);
            }
        }

        [Test]
        public void MissingFile_DefaultsToDisabled() {
            Assert.IsFalse(GrowLensSettings.Enabled);
        }

        [Test]
        public void CorruptFile_DefaultsToDisabled() {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_path, "{ not json");
            LogAssert.Expect(LogType.Warning, new Regex("Could not read"));
            GrowLensSettings.ReloadForTests();
            Assert.IsFalse(GrowLensSettings.Enabled);
        }

        [Test]
        public void Enabled_RoundTripsThroughJson() {
            GrowLensSettings.Enabled = true;
            GrowLensSettings.ReloadForTests();
            Assert.IsTrue(GrowLensSettings.Enabled);

            GrowLensSettings.Enabled = false;
            GrowLensSettings.ReloadForTests();
            Assert.IsFalse(GrowLensSettings.Enabled);
        }
    }
}
