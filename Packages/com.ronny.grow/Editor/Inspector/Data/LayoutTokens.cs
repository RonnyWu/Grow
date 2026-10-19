// SPDX-License-Identifier: MIT

namespace Grow.Editor.Inspector.Data {
    /// <summary>
    /// A draw token: a top-level entry is either a loose member or a group (groups are contiguous and
    /// single-instance, and may nest).
    /// <para><see cref="InspectorTypeModel"/> produces the tokens and GrowEditor draws them in order.</para>
    /// </summary>
    internal abstract class LayoutToken {
    }

    /// <summary>The draw token for a [GrowButton] method member.</summary>
    internal sealed class ButtonToken : LayoutToken {
        /// <summary>The button method model.</summary>
        public readonly ButtonModel Button;

        public ButtonToken(ButtonModel button) {
            Button = button;
        }
    }

    /// <summary>The draw token for a serialized field member.</summary>
    internal sealed class FieldToken : LayoutToken {
        /// <summary>The field model.</summary>
        public readonly FieldModel Field;

        public FieldToken(FieldModel field) {
            Field = field;
        }
    }

    /// <summary>The draw token for a group member (containing nested field / button / child-group tokens).</summary>
    internal sealed class GroupToken : LayoutToken {
        /// <summary>The group model.</summary>
        public readonly GroupModel Group;

        public GroupToken(GroupModel group) {
            Group = group;
        }
    }
}
