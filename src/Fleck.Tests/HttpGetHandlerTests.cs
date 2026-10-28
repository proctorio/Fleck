using System.Text;
using Fleck.Handlers;
using NUnit.Framework;

namespace Fleck.Tests
{
    [TestFixture]
    public class HttpGetHandlerTests
    {
        [Test]
        public void ShouldAnswer200WithEmptyBodyAndConnectionClose()
        {
            var handler = HttpGetHandler.Create(new WebSocketHttpRequest {Method = "GET"});

            var response = Encoding.ASCII.GetString(handler.CreateHandshake());

            Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", response);
        }

        [Test]
        public void ShouldIgnoreDataAndFrameNothing()
        {
            var handler = HttpGetHandler.Create(new WebSocketHttpRequest {Method = "HEAD"});

            Assert.DoesNotThrow(() => handler.Receive(new byte[] {1, 2, 3}, 3));
            Assert.IsEmpty(handler.FrameText("x"));
            Assert.IsEmpty(handler.FrameClose(WebSocketStatusCodes.NormalClosure));
        }
    }
}
