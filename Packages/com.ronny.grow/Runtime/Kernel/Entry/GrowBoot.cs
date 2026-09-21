// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System;
using UnityEngine;
using LoadType = UnityEngine.RuntimeInitializeLoadType;
using LoadMethod = UnityEngine.RuntimeInitializeOnLoadMethodAttribute;

namespace Grow.Kernel.Entry {
    public static class GrowBoot {
        public static event Action OnReset;
        public static event Action OnBoot;
        public static event Action OnReady;
        public static event Action OnShutdown;

        [LoadMethod(LoadType.SubsystemRegistration)]
        private static void SubsystemRegistration() {
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
            OnReset?.Invoke();
        }

        [LoadMethod(LoadType.BeforeSceneLoad)]
        private static void BeforeSceneLoad() {
            OnBoot?.Invoke();
        }

        [LoadMethod(LoadType.AfterSceneLoad)]
        private static void AfterSceneLoad() {
            OnReady?.Invoke();
        }

        private static void OnQuitting() {
            OnShutdown?.Invoke();
        }
    }
}