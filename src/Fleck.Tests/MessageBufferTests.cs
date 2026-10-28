using NUnit.Framework;

namespace Fleck.Tests
{
    [TestFixture]
    public class MessageBufferTests
    {
        [Test]
        public void ShouldAppendOnlyCountBytes()
        {
            var buffer = new MessageBuffer(4);

            buffer.Append(new byte[] {1, 2, 3, 9, 9}, 3);

            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual(new byte[] {1, 2, 3}, ReadAll(buffer));
        }

        [Test]
        public void ShouldGrowPastInitialCapacity()
        {
            var buffer = new MessageBuffer(2);

            buffer.Append(new byte[] {1, 2}, 2);
            buffer.Append(new byte[] {3, 4, 5}, 3);

            Assert.AreEqual(new byte[] {1, 2, 3, 4, 5}, ReadAll(buffer));
        }

        [Test]
        public void ShouldReadRelativeToHeadAfterConsume()
        {
            var buffer = new MessageBuffer(8);
            buffer.Append(new byte[] {1, 2, 3, 4}, 4);

            buffer.Consume(2);

            Assert.AreEqual(2, buffer.Count);
            Assert.AreEqual(3, buffer[0]);
            Assert.AreEqual(new byte[] {3, 4}, ReadAll(buffer));
        }

        [Test]
        public void ShouldKeepLiveBytesWhenCompacting()
        {
            var buffer = new MessageBuffer(4);
            buffer.Append(new byte[] {1, 2, 3, 4}, 4);
            buffer.Consume(3);

            // fits once the consumed prefix is dropped
            buffer.Append(new byte[] {5, 6, 7}, 3);

            Assert.AreEqual(new byte[] {4, 5, 6, 7}, ReadAll(buffer));
        }

        [Test]
        public void ShouldResetWhenDrained()
        {
            var buffer = new MessageBuffer(4);
            buffer.Append(new byte[] {1, 2, 3}, 3);

            buffer.Consume(3);
            Assert.AreEqual(0, buffer.Count);

            buffer.Append(new byte[] {8}, 1);
            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(8, buffer[0]);
        }

        private static byte[] ReadAll(MessageBuffer buffer)
        {
            var bytes = new byte[buffer.Count];
            buffer.CopyTo(0, bytes, 0, bytes.Length);
            return bytes;
        }
    }
}
