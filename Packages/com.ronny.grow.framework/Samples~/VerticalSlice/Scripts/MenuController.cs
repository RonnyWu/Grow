// SPDX-License-Identifier: MIT

using Grow.Scenes;
using UnityEngine;

namespace Grow.Samples.VerticalSlice {
    /// <summary>
    /// The menu screen: shows the coins restored from the store and starts the game scene.
    /// </summary>
    /// <remarks>
    /// The controller resolves the session module and the scene flow from the service registry; it
    /// owns no state of its own, so "continue" and "start" are the same action here — the coin count
    /// already came back from the store when the module initialized.
    /// </remarks>
    public sealed class MenuController : MonoBehaviour {
        private const string GameplayScene = "Gameplay";

        private SessionModule _session;
        private ISceneFlow _scenes;
        private bool _leaving;
        private string _error;

        private void Update() {
            var host = SampleEntry.Host;
            if (_session != null || host == null) {
                return;
            }

            _session = host.GetRequiredService<SessionModule>();
            _scenes = host.GetRequiredService<ISceneFlow>();
        }

        private void OnGUI() {
            GUILayout.BeginArea(new Rect(24, 24, 460, 240));
            GUILayout.Label("Grow vertical slice - menu");

            if (_error != null) {
                GUILayout.Label("Scene load failed: " + _error);
            } else if (_session == null) {
                GUILayout.Label("Waiting for the composition...");
            } else {
                GUILayout.Label("Coins: " + _session.Coins);
                if (!_leaving && GUILayout.Button("Start")) {
                    _leaving = true;
                    var request = _scenes.Load(GameplayScene);
                    if (request.IsFaulted) {
                        _error = request.Error;
                        _leaving = false;
                    }
                }
            }

            GUILayout.EndArea();
        }
    }
}
