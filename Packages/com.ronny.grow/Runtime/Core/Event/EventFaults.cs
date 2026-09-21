// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System;
using UnityEngine;

namespace Grow.Core.Event {
    internal static class EventFaults {
        internal static bool IsFatal(Exception exception) {
            if (exception is OutOfMemoryException) return true;
            if (exception is StackOverflowException) return true;
            if (exception is AccessViolationException) return true;
            if (exception is AppDomainUnloadedException) return true;
            if (exception is BadImageFormatException) return true;
            if (exception is InvalidProgramException) return true;
            if (exception is ExitGUIException) return true;
#if NET_4_6 || NET_UNITY_4_8
            if (exception is System.Threading.ThreadAbortException) return true;
#endif
            return false;
        }

        internal static void Report(Delegate handler, Exception exception) {
            Debug.LogError(
                $"[Grow] Event listener '{Describe(handler)}' threw. The listener stays subscribed.\n{exception}");
        }

        private static string Describe(Delegate handler) {
            if (handler == null) return "<unknown>";
            var method = handler.Method;
            var type = method.DeclaringType;
            return type == null ? method.Name : type.FullName + "." + method.Name;
        }
    }
}
