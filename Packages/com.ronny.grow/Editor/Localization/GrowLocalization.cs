// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using Grow.Editor.Settings.Preferences;
using UnityEditor;
using UnityEngine;

namespace Grow.Editor.Localization {
    [Serializable]
    internal sealed class GrowLocaleFile {
        [SerializeField]
        private int schemaVersion;

        [SerializeField]
        private string language;

        [SerializeField]
        private string languageName;

        [SerializeField]
        private GrowLocaleEntry[] entries;

        [SerializeField]
        private string[] keywords;

        public int SchemaVersion => schemaVersion;

        public string Language => language;

        public string LanguageName => languageName;

        public GrowLocaleEntry[] Entries => entries;

        public string[] Keywords => keywords;
    }

    [Serializable]
    internal struct GrowLocaleEntry {
        [SerializeField]
        private string key;

        [SerializeField]
        private string value;

        public string Key => key;

        public string Value => value;
    }

    internal readonly struct GrowLanguageInfo {
        public GrowLanguageInfo(string code, string nativeName) {
            Code = code;
            NativeName = nativeName;
        }

        public string Code { get; }

        public string NativeName { get; }
    }

    /// <summary>Loads and queries one package's locale files.</summary>
    internal sealed class GrowLocaleCatalog {
        private const string FallbackLanguage = "en";

        private readonly Dictionary<string, Dictionary<string, string>> _translations =
            new(StringComparer.Ordinal);

        private readonly List<GrowLanguageInfo> _languages = new();

        public GrowLocaleCatalog(string localeDirectory) {
            Load(localeDirectory);
        }

        public IReadOnlyList<GrowLanguageInfo> Languages => _languages;

        public bool Supports(string language) {
            return !string.IsNullOrEmpty(language) && _translations.ContainsKey(language);
        }

        public string Get(string language, string key) {
            if (string.IsNullOrEmpty(key)) {
                return key;
            }

            if (TryGet(language, key, out var value)) {
                return value;
            }

            var separator = language?.IndexOf('-') ?? -1;
            if (separator > 0 && TryGet(language.Substring(0, separator), key, out value)) {
                return value;
            }

            if (language != FallbackLanguage && TryGet(FallbackLanguage, key, out value)) {
                return value;
            }

            return key;
        }

        public string[] Keywords(IEnumerable<string> keys) {
            var keywords = new HashSet<string>(StringComparer.Ordinal) { "grow" };

            foreach (var language in _translations.Keys) {
                foreach (var key in keys) {
                    if (TryGet(language, key, out var value) && !string.IsNullOrEmpty(value)) {
                        keywords.Add(value);
                    }
                }
            }

            var result = new string[keywords.Count];
            keywords.CopyTo(result);
            return result;
        }

        private bool TryGet(string language, string key, out string value) {
            value = null;

            if (string.IsNullOrEmpty(language) ||
                !_translations.TryGetValue(language, out var entries)) {
                return false;
            }

            return entries.TryGetValue(key, out value);
        }

        private void Load(string localeDirectory) {
            if (string.IsNullOrEmpty(localeDirectory) || !Directory.Exists(localeDirectory)) {
                return;
            }

            foreach (var path in Directory.GetFiles(localeDirectory, "*.json")) {
                try {
                    var file = JsonUtility.FromJson<GrowLocaleFile>(File.ReadAllText(path));
                    if (file?.Entries == null || string.IsNullOrEmpty(file.Language)) {
                        Debug.LogWarning($"Ignoring malformed Grow locale file: {path}");
                        continue;
                    }

                    var entries = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var entry in file.Entries) {
                        if (string.IsNullOrEmpty(entry.Key)) {
                            continue;
                        }

                        if (entries.ContainsKey(entry.Key)) {
                            Debug.LogWarning(
                                $"Duplicate Grow locale key '{entry.Key}' in {path}.");
                        }

                        entries[entry.Key] = entry.Value;
                    }

                    _translations[file.Language] = entries;
                    _languages.Add(new GrowLanguageInfo(
                        file.Language,
                        string.IsNullOrEmpty(file.LanguageName)
                            ? file.Language
                            : file.LanguageName));
                } catch (Exception exception) {
                    Debug.LogWarning($"Failed to read Grow locale file '{path}': {exception.Message}");
                }
            }

            _languages.Sort(static (left, right) =>
                string.Compare(left.Code, right.Code, StringComparison.Ordinal));
        }
    }

    /// <summary>Editor-only localization facade for Grow settings pages and windows.</summary>
    internal static class GrowLocalization {
        public const string SystemLanguagePreference = "system";

        private const string LocaleDirectory = "Editor/Localization/Locales";

        private static GrowLocaleCatalog _catalog;

        private static readonly HashSet<string> MissingKeys = new(StringComparer.Ordinal);

        public static event Action LanguageChanged;

        public static IReadOnlyList<GrowLanguageInfo> Languages => Catalog.Languages;

        public static string LanguagePreference =>
            GrowEditorPreferencesStore.Instance.Language ?? SystemLanguagePreference;

        public static string CurrentLanguage => ResolveLanguage(LanguagePreference);

        public static string Get(string key) {
            if (string.IsNullOrEmpty(key)) {
                return key;
            }

            var value = Catalog.Get(CurrentLanguage, key);
            if (value == key && MissingKeys.Add(key)) {
                Debug.LogWarning($"Missing Grow localization key '{key}'.");
            }

            return value;
        }

        public static string Format(string key, params object[] args) {
            var template = Get(key);

            try {
                return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    template, args);
            } catch (FormatException) {
                Debug.LogWarning($"Invalid Grow localization template for '{key}'.");
                return template;
            }
        }

        public static GUIContent Content(string key, string tooltipKey = null) {
            return new GUIContent(
                Get(key), string.IsNullOrEmpty(tooltipKey) ? null : Get(tooltipKey));
        }

        public static string[] Keywords(params string[] keys) {
            return Catalog.Keywords(keys);
        }

        public static void SetLanguage(string preference) {
            if (!GrowEditorPreferencesStore.Store.CanSave) {
                return;
            }

            if (preference != SystemLanguagePreference && !Catalog.Supports(preference)) {
                preference = "en";
            }

            var settings = GrowEditorPreferencesStore.Instance;
            if (settings.Language == preference) {
                return;
            }

            settings.Language = preference;
            GrowEditorPreferencesStore.Store.MarkDirty();
            Reload();
            LanguageChanged?.Invoke();
            RefreshSettingsWindows();
        }

        public static void Reload() {
            _catalog = null;
            MissingKeys.Clear();
        }

        internal static string ResolveLanguage(string preference) {
            if (string.IsNullOrEmpty(preference) ||
                preference == SystemLanguagePreference) {
                return ResolveSystemLanguage(Application.systemLanguage);
            }

            return Catalog.Supports(preference) ? preference : "en";
        }

        internal static string ResolveSystemLanguage(SystemLanguage language) {
            return language switch {
                SystemLanguage.ChineseSimplified => "zh-Hans",
                SystemLanguage.ChineseTraditional => "zh-Hans",
                _ => "en"
            };
        }

        private static GrowLocaleCatalog Catalog {
            get {
                if (_catalog != null) {
                    return _catalog;
                }

                var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                    typeof(GrowLocalization).Assembly);
                var directory = package == null || string.IsNullOrEmpty(package.resolvedPath)
                    ? null
                    : Path.Combine(package.resolvedPath, LocaleDirectory);

                _catalog = new GrowLocaleCatalog(directory);
                return _catalog;
            }
        }

        private static void RefreshSettingsWindows() {
            EditorApplication.delayCall += () => {
                SettingsService.NotifySettingsProviderChanged();
                SettingsService.RepaintAllSettingsWindow();
            };
        }
    }
}
