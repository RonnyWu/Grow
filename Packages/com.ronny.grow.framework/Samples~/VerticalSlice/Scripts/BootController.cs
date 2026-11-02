// SPDX-License-Identifier: MIT

using Grow.Kernel.Entry;
using Grow.Scenes;
using UnityEngine;

namespace Grow.Samples.VerticalSlice {
    /// <summary>
    /// The cold-start screen: shows boot progress, then hands over to the menu scene.
    /// </summary>
    /// <remarks>
    /// The scene only observes the kernel. It never starts the composition — <see cref="SampleEntry"/>
    /// does that before any scene loads — so replacing this controller changes presentation only.
    /// </remarks>
    public sealed class BootController : MonoBehaviour {
        private const string NextScene = "Menu";

        private bool _requested;
        private string _error;
        private SceneRequest _request;

        private void Update() {
            var host = SampleEntry.Host;
            if (host == null || _error != null) {
                return;
            }

            if (_requested) {
                if (_request.IsFaulted) {
                    _error = _request.Error;
                }

                return;
            }

            if (GrowBoot.State == GrowState.Failed) {
                _error = GrowBoot.FailureMessage;
                return;
            }

            if (!host.IsReady) {
                return;
            }

            _requested = true;
            _request = host.GetRequiredService<ISceneFlow>().Load(NextScene);
        }

        private void OnGUI() {
            GUILayout.BeginArea(new Rect(24, 24, 460, 240));
            GUILayout.Label("Grow vertical slice - boot");

            if (_error != null) {
                GUILayout.Label("Boot failed: " + _error);
            } else if (_requested) {
                GUILayout.Label("Loading " + NextScene + "...");
            } else if (SampleEntry.Host == null) {
                GUILayout.Label("Waiting for the composition...");
            } else {
                GUILayout.Label("State: " + GrowBoot.State);
                GUILayout.Label("Module: " + DescribeLoadingModule());
                GUILayout.Label("Progress: " + GrowBoot.Progress.ToString("P0"));
            }

            GUILayout.EndArea();
        }

        private static string DescribeLoadingModule() {
            var name = GrowBoot.LoadingModule;
            return string.IsNullOrEmpty(name) ? "(none)" : name;
        }
    }
}
