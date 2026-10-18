// SPDX-License-Identifier: MIT

using System.Text;
using Grow.Core.Pool;
using NUnit.Framework;

namespace Grow.Tests.Editor.Pool {
    public class StringBuilderPoolTests {
        [SetUp]
        public void SetUp() {
            StringBuilderPool.ResetStaticState();
        }

        [TearDown]
        public void TearDown() {
            StringBuilderPool.ResetStaticState();
        }

        [Test]
        public void Rent_ReturnsABuilderWithAtLeastTheRequestedCapacity() {
            using var pooled = StringBuilderPool.Rent(2048);

            Assert.NotNull(pooled.Builder);
            Assert.GreaterOrEqual(pooled.Builder.Capacity, 2048);
        }

        [Test]
        public void Dispose_ReturnsTheBuilder_AndTheNextRentReusesIt() {
            StringBuilder first;
            using (var pooled = StringBuilderPool.Rent()) {
                first = pooled.Builder;
            }

            using var second = StringBuilderPool.Rent();
            Assert.AreSame(first, second.Builder);
        }

        [Test]
        public void Dispose_ClearsTheContentBeforeReuse() {
            StringBuilder first;
            using (var pooled = StringBuilderPool.Rent()) {
                pooled.Builder.Append("stale");
                first = pooled.Builder;
            }

            using var second = StringBuilderPool.Rent();
            Assert.AreSame(first, second.Builder);
            Assert.AreEqual(0, second.Builder.Length);
        }

        [Test]
        public void ToStringAndDispose_ReturnsContentAndReleasesTheBuilder() {
            StringBuilder first;
            string text;
            using (var pooled = StringBuilderPool.Rent()) {
                first = pooled.Builder;
                pooled.Builder.Append("hello");
                text = pooled.ToStringAndDispose();
            }

            Assert.AreEqual("hello", text);

            using var second = StringBuilderPool.Rent();
            Assert.AreSame(first, second.Builder);
        }

        [Test]
        public void Dispose_Twice_IsIgnored() {
            StringBuilder first;
            var pooled = StringBuilderPool.Rent();
            first = pooled.Builder;

            pooled.Dispose();
            pooled.Dispose();

            Assert.IsNull(pooled.Builder);
            using var second = StringBuilderPool.Rent();
            Assert.AreSame(first, second.Builder);
        }

        [Test]
        public void Rent_AboveTheRetentionLimit_IsNotPooledOnReturn() {
            StringBuilder oversized;
            using (var pooled = StringBuilderPool.Rent(8192)) {
                oversized = pooled.Builder;
            }

            using var second = StringBuilderPool.Rent();
            Assert.AreNotSame(oversized, second.Builder);
        }

        [Test]
        public void Return_Null_IsIgnored() {
            Assert.DoesNotThrow(() => StringBuilderPool.Return(null));
        }

        [Test]
        public void ResetStaticState_DropsCachedBuilders() {
            StringBuilder first;
            using (var pooled = StringBuilderPool.Rent()) {
                first = pooled.Builder;
            }

            StringBuilderPool.ResetStaticState();

            using var second = StringBuilderPool.Rent();
            Assert.AreNotSame(first, second.Builder);
        }

        [Test]
        public void CopyingTheToken_DoesNotReturnTheBuilderTwice() {
            var first = StringBuilderPool.Rent();
            var copy = first;
            first.Dispose();
            copy.Dispose();

            using var a = StringBuilderPool.Rent();
            using var b = StringBuilderPool.Rent();

            Assert.AreNotSame(a.Builder, b.Builder);
        }

        [Test]
        public void ThirdConcurrentRent_StillReturnsDistinctBuilders() {
            using var first = StringBuilderPool.Rent();
            using var second = StringBuilderPool.Rent();
            using var third = StringBuilderPool.Rent();

            Assert.AreNotSame(first.Builder, second.Builder);
            Assert.AreNotSame(first.Builder, third.Builder);
            Assert.AreNotSame(second.Builder, third.Builder);
        }
    }
}
