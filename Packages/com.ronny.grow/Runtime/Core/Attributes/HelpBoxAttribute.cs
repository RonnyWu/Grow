// SPDX-License-Identifier: MIT

using System;
using UnityEngine;

namespace Grow.Core.Attributes {
    /// <summary>Visual kind of a Grow help box; mirrors UnityEditor.MessageType without an editor reference.</summary>
    public enum GrowHelpBoxKind {
        /// <summary>Informational message.</summary>
        Info = 0,

        /// <summary>Warning message.</summary>
        Warning = 1,

        /// <summary>Error message.</summary>
        Error = 2
    }

    /// <summary>
    /// Draws a help box above a serialized field, with the same visual language as
    /// <c>EditorGUILayout.HelpBox</c>.
    /// <para>This is a decorator: it adds content above the field without changing how the field itself
    /// is drawn. It only applies to Grow's editor; Unity's default Inspector ignores it.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class GrowHelpBoxAttribute : PropertyAttribute {
        /// <summary>The message shown in the box.</summary>
        public readonly string Message;

        /// <summary>The box kind (Info, Warning or Error).</summary>
        public readonly GrowHelpBoxKind Kind;

        /// <summary>Creates a help box attribute.</summary>
        /// <param name="message">Message text; must not be null.</param>
        /// <param name="kind">Visual kind.</param>
        public GrowHelpBoxAttribute(string message, GrowHelpBoxKind kind = GrowHelpBoxKind.Info) {
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Kind = kind;
        }
    }
}
