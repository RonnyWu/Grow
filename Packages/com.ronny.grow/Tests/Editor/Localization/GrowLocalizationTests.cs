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
        public void ResolveSystemLanguage_MapsUnOfficialLanguages() {
            Assert.AreEqual("ar", GrowLocalization.ResolveSystemLanguage(SystemLanguage.Arabic));
            Assert.AreEqual("zh-Hans",
                GrowLocalization.ResolveSystemLanguage(SystemLanguage.ChineseSimplified));
            Assert.AreEqual("en", GrowLocalization.ResolveSystemLanguage(SystemLanguage.English));
            Assert.AreEqual("fr", GrowLocalization.ResolveSystemLanguage(SystemLanguage.French));
            Assert.AreEqual("ru", GrowLocalization.ResolveSystemLanguage(SystemLanguage.Russian));
            Assert.AreEqual("es", GrowLocalization.ResolveSystemLanguage(SystemLanguage.Spanish));
        }

        [Test]
        public void PackageLocaleFiles_CoverTheSixUnOfficialLanguages() {
            var languages = GrowLocalization.Languages;

            foreach (var code in new[] { "ar", "en", "es", "fr", "ru", "zh-Hans" }) {
                Assert.IsTrue(
                    languages.Any(language => language.Code == code),
                    $"Missing locale '{code}'.");
            }
        }
    }
}
