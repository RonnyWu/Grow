// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Grow.Editor.Inspector.Workflow {
    /// <summary>
    /// Reusable workflow base for Grow custom editors: it owns the draw pipeline, change tracking,
    /// lifecycle hooks and an explicit <see cref="SaveAll"/> path. It does not draw any content.
    /// </summary>
    /// <remarks>
    /// Subclasses implement <see cref="OnDraw"/> and get serialized-object updates, wide-mode
    /// scoping, change detection (including "newly dirty this frame"), <see cref="OnChanged"/>,
    /// lifecycle hooks and manual saving of assets, scenes and prefab instances.
    /// <para>
    /// Automatic background saving is intentionally not part of the base class: a scheduler that
    /// writes assets/scenes from <c>EditorApplication.update</c> needs its own instance-state and
    /// tests before it can be shipped. Use <see cref="SaveAll"/> from an explicit action instead.
    /// </para>
    /// <para>The annotation-driven editor is <c>GrowEditor</c>; fully custom editors inherit this
    /// class directly.</para>
    /// </remarks>
    [CanEditMultipleObjects]
    public abstract class GrowEditorBase : UnityEditor.Editor {
        /// <summary>Whether to save the scenes that contain the edited objects.</summary>
        protected virtual bool SaveScene => false;

        /// <summary>Whether prefab instances are written back to their <c>.prefab</c> asset.</summary>
        protected virtual bool ApplyPrefabInstanceToAsset => false;

        /// <summary>Whether to force wide mode (label and field on one line).</summary>
        protected virtual bool ForceWideMode => false;

        /// <summary>Whether setup has completed (OnSetup has run).</summary>
        private bool _setupDone;

        /// <summary>Whether any target was already dirty before this frame was drawn.</summary>
        private bool _anyTargetDirtyBefore;

        /// <summary>Whether an actual change happened this frame (source of OnChanged).</summary>
        private bool _frameChanged;

        /// <summary>Whether OnEditorDestroy has been raised; domain reload and destroy may both fire.</summary>
        private bool _destroyNotified;

        /// <summary>The current target object, or null when the serialized state is unavailable.</summary>
        protected Object TargetObjectOrNull {
            get {
                try {
                    return serializedObject.targetObject == null ? null : serializedObject.targetObject;
                } catch (Exception) {
                    return null;
                }
            }
        }

        /// <summary>Inspector entry point: one-time setup on the first Layout, then draw and apply.</summary>
        public sealed override void OnInspectorGUI() {
            serializedObject.UpdateIfRequiredOrScript();
            _frameChanged = false;

            if (Event.current.type == EventType.Layout) {
                if (!_setupDone) {
                    _setupDone = true;
                    OnSetup();
                    AssemblyReloadEvents.beforeAssemblyReload -= NotifyDestroy;
                    AssemblyReloadEvents.beforeAssemblyReload += NotifyDestroy;
                }

                OnCalc();
            }

            // Snapshot the pre-frame dirty state so only changes made by this frame raise OnChanged.
            _anyTargetDirtyBefore = AnyTargetDirty();

            var prevWide = EditorGUIUtility.wideMode;
            if (ForceWideMode) {
                EditorGUIUtility.wideMode = true;
            }

            try {
                DrawScriptHeader();

                EditorGUI.BeginChangeCheck();
                try {
                    OnDraw();
                } finally {
                    if (EditorGUI.EndChangeCheck()) {
                        MarkChanged();
                    }
                }
            } finally {
                EditorGUIUtility.wideMode = prevWide;
            }

            ApplyAll();
            if (_frameChanged) {
                _frameChanged = false;
                OnChanged();
            }
        }

        /// <summary>Called once on the first Layout, for one-time setup.</summary>
        protected virtual void OnSetup() {
        }

        /// <summary>Called on every Layout, for per-frame pre-calculation.</summary>
        protected virtual void OnCalc() {
        }

        /// <summary>Draws the body. The base class draws nothing.</summary>
        protected virtual void OnDraw() {
        }

        /// <summary>Called after a change from this frame has been applied to the targets.</summary>
        protected virtual void OnChanged() {
        }

        /// <summary>Called when the inspector is disabled.</summary>
        protected virtual void OnDisabled() {
        }

        /// <summary>Called once when the editor is destroyed, including domain reload.</summary>
        protected virtual void OnEditorDestroy() {
        }

        /// <summary>Unity callback: reset setup state, notify the subclass and apply pending edits.</summary>
        private void OnDisable() {
            _setupDone = false;
            OnDisabled();
            ApplyAll(false);
        }

        /// <summary>Unity callback: forwards to the idempotent <see cref="NotifyDestroy"/>.</summary>
        private void OnDestroy() {
            NotifyDestroy();
        }

        /// <summary>Idempotent destroy notification; domain reload and destroy may both fire.</summary>
        private void NotifyDestroy() {
            if (_destroyNotified) {
                return;
            }

            _destroyNotified = true;
            AssemblyReloadEvents.beforeAssemblyReload -= NotifyDestroy;
            OnEditorDestroy();
        }

        /// <summary>Marks that this frame produced an actual change (source of OnChanged).</summary>
        protected void MarkChanged() {
            _frameChanged = true;
        }

        /// <summary>True when any target is dirty.</summary>
        private bool AnyTargetDirty() {
            foreach (var obj in targets) {
                if (obj == null) {
                    continue;
                }

                if (EditorUtility.IsDirty(obj)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Applies pending serialized edits. A target that became dirty this frame also counts as a
        /// change, which covers immediate Apply calls and the [GrowButton] path.
        /// </summary>
        private void ApplyAll(bool notify = true) {
            var changed = false;
            if (serializedObject.hasModifiedProperties) {
                serializedObject.ApplyModifiedProperties();
                changed = true;
            }

            if (!_anyTargetDirtyBefore && AnyTargetDirty()) {
                changed = true;
            }

            if (changed && notify) {
                MarkChanged();
            }
        }

        /// <summary>
        /// Applies pending edits and writes the targets to disk. Saves dirty persistent target assets,
        /// target prefab instances when <see cref="ApplyPrefabInstanceToAsset"/> is enabled, and the
        /// containing scenes when <see cref="SaveScene"/> is enabled; unrelated dirty assets are not saved.
        /// </summary>
        public void SaveAll() {
            ApplyAll(false);
            PerformSave();
        }

        /// <summary>Writes dirty assets, prefab instances and scenes for the current targets.</summary>
        private void PerformSave() {
            var assetObjects = new HashSet<Object>();
            var scenes = new HashSet<Scene>();
            var prefabRoots = new HashSet<GameObject>();

            foreach (var obj in targets) {
                if (!obj) {
                    continue;
                }

                if (EditorUtility.IsPersistent(obj)) {
                    if (EditorUtility.IsDirty(obj)) {
                        assetObjects.Add(obj);
                    }

                    continue;
                }

                var go = (obj as Component)?.gameObject ?? obj as GameObject;
                if (!go) {
                    continue;
                }

                if (ApplyPrefabInstanceToAsset && PrefabUtility.IsPartOfPrefabInstance(go)) {
                    var root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
                    if (root) {
                        prefabRoots.Add(root);
                    }
                }

                var scene = go.scene;
                if (scene.IsValid() && scene is { isLoaded: true, isDirty: true }) {
                    scenes.Add(scene);
                }
            }

            if (ApplyPrefabInstanceToAsset && prefabRoots.Count > 0) {
                foreach (var root in prefabRoots) {
                    if (!root) {
                        continue;
                    }

                    PrefabUtility.ApplyPrefabInstance(root, InteractionMode.AutomatedAction);
                    var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
                    if (string.IsNullOrEmpty(prefabPath)) {
                        continue;
                    }

                    var prefabAsset = AssetDatabase.LoadMainAssetAtPath(prefabPath);
                    if (prefabAsset) {
                        AssetDatabase.SaveAssetIfDirty(prefabAsset);
                    }
                }
            }

            foreach (var asset in assetObjects) {
                AssetDatabase.SaveAssetIfDirty(asset);
            }

            if (SaveScene) {
                foreach (var scene in scenes) {
                    EditorSceneManager.SaveScene(scene);
                }
            }
        }

        /// <summary>Draws the default read-only script header.</summary>
        protected void DrawScriptHeader() {
            var script = serializedObject.FindProperty("m_Script");
            if (script == null) {
                return;
            }

            using (new EditorGUI.DisabledScope(true)) {
                EditorGUILayout.PropertyField(script);
            }
        }
    }
}
