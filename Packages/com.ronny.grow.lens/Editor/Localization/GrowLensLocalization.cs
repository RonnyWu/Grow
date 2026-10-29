// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using Grow.Editor.Localization;
using UnityEngine;

namespace Grow.Lens.Editor.Localization {
    [Serializable]
    internal sealed class GrowLensLocaleFile {
        [SerializeField]
        private string language;

        [SerializeField]
        private GrowLensLocaleEntry[] entries;

        public string Language => language;

        public GrowLensLocaleEntry[] Entries => entries;
    }

    [Serializable]
    internal struct GrowLensLocaleEntry {
        [SerializeField]
        private string key;

        [SerializeField]
        private string value;

        public string Key => key;

        public string Value => value;
    }

    /// <summary>
    /// Editor-only translations for Grow Lens. The language is resolved by Grow's shared
    /// localization service, so changing <b>Preferences &gt; Grow &gt; General</b> updates Lens
    /// without a restart; Lens keeps its own strings in its package locale files.
    /// </summary>
    internal static class GrowLensLocalization {
        private const string FallbackLanguage = "en";

        private const string LocaleDirectory = "Editor/Localization/Locales";

        private static readonly Dictionary<string, Dictionary<string, string>> Translations =
            new(StringComparer.Ordinal);

        private static bool _catalogLoaded;

        private static bool _missingCatalogWarning;

        /// <summary>The resolved language code, one of the bundled locale files.</summary>
        internal static string CurrentLanguage {
            get {
                EnsureCatalog();
                var language = GrowLocalization.CurrentLanguage;
                return Translations.ContainsKey(language) ? language : FallbackLanguage;
            }
        }

        /// <summary>Returns a translated value, then the English value, then the key itself.</summary>
        internal static string Get(string key) {
            if (string.IsNullOrEmpty(key)) {
                return key;
            }

            var language = CurrentLanguage;
            if (TryGet(language, key, out var value)) {
                return value;
            }

            if (language != FallbackLanguage && TryGet(FallbackLanguage, key, out value)) {
                return value;
            }

            return key;
        }

        /// <summary>Formats a translated template with invariant culture.</summary>
        internal static string Format(string key, params object[] args) {
            var template = Get(key);

            try {
                return string.Format(System.Globalization.CultureInfo.InvariantCulture, template,
                    args);
            } catch (FormatException) {
                return template;
            }
        }

        /// <summary>Clears the Lens locale catalog; tests only.</summary>
        internal static void ResetForTests() {
            Translations.Clear();
            _catalogLoaded = false;
            _missingCatalogWarning = false;
        }

        private static void EnsureCatalog() {
            if (_catalogLoaded) {
                return;
            }

            _catalogLoaded = true;
            var directory = LocaleDirectoryPath;
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) {
                WarnMissingCatalog(directory);
                return;
            }

            foreach (var path in Directory.GetFiles(directory, "*.json")) {
                try {
                    var file = JsonUtility.FromJson<GrowLensLocaleFile>(File.ReadAllText(path));
                    if (file?.Entries == null || string.IsNullOrEmpty(file.Language)) {
                        Debug.LogWarning($"[Grow Lens] Ignoring malformed locale file: {path}");
                        continue;
                    }

                    var entries = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var entry in file.Entries) {
                        if (!string.IsNullOrEmpty(entry.Key)) {
                            entries[entry.Key] = entry.Value;
                        }
                    }

                    Translations[file.Language] = entries;
                } catch (Exception exception) {
                    Debug.LogWarning(
                        $"[Grow Lens] Could not read locale file '{path}': {exception.Message}");
                }
            }

            if (Translations.Count == 0) {
                WarnMissingCatalog(directory);
            }
        }

        private static string LocaleDirectoryPath {
            get {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                    typeof(GrowLensLocalization).Assembly);
                return package == null || string.IsNullOrEmpty(package.resolvedPath)
                    ? null
                    : Path.Combine(package.resolvedPath, LocaleDirectory);
            }
        }

        private static void WarnMissingCatalog(string directory) {
            if (_missingCatalogWarning) {
                return;
            }

            _missingCatalogWarning = true;
            Debug.LogWarning(
                $"[Grow Lens] Could not load locale files from '{directory ?? "<unresolved>"}'.");
        }

        private static bool TryGet(string language, string key, out string value) {
            value = null;
            return !string.IsNullOrEmpty(language) &&
                Translations.TryGetValue(language, out var entries) &&
                entries.TryGetValue(key, out value);
        }
    }
}
