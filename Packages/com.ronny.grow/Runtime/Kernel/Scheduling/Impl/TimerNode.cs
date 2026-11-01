// SPDX-License-Identifier: MIT

using System;

namespace Grow.Kernel.Scheduling.Impl {
    /// <summary>One scheduled entry; stored by <see cref="TimerCore"/> until it fires or is dropped.</summary>
    internal sealed class TimerNode {
        internal Action Action;

        internal long Sequence;

        internal long DueFrame;

        internal double DueTime;

        internal float Interval;

        internal int IntervalFrames;

        internal TimeKind Kind;

        internal bool IsFrame;

        internal bool Repeating;

        internal bool Cancelled;
    }
}
