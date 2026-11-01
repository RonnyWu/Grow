// SPDX-License-Identifier: MIT

using System;
using Grow.Core.Events;
using Grow.Core.Log;
using UnityEngine;

namespace Grow.Kernel.Entry {
    /// <summary>
    /// The kernel lifecycle state machine.
    /// </summary>
    /// <remarks>
    /// Internal on purpose: <see cref="GrowBoot"/> is the public face, so callers cannot reach
    /// around it.
    /// <para>
    /// The runtime is driven by hand: tests today, and the composition root in a game. Nothing is started
    /// automatically: <see cref="ResetStaticState"/> only <em>invalidates</em> state at the start of
    /// a play session, it never begins a cycle.
    /// </para>
    /// <para>
    /// The entries are <em>ordered</em> and <em>idempotent</em>: calling one too early fails
    /// fast with a readable message, while repeating one neither throws nor moves the state
    /// backwards.
    /// </para>
    /// <para>
    /// Every entry spells out the states it accepts rather than comparing the enum's numeric order,
    /// so adding a state later cannot silently change what an entry does.
    /// </para>
    /// <para>
    /// State is committed before <see cref="StateChanged"/> runs, and a subscriber that throws is
    /// isolated: observers can never change what the machine did, nor silence the other observers.
    /// </para>
    /// </remarks>
    internal static class GrowRuntime {
        private static GrowState _state = GrowState.None;
        private static string _failureMessage;

        /// <summary>Current lifecycle state. Starts at <see cref="GrowState.None"/>.</summary>
        internal static GrowState State => _state;

        /// <summary>
        /// True once the cycle reached <see cref="GrowState.Ready"/>: every module started and every
        /// asynchronous load finished.
        /// </summary>
        internal static bool IsReady => _state == GrowState.Ready;

        /// <summary>Message of the first fatal boot failure in this cycle, or <c>null</c>.</summary>
        internal static string FailureMessage => _failureMessage;

        /// <summary>
        /// Raised after every state change, with the previous and the new state. Handlers must not
        /// throw and must not re-enter the machine; the publisher's sink reports one that throws and
        /// keeps the other handlers running.
        /// </summary>
        internal static GrowEvent<StateChange> StateChanged => StateChangedPublisher.Event;

        private static readonly GrowEvent<StateChange>.Publisher StateChangedPublisher =
            new(exception => GrowLog.Error($"A StateChanged handler threw: {exception}", "Kernel"));

        /// <summary>
        /// Starts a cycle: <see cref="GrowState.None"/>, <see cref="GrowState.Shutdown"/> and
        /// <see cref="GrowState.Failed"/> move to <see cref="GrowState.Reset"/>. Any later state is
        /// left alone, so this never moves the runtime backwards.
        /// </summary>
        internal static void Reset() {
            switch (_state) {
                case GrowState.None:
                case GrowState.Failed:
                case GrowState.Shutdown:
                    _failureMessage = null;
                    SetState(GrowState.Reset);
                    break;
                case GrowState.Reset:
                case GrowState.Installed:
                case GrowState.Booted:
                case GrowState.Loading:
                case GrowState.Ready:
                    break;
                default:
                    throw UnhandledState();
            }
        }

        /// <summary>
        /// Installs kernel modules. Requires <see cref="GrowState.Reset"/> or later and fails fast
        /// when called before <see cref="Reset"/>; from <see cref="GrowState.Installed"/> onwards it
        /// does nothing.
        /// </summary>
        /// <remarks>
        /// The composition root calls this after the loop, manager and modules are installed, so the
        /// Installed notification observes a fully composed kernel.
        /// </remarks>
        internal static void Install() {
            switch (_state) {
                case GrowState.None:
                    throw MissingPrequel(GrowState.Reset, nameof(Install));
                case GrowState.Shutdown:
                    throw AfterShutdown(nameof(Install));
                case GrowState.Failed:
                    throw AfterFailure(nameof(Install));
                case GrowState.Reset:
                    SetState(GrowState.Installed);
                    break;
                case GrowState.Installed:
                case GrowState.Booted:
                case GrowState.Loading:
                case GrowState.Ready:
                    break;
                default:
                    throw UnhandledState();
            }
        }

        /// <summary>
        /// Boots the kernel. Requires <see cref="GrowState.Installed"/> or later and fails fast when
        /// called before <see cref="Install"/>; from <see cref="GrowState.Loading"/> onwards it does
        /// nothing.
        /// </summary>
        /// <param name="complete">
        /// When true (the default) the entry continues to <see cref="GrowState.Ready"/>, which is the
        /// synchronous boot path. The composition root passes false when a module reported an
        /// asynchronous load, and <see cref="Ready"/> completes the boot later.
        /// </param>
        internal static void Boot(bool complete = true) {
            switch (_state) {
                case GrowState.None:
                case GrowState.Reset:
                    throw MissingPrequel(GrowState.Installed, nameof(Boot));
                case GrowState.Shutdown:
                    throw AfterShutdown(nameof(Boot));
                case GrowState.Failed:
                    throw AfterFailure(nameof(Boot));
                case GrowState.Installed:
                    SetState(GrowState.Booted);
                    if (complete) {
                        SetState(GrowState.Ready);
                    }

                    break;
                case GrowState.Booted:
                    if (complete) {
                        SetState(GrowState.Ready);
                    }

                    break;
                case GrowState.Loading:
                case GrowState.Ready:
                    break;
                default:
                    throw UnhandledState();
            }
        }

        /// <summary>
        /// Enters <see cref="GrowState.Loading"/> from <see cref="GrowState.Booted"/>; repeating it
        /// while loading or ready does nothing.
        /// </summary>
        internal static void BeginLoading() {
            switch (_state) {
                case GrowState.None:
                case GrowState.Reset:
                case GrowState.Installed:
                    throw MissingPrequel(GrowState.Booted, nameof(BeginLoading));
                case GrowState.Shutdown:
                    throw AfterShutdown(nameof(BeginLoading));
                case GrowState.Failed:
                    throw AfterFailure(nameof(BeginLoading));
                case GrowState.Booted:
                    SetState(GrowState.Loading);
                    break;
                case GrowState.Loading:
                case GrowState.Ready:
                    break;
                default:
                    throw UnhandledState();
            }
        }

        /// <summary>
        /// Completes the boot from <see cref="GrowState.Booted"/> or <see cref="GrowState.Loading"/>.
        /// Requires <see cref="Boot"/> to have run first.
        /// </summary>
        internal static void Ready() {
            switch (_state) {
                case GrowState.None:
                case GrowState.Reset:
                case GrowState.Installed:
                    throw MissingPrequel(GrowState.Booted, nameof(Ready));
                case GrowState.Shutdown:
                    throw AfterShutdown(nameof(Ready));
                case GrowState.Failed:
                    throw AfterFailure(nameof(Ready));
                case GrowState.Booted:
                case GrowState.Loading:
                    SetState(GrowState.Ready);
                    break;
                case GrowState.Ready:
                    break;
                default:
                    throw UnhandledState();
            }
        }

        /// <summary>
        /// Records a fatal boot failure. Valid from <see cref="GrowState.Booted"/> or
        /// <see cref="GrowState.Loading"/>; the first message is kept and a ready runtime ignores the
        /// call.
        /// </summary>
        internal static void Fail(string message) {
            switch (_state) {
                case GrowState.None:
                case GrowState.Reset:
                case GrowState.Installed:
                    throw MissingPrequel(GrowState.Booted, nameof(Fail));
                case GrowState.Shutdown:
                    throw AfterShutdown(nameof(Fail));
                case GrowState.Booted:
                case GrowState.Loading:
                    if (_failureMessage == null) {
                        _failureMessage = message;
                    }

                    SetState(GrowState.Failed);
                    break;
                case GrowState.Ready:
                case GrowState.Failed:
                    break;
                default:
                    throw UnhandledState();
            }
        }

        /// <summary>
        /// Shuts the runtime down. Does nothing when nothing has started, and is idempotent when it
        /// is already shut down; otherwise it ends the cycle until <see cref="Reset"/> begins a new
        /// one.
        /// </summary>
        internal static void Shutdown() {
            switch (_state) {
                case GrowState.None:
                case GrowState.Shutdown:
                    break;
                case GrowState.Reset:
                case GrowState.Installed:
                case GrowState.Booted:
                case GrowState.Loading:
                case GrowState.Ready:
                case GrowState.Failed:
                    SetState(GrowState.Shutdown);
                    break;
                default:
                    throw UnhandledState();
            }
        }

        /// <summary>Drops the machine's static state at the start of every play session.</summary>
        /// <remarks>
        /// Unity skips the domain reload when "Enter Play Mode Options" disables it, so
        /// <see cref="_state"/> and <see cref="StateChanged"/> would otherwise survive
        /// <c>Play</c> → <c>Stop</c> → <c>Play</c>: a stale <see cref="GrowState.Ready"/> makes the
        /// next session's <see cref="Install"/> throw, and stale handlers fire against objects Unity
        /// has already destroyed. <see cref="GrowSession"/> still runs at
        /// <c>SubsystemRegistration</c> in that configuration, before any consumer code, so
        /// subscribers registered later see a clean machine.
        /// </remarks>
        internal static void ResetStaticState() {
            _state = GrowState.None;
            _failureMessage = null;
            StateChangedPublisher.Clear();

            // Subscribe once per session; the removal keeps a domain reload from doubling up.
            Application.quitting -= OnApplicationQuitting;
            Application.quitting += OnApplicationQuitting;
        }

        /// <summary>Ends the cycle when the process quits, so modules get a teardown signal.</summary>
        /// <remarks>
        /// Best effort: Unity does not raise this on a crash or a force-quit, and iOS suspends
        /// instead of quitting, so teardown that must happen still needs an explicit
        /// <see cref="Shutdown"/>.
        /// </remarks>
        private static void OnApplicationQuitting() {
            Shutdown();
        }

        private static void SetState(GrowState next) {
            if (_state == next) {
                return;
            }

            var previous = _state;
            _state = next;

            if (GrowLog.IsEnabled(LogLevel.Info)) {
                GrowLog.Info($"{previous} -> {next}", "Kernel");
            }

            StateChangedPublisher.Publish(new StateChange(previous, next));
        }

        private static InvalidOperationException UnhandledState() {
            return new InvalidOperationException(
                $"Unhandled lifecycle state {_state}; an entry is missing a case.");
        }

        private static InvalidOperationException MissingPrequel(GrowState minimum, string entry) {
            return new InvalidOperationException(
                $"{entry}() requires {minimum} or later, but the runtime is at {_state}.");
        }

        private static InvalidOperationException AfterShutdown(string entry) {
            return new InvalidOperationException(
                $"{entry}() cannot run after Shutdown(); call Reset() to start a new cycle.");
        }

        private static InvalidOperationException AfterFailure(string entry) {
            return new InvalidOperationException(
                $"{entry}() cannot run after a failed boot; call Reset() to start a new cycle.");
        }
    }
}
