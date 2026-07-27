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
            // Return a simple HTTP 204 No Content response with empty body
            var response = "HTTP/1.1 204 No Content\r\n" +
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
