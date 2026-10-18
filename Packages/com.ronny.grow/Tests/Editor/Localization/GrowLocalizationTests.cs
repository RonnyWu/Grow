// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Linq;
using Grow.Editor.Localization;
using NUnit.Framework;
using UnityEngine;

namespace Grow.Tests.Editor.Localization {
    public class GrowLocalizationTests {
        private string _root;

        [SetUp]
        public void SetUp() {
            _root = Path.Combine(
                Path.GetTempPath(), "GrowLocaleTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);

            File.WriteAllText(Path.Combine(_root, "en.json"),
                "{\"schemaVersion\":1,\"language\":\"en\",\"languageName\":\"English\"," +
                "\"entries\":[" +
                "{\"key\":\"grow.test.hello\",\"value\":\"Hello\"}," +
                "{\"key\":\"grow.test.englishOnly\",\"value\":\"English only\"}" +
                "]}");
            File.WriteAllText(Path.Combine(_root, "zh-Hans.json"),
                "{\"schemaVersion\":1,\"language\":\"zh-Hans\",\"languageName\":\"简体中文\"," +
                "\"entries\":[" +
                "{\"key\":\"grow.test.hello\",\"value\":\"你好\"}" +
                "]}");
        }

        [TearDown]
        public void TearDown() {
            if (Directory.Exists(_root)) {
                Directory.Delete(_root, true);
            }
        }

        [Test]
        public void Catalog_LoadsLanguagesAndEntries() {
            var catalog = new GrowLocaleCatalog(_root);

            Assert.AreEqual(2, catalog.Languages.Count);
            Assert.AreEqual("你好", catalog.Get("zh-Hans", "grow.test.hello"));
            Assert.AreEqual("Hello", catalog.Get("en", "grow.test.hello"));
        }

        [Test]
        public void Catalog_FallsBackToEnglishThenKey() {
            var catalog = new GrowLocaleCatalog(_root);

            Assert.AreEqual("English only", catalog.Get("zh-Hans", "grow.test.englishOnly"));
            Assert.AreEqual("grow.test.missing", catalog.Get("zh-Hans", "grow.test.missing"));
        }

        [Test]
        public void Catalog_KeywordsCoverAllLanguages() {
            var catalog = new GrowLocaleCatalog(_root);

            var keywords = catalog.Keywords(new[] { "grow.test.hello" });

            CollectionAssert.Contains(keywords, "grow");
            CollectionAssert.Contains(keywords, "Hello");
            CollectionAssert.Contains(keywords, "你好");
        }

        [Test]
        public void ResolveSystemLanguage_MapsOnlySupportedEditorLanguages() {
            Assert.AreEqual("zh-Hans",
                GrowLocalization.ResolveSystemLanguage(SystemLanguage.ChineseSimplified));
            Assert.AreEqual("en", GrowLocalization.ResolveSystemLanguage(SystemLanguage.English));

            // These are sample fixtures, not supported editor languages.
            Assert.AreEqual("en", GrowLocalization.ResolveSystemLanguage(SystemLanguage.Arabic));
            Assert.AreEqual("en", GrowLocalization.ResolveSystemLanguage(SystemLanguage.French));
            Assert.AreEqual("en", GrowLocalization.ResolveSystemLanguage(SystemLanguage.Russian));
            Assert.AreEqual("en", GrowLocalization.ResolveSystemLanguage(SystemLanguage.Spanish));
        }

        [Test]
        public void PackageLocaleFiles_CoverSupportedEditorLanguages() {
            var languages = GrowLocalization.Languages;
            var codes = languages.Select(language => language.Code).ToArray();

            CollectionAssert.Contains(codes, "en");
            CollectionAssert.Contains(codes, "zh-Hans");

            foreach (var sampleOnly in new[] { "ar", "es", "fr", "ru" }) {
                CollectionAssert.DoesNotContain(codes, sampleOnly);
            }
        }

        [Test]
        public void TestFixtures_LoadSampleLanguages() {
            var fixtures = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..",
                "Packages/com.ronny.grow/Tests/Editor/Localization/Fixtures/Locales"));
            var catalog = new GrowLocaleCatalog(fixtures);

            foreach (var code in new[] { "fr", "ru", "es" }) {
                Assert.IsTrue(catalog.Supports(code), $"Missing test locale '{code}'.");
                Assert.AreNotEqual(
                    "grow.settings.project.general.title",
                    catalog.Get(code, "grow.settings.project.general.title"));
            }

            Assert.IsFalse(catalog.Supports("ar"));
        }
    }
}
