using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Fleck
{
    public interface ISocket
    {
        bool Connected { get; }
        string RemoteIpAddress { get; }
        int RemotePort { get; }
        Stream Stream { get; }
        bool NoDelay { get; set; }
        EndPoint LocalEndPoint { get; }

        Task<ISocket> Accept(Action<ISocket> callback, Action<Exception> error);
        Task Send(byte[] buffer, Action callback, Action<Exception> error);
        Task<int> Receive(byte[] buffer, Action<int> callback, Action<Exception> error, int offset = 0);
        Task Authenticate(X509Certificate2 certificate, SslProtocols enabledSslProtocols, Action callback, Action<Exception> error);

        void Dispose();
        void Close();

        /// <summary>
        /// TCP half-close: flush queued outbound data and send FIN, leaving the
        /// receive side open so the peer's close completes the normal read-0
        /// teardown. Used after plain-HTTP responses, where an immediate
        /// Close/Dispose races the in-flight send and can RST the response away.
        /// </summary>
        void ShutdownSend();

        void Bind(EndPoint ipLocal);
        void Listen(int backlog);
    }
}
