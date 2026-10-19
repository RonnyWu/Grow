// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Grow.Lens.Editor.Menus {
    /// <summary>Commands used by the Project and Hierarchy context menus.</summary>
    internal static class GrowMenuActions {
        internal static void CopyName(Object target) {
            if (target == null) {
                return;
            }

            EditorGUIUtility.systemCopyBuffer = target.name;
        }

        internal static void CopyPath(Object target) {
            if (target == null) {
                return;
            }

            var path = AssetDatabase.GetAssetPath(target);
            if (string.IsNullOrEmpty(path) && target is GameObject gameObject) {
                path = GetHierarchyPath(gameObject);
            }

            EditorGUIUtility.systemCopyBuffer = path ?? string.Empty;
        }

        internal static void CopyGuid(Object target) {
            if (target == null || !TryGetGuidInfo(target, out var guid, out _)) {
                return;
            }

            EditorGUIUtility.systemCopyBuffer = guid;
        }

        internal static void RevealInFinder(Object target) {
            if (target == null) {
                return;
            }

            var path = AssetDatabase.GetAssetPath(target);
            if (!string.IsNullOrEmpty(path)) {
                EditorUtility.RevealInFinder(path);
            }
        }

        internal static void CreateFolder() {
            ProjectWindowUtil.CreateFolder();
        }

        internal static void ExecuteMenuItem(string menuPath) {
            if (!string.IsNullOrEmpty(menuPath)) {
                EditorApplication.ExecuteMenuItem(menuPath);
            }
        }

        /// <summary>Creates an empty object through Unity's menu and resets its local transform.</summary>
        internal static void CreateEmpty(string menuPath) {
            var previous = Selection.activeGameObject;
            EditorApplication.ExecuteMenuItem(menuPath);
            EditorApplication.delayCall += () => {
                var created = Selection.activeGameObject;
                if (created == null || created == previous) {
                    return;
                }

                var transform = created.transform;
                Undo.RecordObject(transform, "Reset Transform");
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                transform.localScale = Vector3.one;
                EditorUtility.SetDirty(created);
            };
        }

        internal static void DeleteSelection() {
            EditorApplication.ExecuteMenuItem("Edit/Delete");
        }

        /// <summary>Returns the project-relative asset path or a root-to-leaf Hierarchy path.</summary>
        internal static string GetHierarchyPath(GameObject gameObject) {
            if (gameObject == null) {
                return string.Empty;
            }

            var segments = new Stack<string>();
            for (var current = gameObject.transform; current != null; current = current.parent) {
                segments.Push(current.name);
            }

            var builder = new StringBuilder();
            while (segments.Count > 0) {
                if (builder.Length > 0) {
                    builder.Append('/');
                }

                builder.Append(segments.Pop());
            }

            return builder.ToString();
        }

        /// <summary>
        /// Resolves a GUID for a main asset, a prefab instance or a sub-asset. Returns false when
        /// the object has no asset identity.
        /// </summary>
        internal static bool TryGetGuidInfo(Object target, out string guid, out string assetPath) {
            guid = null;
            assetPath = null;

            if (target == null) {
                return false;
            }

            assetPath = AssetDatabase.GetAssetPath(target);
            guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (!string.IsNullOrEmpty(assetPath) && !string.IsNullOrEmpty(guid)) {
                return true;
            }

            if (target is GameObject gameObject) {
                assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
                guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (!string.IsNullOrEmpty(assetPath) && !string.IsNullOrEmpty(guid)) {
                    return true;
                }
            }

            if (string.IsNullOrEmpty(assetPath)) {
                return false;
            }

            var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (mainAsset == null || mainAsset == target) {
                return false;
            }

            assetPath = AssetDatabase.GetAssetPath(mainAsset);
            guid = AssetDatabase.AssetPathToGUID(assetPath);
            return !string.IsNullOrEmpty(assetPath) && !string.IsNullOrEmpty(guid);
        }
    }
}
