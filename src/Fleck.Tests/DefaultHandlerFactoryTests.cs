using Fleck.Handlers;
using NUnit.Framework;

namespace Fleck.Tests
{
    [TestFixture]
    public class DefaultHandlerFactoryTests
    {
        [Test]
        public void ShouldReturnHandlerForValidHeaders()
        {
            var request = new WebSocketHttpRequest {Headers = {{"Sec-WebSocket-Version", "13"}, {"Sec-WebSocket-Key", "dGhlIHNhbXBsZSBub25jZQ=="}}};
            var handler = HandlerFactory.BuildHandler(request, x => { }, () => { }, x => { }, x => { }, x => { });
            
            Assert.IsNotNull(handler);
        }

        [TestCase("GET")]
        [TestCase("HEAD")]
        public void ShouldReturnHttpGetHandlerForPlainHttp(string method)
        {
            var request = new WebSocketHttpRequest {Method = method};
            var handler = HandlerFactory.BuildHandler(request, x => { }, () => { }, x => { }, x => { }, x => { });

            Assert.IsInstanceOf<HttpGetHandler>(handler);
        }

        [Test]
        public void ShouldRefuseDraft76Handshake()
        {
            var request = new WebSocketHttpRequest {Method = "GET", Headers = {{"Upgrade", "WebSocket"}, {"Sec-WebSocket-Key1", "BLAH"}, {"Sec-WebSocket-Key2", "BLAH"}}};

            var ex = Assert.Throws<WebSocketException>(() => HandlerFactory.BuildHandler(request, x => {}, () => {}, x => { }, x => { }, x => { }));
            Assert.AreEqual(WebSocketStatusCodes.UnsupportedDataType, ex.StatusCode);
        }

        [Test]
        public void ShouldRefuseFlashPolicyRequest()
        {
            var request = new WebSocketHttpRequest {Body = "<policy-file-request/>"};

            var ex = Assert.Throws<WebSocketException>(() => HandlerFactory.BuildHandler(request, x => {}, () => {}, x => { }, x => { }, x => { }));
            Assert.AreEqual(WebSocketStatusCodes.UnsupportedDataType, ex.StatusCode);
        }
        
        [Test]
        public void ShouldThrowWhenUnsupportedType()
        {
            
            var request = new WebSocketHttpRequest {Headers = {{"Bad", "Request"}}};
            Assert.Throws<WebSocketException>(() => HandlerFactory.BuildHandler(request, x => {}, () => {}, x => { }, x => { }, x => { }));
            
        }
    }
}

