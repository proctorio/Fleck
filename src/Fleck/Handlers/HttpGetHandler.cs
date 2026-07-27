using System;
using System.Collections.Generic;
using System.Text;

namespace Fleck.Handlers
{
    public class HttpGetHandler : IHandler
    {
        public static HttpGetHandler Create(WebSocketHttpRequest request)
        {
            return new HttpGetHandler();
        }

        public byte[] CreateHandshake(string subProtocol = null)
        {
            // 200 (not 204): Azure LB HTTP(S) health probes mark an instance DOWN
            // for any status other than 200, and RFC 9110 forbids Content-Length
            // on a 204. Content-Length: 0 on a 200 is valid framing for GET and
            // HEAD alike. The connection is closed by the server right after
            // this is sent (see WebSocketConnection.CreateHandler).
            var response = "HTTP/1.1 200 OK\r\n" +
                          "Content-Length: 0\r\n" +
                          "Connection: close\r\n" +
                          "\r\n";

            return Encoding.UTF8.GetBytes(response);
        }

        public void Receive(IEnumerable<byte> data)
        {
            // HTTP is request-response, no additional data expected
            // Do nothing
        }

        public byte[] FrameText(string text)
        {
            // Not used for HTTP
            return new byte[0];
        }

        public byte[] FrameBinary(byte[] payload)
        {
            // Not used for HTTP
            return new byte[0];
        }

        public byte[] FramePing(byte[] payload)
        {
            // Not used for HTTP
            return new byte[0];
        }

        public byte[] FramePong(byte[] payload)
        {
            // Not used for HTTP
            return new byte[0];
        }

        public byte[] FrameClose(int code)
        {
            // Just return empty, socket will be closed anyway
            return new byte[0];
        }
    }
}
