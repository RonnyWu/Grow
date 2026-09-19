// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using UnityEditor;
using UnityEngine;

namespace Grow.Editor.Setup
{
    public static class GrowProjectSetup
    {
        private const string MenuRoot = "Tools/Grow/Setup/";

        private const EnterPlayModeOptions RequiredOptions = EnterPlayModeOptions.DisableDomainReload;

        [MenuItem(MenuRoot + "Apply Project Settings", priority = 100)]
        public static void ApplyProjectSettings()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning(
                    "[Grow] Cannot apply project settings while entering or in Play mode. " +
                    "Exit Play mode and try again.");
                return;
            }

            if (IsConfigured())
            {
                Debug.Log("[Grow] Project settings already configured. Nothing to do.");
                return;
            }

            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = RequiredOptions;
            AssetDatabase.SaveAssets();

            Debug.Log(
                "[Grow] Project settings applied: Domain Reload disabled, Scene Reload kept. " +
                "Static state is reset by GrowBoot at SubsystemRegistration.");
        }

        [MenuItem(MenuRoot + "Diagnose", priority = 101)]
        public static void Diagnose()
        {
            var configured = IsConfigured();

            Debug.Log(
                "[Grow] Project settings diagnose:\n" +
                $"  Enter Play Mode Options enabled : {EditorSettings.enterPlayModeOptionsEnabled}\n" +
                $"  Enter Play Mode Options         : {EditorSettings.enterPlayModeOptions}\n" +
                $"  Required                        : enabled, {RequiredOptions}\n" +
                $"  Status                          : {(configured ? "OK" : "NOT CONFIGURED")}");

            if (!configured)
            {
                Debug.LogWarning(
                    "[Grow] Run 'Tools/Grow/Setup/Apply Project Settings' to configure the project.");
            }
        }

        private static bool IsConfigured()
        {
            return EditorSettings.enterPlayModeOptionsEnabled
                && EditorSettings.enterPlayModeOptions == RequiredOptions;
        }
    }
}
