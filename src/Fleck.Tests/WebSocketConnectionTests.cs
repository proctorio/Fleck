using System;
using System.Linq;
using Fleck.Handlers;
using Moq;
using NUnit.Framework;

namespace Fleck.Tests
{
    [TestFixture]
    public class WebSocketConnectionTests
    {
        private Mock<ISocket> _socketMock;
        private WebSocketConnection _connection;
        private Mock<IHandler> _handlerMock;

        [SetUp]
        public void Setup()
        {
            _socketMock = new Mock<ISocket>();
            _handlerMock = new Mock<IHandler>();
            _connection = new WebSocketConnection(_socketMock.Object,
                                                  connection => { },
                                                  b => new WebSocketHttpRequest(),
                                                  r => _handlerMock.Object,
                                                  s => default(string));
        }

        [Test]
        public void ShouldCloseOnReadingZero()
        {
            _socketMock.SetupGet(x => x.Connected).Returns(true);
            SetupReadLengths(0);
            bool hit = false;
            _connection.OnClose = () => hit = true;
            _connection.StartReceiving();
            Assert.IsTrue(hit);
        }

        [Test]
        public void ShouldNotSendOnClosed()
        {
            _connection.Handler = _handlerMock.Object;
            SetupReadLengths(0);
            _connection.StartReceiving();
            _connection.Send("Zing");
            _socketMock.Verify(x => x.Send(It.IsAny<byte[]>(), It.IsAny<Action>(), It.IsAny<Action<Exception>>()), Times.Never());
        }

        [Test]
        public void ShouldNotSendWhenSocketDisconnected()
        {
            _connection.Handler = _handlerMock.Object;
            _socketMock.SetupGet(x => x.Connected).Returns(false);
            _connection.Send("Zing");
            _socketMock.Verify(x => x.Send(It.IsAny<byte[]>(), It.IsAny<Action>(), It.IsAny<Action<Exception>>()), Times.Never());
        }

        [Test]
        public void ShouldNotReadWhenSocketClosed()
        {
            _socketMock.SetupGet(x => x.Connected).Returns(false);
            _connection.StartReceiving();
            _socketMock.Verify(x => x.Receive(It.IsAny<byte[]>(), It.IsAny<Action<int>>(), It.IsAny<Action<Exception>>(), 0), Times.Never());
        }

        [Test]
        public void ShouldRaiseInitializeOnFirstRead()
        {
            bool initializeRaised = false;
            var connection = new WebSocketConnection(_socketMock.Object,
                                                  conn => { initializeRaised = true; },
                                                  b => new WebSocketHttpRequest(),
                                                  r => _handlerMock.Object,
                                                  s => default(string));

            _socketMock.SetupGet(x => x.Connected).Returns(true);
            SetupReadLengths(1, 0);
            connection.StartReceiving();

            Assert.IsTrue(initializeRaised);
        }

        [Test]
        public void ShouldNotRaiseInitializeIfParseRequestReturnsNull()
        {
            bool initializeRaised = false;
            var connection = new WebSocketConnection(_socketMock.Object,
                                                  conn => { initializeRaised = true; },
                                                  b => null,
                                                  r => _handlerMock.Object,
                                                  s => default(string));

            _socketMock.SetupGet(x => x.Connected).Returns(true);
            SetupReadLengths(1, 0);
            connection.StartReceiving();

            Assert.IsFalse(initializeRaised);
        }

        [Test]
        public void ShouldNotRaiseInitializeIfHandlerFactoryReturnsNull()
        {
            bool initializeRaised = false;
            var connection = new WebSocketConnection(_socketMock.Object,
                                                  conn => { initializeRaised = true; },
                                                  b => new WebSocketHttpRequest(),
                                                  r => null,
                                                  s => null);

            _socketMock.SetupGet(x => x.Connected).Returns(true);
            SetupReadLengths(1, 0);
            connection.StartReceiving();

            Assert.IsFalse(initializeRaised);
        }

        [Test]
        public void ShouldCallOnErrorWhenError()
        {
            _socketMock.Setup(
                x =>
                x.Receive(It.IsAny<byte[]>(), It.IsAny<Action<int>>(), It.IsAny<Action<Exception>>(), It.IsAny<int>()))
                .Callback<byte[], Action<int>, Action<Exception>, int>((buffer, success, error, offset) =>
                {
                    error(new Exception());
                });

            _socketMock.SetupGet(x => x.Connected).Returns(true);

            bool hit = false;
            _connection.OnError = e => hit = true;

            _connection.StartReceiving();
            Assert.IsTrue(hit);
        }

        [Test]
        public void ShouldSwallowObjectDisposedExceptionOnRead()
        {
            _socketMock.Setup(
                x =>
                x.Receive(It.IsAny<byte[]>(), It.IsAny<Action<int>>(), It.IsAny<Action<Exception>>(), It.IsAny<int>()))
                .Callback<byte[], Action<int>, Action<Exception>, int>((buffer, success, error, offset) =>
                {
                    error(new ObjectDisposedException("socket"));
                });

            _socketMock.SetupGet(x => x.Connected).Returns(true);

            bool hit = false;
            _connection.OnError = e => hit = true;

            _connection.StartReceiving();
            Assert.IsFalse(hit);
        }

        [Test]
        public void ShouldAnswerPlainHttpAndHalfCloseWithoutOpening()
        {
            bool initializeRaised = false;
            var connection = new WebSocketConnection(_socketMock.Object,
                                                  conn => { initializeRaised = true; },
                                                  b => new WebSocketHttpRequest {Method = "GET"},
                                                  r => HttpGetHandler.Create(r),
                                                  s => default(string));

            byte[] sent = null;
            _socketMock.Setup(x => x.Send(It.IsAny<byte[]>(), It.IsAny<Action>(), It.IsAny<Action<Exception>>()))
                .Callback<byte[], Action, Action<Exception>>((bytes, callback, error) =>
                {
                    sent = bytes;
                    callback();
                });
            _socketMock.SetupGet(x => x.Connected).Returns(true);
            SetupReadLengths(1, 0);
            connection.StartReceiving();

            Assert.IsFalse(initializeRaised);
            StringAssert.StartsWith("HTTP/1.1 200 OK\r\n", System.Text.Encoding.ASCII.GetString(sent));
            _socketMock.Verify(x => x.ShutdownSend(), Times.Once());
        }

        [TestCase(4096, 4)]
        [TestCase(WebSocketConnection.MaxHandshakeSize + 1, 0)]
        public void ShouldCloseWhenPreHandshakeDataExceedsCap(int readLength, int readsBuffered)
        {
            var parseCalls = 0;
            var connection = new WebSocketConnection(_socketMock.Object,
                                                  conn => { },
                                                  b => { parseCalls++; return null; },
                                                  r => _handlerMock.Object,
                                                  s => default(string));

            _socketMock.SetupGet(x => x.Connected).Returns(true);
            // a client that streams without ever completing its request
            SetupReadLengths(readLength, readLength, readLength, readLength, readLength, readLength, readLength, readLength);
            bool closed = false;
            connection.OnClose = () => closed = true;
            connection.StartReceiving();

            Assert.IsTrue(closed);
            Assert.AreEqual(readsBuffered, parseCalls);
            Assert.IsNull(connection.Handler);
            _socketMock.Verify(x => x.Close(), Times.Once());
            _socketMock.Verify(x => x.Receive(It.IsAny<byte[]>(), It.IsAny<Action<int>>(), It.IsAny<Action<Exception>>(), It.IsAny<int>()), Times.Exactly(readsBuffered + 1));
        }

        [TestCase(0)]
        [TestCase(WebSocketConnection.MaxHandshakeSize)]
        public void ShouldCompleteHandshakeWithinCap(int padToLength)
        {
            var request = HandshakeRequest;
            if (padToLength > 0)
            {
                // "X-Pad: " + value + "\r\n" brings the request to exactly padToLength bytes
                var padding = "X-Pad: " + new string('a', padToLength - request.Length - 9) + "\r\n";
                request = request.Substring(0, request.Length - 2) + padding + "\r\n";
            }
            var bytes = System.Text.Encoding.ASCII.GetBytes(request);
            Assert.AreEqual(padToLength > 0 ? padToLength : bytes.Length, bytes.Length);

            bool initializeRaised = false;
            var connection = new WebSocketConnection(_socketMock.Object,
                                                  conn => { initializeRaised = true; },
                                                  b => RequestParser.Parse(b),
                                                  r => HandlerFactory.BuildHandler(r, x => { }, () => { }, x => { }, x => { }, x => { }),
                                                  s => default(string));

            byte[] sent = null;
            _socketMock.Setup(x => x.Send(It.IsAny<byte[]>(), It.IsAny<Action>(), It.IsAny<Action<Exception>>()))
                .Callback<byte[], Action, Action<Exception>>((b, callback, error) => sent = b);
            _socketMock.SetupGet(x => x.Connected).Returns(true);
            // the request arrives split across two reads
            var half = bytes.Length / 2;
            SetupReads(bytes.Take(half).ToArray(), bytes.Skip(half).ToArray());
            connection.StartReceiving();

            Assert.IsTrue(initializeRaised);
            Assert.IsNotNull(connection.Handler);
            StringAssert.StartsWith("HTTP/1.1 101 Switching Protocols\r\n", System.Text.Encoding.ASCII.GetString(sent));
            _socketMock.Verify(x => x.Close(), Times.Never());
        }

        private const string HandshakeRequest =
"GET /chat HTTP/1.1\r\n" +
"Host: server.example.com\r\n" +
"Upgrade: websocket\r\n" +
"Connection: Upgrade\r\n" +
"Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n" +
"Sec-WebSocket-Version: 13\r\n" +
"\r\n";

        private void SetupReads(params byte[][] chunks)
        {
            var index = 0;
            _socketMock.Setup(
                x =>
                x.Receive(It.IsAny<byte[]>(), It.IsAny<Action<int>>(), It.IsAny<Action<Exception>>(), It.IsAny<int>()))
                .Callback<byte[], Action<int>, Action<Exception>, int>((buffer, success, error, offset) =>
                {
                    if (chunks.Length > index)
                    {
                        var chunk = chunks[index++];
                        Array.Copy(chunk, buffer, chunk.Length);
                        success(chunk.Length);
                    }
                });
        }

        private void SetupReadLengths(params int[] args)
        {
            var index = 0;
            _socketMock.Setup(
                x =>
                x.Receive(It.IsAny<byte[]>(), It.IsAny<Action<int>>(), It.IsAny<Action<Exception>>(), It.IsAny<int>()))
                .Callback<byte[], Action<int>, Action<Exception>, int>((buffer, success, error, offset) =>
                {
                    if (args.Length > index)
                        success(args[index++]);
                    else
                        _socketMock.SetupGet(x => x.Connected == false);
                });
        }
    }
}
