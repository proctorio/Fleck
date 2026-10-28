using System;
using Fleck.Handlers;

namespace Fleck
{
    public class HandlerFactory
    {
        public static IHandler BuildHandler(WebSocketHttpRequest request, Action<string> onMessage, Action onClose, Action<byte[]> onBinary, Action<byte[]> onPing, Action<byte[]> onPong)
        {
            var version = GetVersion(request);
            
            // fork note: Draft76/Hixie ("76") and the Flash socket-policy handler
            // were removed 2026-07-27 - dead protocols (pre-2012 browsers, Flash),
            // pure attack surface; the policy handler even served an allow-all
            // cross-domain policy. Anything that isn't Hybi-07/08/13 or plain
            // http now falls through to the UnsupportedDataType close below.
            switch (version)
            {
                case "7":
                case "8":
                case "13":
                    return Hybi13Handler.Create(request, onMessage, onClose, onBinary, onPing, onPong);
                case "http-get":
                    return HttpGetHandler.Create(request);
            }

            throw new WebSocketException(WebSocketStatusCodes.UnsupportedDataType);
        }

        public static string GetVersion(WebSocketHttpRequest request)
        {
            string version;
            if (request.Headers.TryGetValue("Sec-WebSocket-Version", out version))
                return version;

            if (request.Headers.TryGetValue("Sec-WebSocket-Draft", out version))
                return version;

            // Check if it's a regular HTTP request (no WebSocket upgrade).
            // HEAD is included: it is GET-without-body and health checkers use it;
            // the empty-body 200 this maps to is valid for both.
            if ((request.Method == "GET" || request.Method == "HEAD") && !request.Headers.ContainsKey("Upgrade"))
                return "http-get";

            return "75";
        }
    }
}

