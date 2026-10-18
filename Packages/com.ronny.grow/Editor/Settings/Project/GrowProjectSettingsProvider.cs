// SPDX-License-Identifier: MIT

using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Grow.Editor.Settings.Project {
    /// <summary>
    /// Read-only <b>Project Settings &gt; Grow &gt; General</b> page.
    /// </summary>
    /// <remarks>
    /// The page deliberately exposes no project-level switch yet. A setting is added only when a
    /// feature consumes it; until then the page reports the installed package identity.
    /// </remarks>
    internal sealed class GrowProjectSettingsProvider : GrowSettingsProviderBase {
        internal const string ProviderPath = "Project/Grow/General";

        internal const string PackageName = "com.ronny.grow";

        private PackageInfo _package;

        private string _documentationPath;

        private GrowProjectSettingsProvider(string path, SettingsScope scope) : base(path, scope) {
        }

        /// <summary>Registers the page under <b>Project Settings &gt; Grow &gt; General</b>.</summary>
        [SettingsProvider]
        public static SettingsProvider Create() {
            return new GrowProjectSettingsProvider(ProviderPath, SettingsScope.Project) {
                label = "Grow",
                keywords = new[] { "grow" }
            };
        }

        /// <inheritdoc />
        public override void OnActivate(string searchContext, VisualElement rootElement) {
            _package = ResolvePackage();
            _documentationPath = ResolveDocumentationPath(_package);
        }

        /// <inheritdoc />
        protected override void DrawContent(string searchContext) {
            EditorGUILayout.LabelField("Grow", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This page reports the installed package. Project-level settings are added only " +
                "when a feature consumes them.", MessageType.Info);

            EditorGUILayout.Space(8);

            if (_package == null) {
                EditorGUILayout.HelpBox(
                    "Could not resolve the com.ronny.grow package information.",
                    MessageType.Warning);
                return;
            }

            GrowSettingsUi.DrawValue("Package", _package.name);
            GrowSettingsUi.DrawValue("Display name", _package.displayName);
            GrowSettingsUi.DrawValue("Version", _package.version);
            GrowSettingsUi.DrawValue("Documentation", _documentationPath);

            DrawDocumentationButton();

            if (!string.IsNullOrEmpty(_package.description)) {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Description", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(_package.description, EditorStyles.wordWrappedLabel);
            }
        }

        private static PackageInfo ResolvePackage() {
            var package = PackageInfo.FindForAssembly(
                typeof(GrowProjectSettingsProvider).Assembly);
            return package ?? PackageInfo.FindForPackageName(PackageName);
        }

        private static string ResolveDocumentationPath(PackageInfo package) {
            if (package == null) {
                return null;
            }

            if (!string.IsNullOrEmpty(package.documentationUrl)) {
                return package.documentationUrl;
            }

            if (string.IsNullOrEmpty(package.resolvedPath)) {
                return null;
            }

            return Path.Combine(package.resolvedPath, "Documentation~", "index.md");
        }

        private void DrawDocumentationButton() {
            if (string.IsNullOrEmpty(_documentationPath)) {
                return;
            }

            if (_documentationPath.StartsWith("http", StringComparison.OrdinalIgnoreCase)) {
                if (GUILayout.Button("Open documentation")) {
                    Help.BrowseURL(_documentationPath);
                }

                return;
            }

            if (!File.Exists(_documentationPath)) {
                return;
            }

            if (GUILayout.Button("Reveal documentation")) {
                EditorUtility.RevealInFinder(_documentationPath);
            }
        }
    }
}
