// SPDX-License-Identifier: MIT

using Grow.Core.Events;
using NUnit.Framework;

namespace Grow.Tests.Editor.Events {
    // Temporary ergonomics spike: three realistic usages, compiled and exercised.
    [Explicit("Manual ergonomics spike; run only on demand.")]
    [Category("Events.UsageSpike")]
    public class GrowEventUsageSpike {
        private enum BootPhase {
            Installing,
            Booted,
            Ready
        }

        private sealed class Button {
            private readonly GrowEvent.Publisher _clicked = new();
            public GrowEvent Clicked => _clicked.Event;
            public void Click() => _clicked.Publish();
        }

        private sealed class ReadyObserver {
            private GrowSubscription _subscription;
            public BootPhase Last;

            public ReadyObserver(GrowEvent<BootPhase> phase) {
                _subscription = phase.Subscribe(OnPhase);
            }

            public bool IsReady => Last == BootPhase.Ready;

            public void Dispose() {
                _subscription.Dispose();
            }

            private void OnPhase(BootPhase phase) {
                Last = phase;
            }
        }

        private sealed class FrameTicker {
            private readonly GrowEvent<float>.Publisher _tick = new();
            public GrowEvent<float> Tick => _tick.Event;
            public void Advance(float deltaTime) => _tick.Publish(deltaTime);
        }

        private sealed class FrameStats {
            private GrowSubscription _subscription;
            public float Total;

            public FrameStats(GrowEvent<float> tick) {
                _subscription = tick.Subscribe(OnTick);
            }

            public void Dispose() {
                _subscription.Dispose();
            }

            private void OnTick(float deltaTime) {
                Total += deltaTime;
            }
        }

        [Test]
        public void UiButton_SubscribeClickDispose() {
            var button = new Button();
            var clicks = 0;
            var subscription = button.Clicked.Subscribe(() => clicks++);

            button.Click();
            button.Click();
            subscription.Dispose();
            button.Click();

            Assert.AreEqual(2, clicks);
        }

        [Test]
        public void ReadyBarrier_SubscribeObserveReady() {
            var phase = new GrowEvent<BootPhase>.Publisher();
            var observer = new ReadyObserver(phase.Event);

            phase.Publish(BootPhase.Installing);
            phase.Publish(BootPhase.Booted);
            phase.Publish(BootPhase.Ready);

            Assert.IsTrue(observer.IsReady);
            observer.Dispose();
        }

        [Test]
        public void FrameTicker_SubscribeAccumulate() {
            var ticker = new FrameTicker();
            var stats = new FrameStats(ticker.Tick);

            ticker.Advance(0.016f);
            ticker.Advance(0.020f);
            ticker.Advance(0.014f);

            Assert.AreEqual(0.05f, stats.Total, 0.0001f);
            stats.Dispose();
        }
    }
}
