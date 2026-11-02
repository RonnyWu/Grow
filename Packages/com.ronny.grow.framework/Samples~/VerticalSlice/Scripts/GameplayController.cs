// SPDX-License-Identifier: MIT

using Grow.Scenes;
using UnityEngine;

namespace Grow.Samples.VerticalSlice {
    /// <summary>
    /// The gameplay screen: adds coins, saves them through the session module, and goes back.
    /// </summary>
    /// <remarks>
    /// The sample keeps the save explicit on purpose. <see cref="SessionModule"/> also saves when it
    /// stops, so stopping Play Mode keeps the count even when the player never pressed "Save".
    /// </remarks>
    public sealed class GameplayController : MonoBehaviour {
        private const string MenuScene = "Menu";

        private SessionModule _session;
        private ISceneFlow _scenes;
        private bool _leaving;
        private string _message;
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
            GUILayout.BeginArea(new Rect(24, 24, 460, 300));
            GUILayout.Label("Grow vertical slice - gameplay");

            if (_error != null) {
                GUILayout.Label("Scene load failed: " + _error);
            } else if (_session == null) {
                GUILayout.Label("Waiting for the composition...");
            } else {
                GUILayout.Label("Coins: " + _session.Coins);

                if (GUILayout.Button("Add a coin")) {
                    _session.AddCoins(1);
                    _message = "Coin added. It is not in the store yet.";
                }

                if (GUILayout.Button("Save")) {
                    _session.Save();
                    _message = "Saved " + _session.Coins + " coins.";
                }

                if (!_leaving && GUILayout.Button("Back to menu")) {
                    _leaving = true;
                    var request = _scenes.Load(MenuScene);
                    if (request.IsFaulted) {
                        _error = request.Error;
                        _leaving = false;
                    }
                }

                if (_message != null) {
                    GUILayout.Label(_message);
                }
            }

            GUILayout.EndArea();
        }
    }
}
