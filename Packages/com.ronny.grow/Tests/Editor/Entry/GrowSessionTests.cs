// SPDX-License-Identifier: MIT

using System.Text;
using Grow.Core.Pool;
using Grow.Kernel.Entry;
using NUnit.Framework;

namespace Grow.Tests.Editor.Entry {
    public class GrowSessionTests {
        [SetUp]
        public void SetUp() {
            GrowSession.ResetStaticState();
        }

        [TearDown]
        public void TearDown() {
            GrowSession.ResetStaticState();
        }

        [Test]
        public void ResetStaticState_DropsPooledBuilders() {
            StringBuilder first;
            using (var pooled = StringBuilderPool.Rent()) {
                first = pooled.Builder;
            }

            GrowSession.ResetStaticState();

            using var second = StringBuilderPool.Rent();
            Assert.AreNotSame(first, second.Builder);
        }

        [Test]
        public void ResetStaticState_IsIdempotent() {
            Assert.DoesNotThrow(() => GrowSession.ResetStaticState());
            Assert.DoesNotThrow(() => GrowSession.ResetStaticState());
        }
    }
}
