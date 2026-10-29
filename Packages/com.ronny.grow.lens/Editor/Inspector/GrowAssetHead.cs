// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Grow.Lens.Editor.Core;
using Grow.Lens.Editor.Localization;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Grow.Lens.Editor.Inspector {
    /// <summary>
    /// Adds asset actions to the Inspector header: copy the full/asset path or GUID and open the
    /// file or its <c>.meta</c> in Rider. Rider is resolved by reflection and the row is hidden
    /// when the package is not installed.
    /// </summary>
    internal static class GrowAssetHead {
        private const BindingFlags RiderFlags = BindingFlags.Public | BindingFlags.Static;

        private static readonly MethodInfo SyncIfNeededMethod;
        private static readonly MethodInfo OpenFileMethod;

        /// <summary>
        /// Types whose importer already draws a custom header. Keeping them out avoids duplicate
        /// header rows; assignable types are excluded too, so subclasses of <see cref="TextAsset"/>
        /// do not draw a second header.
        /// </summary>
        private static readonly Type[] IgnoreTargetTypes = {
            typeof(AudioClip), typeof(ComputeShader), typeof(Font), typeof(GameObject), typeof(MonoScript),
            typeof(Shader), typeof(TextAsset)
        };

        static GrowAssetHead() {
            try {
                var riderScriptEditorType = Type.GetType("Packages.Rider.Editor.RiderScriptEditor, Unity.Rider.Editor");
                SyncIfNeededMethod = riderScriptEditorType?.GetMethod("SyncIfNeeded", RiderFlags);
                if (SyncIfNeededMethod == null) {
                    return;
                }

                var editorPluginInteropType =
                    Type.GetType("Packages.Rider.Editor.EditorPluginInterop, Unity.Rider.Editor");
                OpenFileMethod = editorPluginInteropType?.GetMethod("OpenFileDllImplementation", RiderFlags);
            } catch (Exception exception) {
                Debug.LogWarning($"[Grow Lens] Could not resolve the Rider integration: {exception.Message}");
            }
        }

        internal static void Subscribe() {
            UnityEditor.Editor.finishedDefaultHeaderGUI -= OnPostHeaderGUI;
            UnityEditor.Editor.finishedDefaultHeaderGUI += OnPostHeaderGUI;
        }

        internal static void Unsubscribe() {
            UnityEditor.Editor.finishedDefaultHeaderGUI -= OnPostHeaderGUI;
        }

        private static void OnPostHeaderGUI(UnityEditor.Editor editor) {
            GrowLensGuard.Run("Asset header", () => DrawHeader(editor));
        }

        private static void DrawHeader(UnityEditor.Editor editor) {
            if (editor.targets == null || editor.targets.Length > 1) {
                return;
            }

            var target = editor.targets.First();
            if (target == null) {
                return;
            }

            if (IgnoreTargetTypes.Any(type => type.IsAssignableFrom(target.GetType()))) {
                return;
            }

            var assetPath = AssetDatabase.GetAssetPath(target);
            if (string.IsNullOrEmpty(assetPath)) {
                return;
            }

            var projectRoot = Path.GetDirectoryName(Application.dataPath) ?? ".";
            var fullPath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
            if (string.IsNullOrEmpty(fullPath)) {
                return;
            }

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid)) {
                return;
            }

            DrawCustomHeader(target, fullPath, assetPath, guid);
        }

        private static void DrawCustomHeader(Object target, string fullPath, string assetPath, string guid) {
            var buttonWidth = GUILayout.Width(110);
            var halfButtonWidth = GUILayout.Width(54f);
            var buttonStyle = EditorStyles.miniButton;

            DrawSplitLine();
            GUILayout.Space(6);

            DrawRow(fullPath, () => {
                var content = new GUIContent(Text("grow.lens.asset.copyFullPath"), fullPath);
                if (GUILayout.Button(content, buttonStyle, buttonWidth)) {
                    EditorGUIUtility.systemCopyBuffer = fullPath;
                }
            });

            DrawRow(assetPath, () => {
                var content = new GUIContent(Text("grow.lens.asset.copyAssetPath"), assetPath);
                if (GUILayout.Button(content, buttonStyle, buttonWidth)) {
                    EditorGUIUtility.systemCopyBuffer = assetPath;
                }
            });

            DrawRow(guid, () => {
                var content = new GUIContent(Text("grow.lens.asset.copyGuid"), guid);
                if (GUILayout.Button(content, buttonStyle, buttonWidth)) {
                    EditorGUIUtility.systemCopyBuffer = guid;
                }
            });

            if (SyncIfNeededMethod == null || OpenFileMethod == null) {
                return;
            }

            DrawRow(Text("grow.lens.asset.openInRider"), () => {
                if (GUILayout.Button(Text("grow.lens.asset.meta"), buttonStyle, halfButtonWidth)) {
                    SyncIfNeededMethod.Invoke(null, new object[] { false });
                    OpenFileMethod.Invoke(null, new object[] { $"{fullPath}.meta", -1, -1 });
                }

                using (new EditorGUI.DisabledScope(target is DefaultAsset)) {
                    if (GUILayout.Button(Text("grow.lens.asset.file"), buttonStyle, halfButtonWidth)) {
                        SyncIfNeededMethod.Invoke(null, new object[] { false });
                        OpenFileMethod.Invoke(null, new object[] { fullPath, -1, -1 });
                    }
                }
            });
        }

        private static void DrawSplitLine() {
            var lineRect = EditorGUILayout.GetControlRect(false, 1);
            lineRect.x -= 3;
            lineRect.width += 6;
            EditorGUI.DrawRect(lineRect, new Color32(127, 127, 127, 255));
        }

        private static void DrawRow(string label, Action action) {
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(true)) {
                var maxWidth = Mathf.Max(60f, EditorGUIUtility.currentViewWidth - 136f);
                GUILayout.Label(label, GUILayout.MaxWidth(maxWidth));
            }

            GUILayout.FlexibleSpace();
            action?.Invoke();
            EditorGUILayout.EndHorizontal();
        }

        private static string Text(string key) {
            return GrowLensLocalization.Get(key);
        }
    }
}
