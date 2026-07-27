using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;

namespace Fleck
{
  public class WebSocketConnection : IWebSocketConnection
  {
    public WebSocketConnection(ISocket socket, Action<IWebSocketConnection> initialize, Func<byte[], WebSocketHttpRequest> parseRequest, Func<WebSocketHttpRequest, IHandler> handlerFactory, Func<IEnumerable<string>, string> negotiateSubProtocol)
    {
      Socket = socket;
      OnOpen = () => { };
      OnClose = () => { };
      OnMessage = x => { };
      OnBinary = x => { };
      OnPing = x => SendPong(x);
      OnPong = x => { };
      OnError = x => { };
      _initialize = initialize;
      _handlerFactory = handlerFactory;
      _parseRequest = parseRequest;
      _negotiateSubProtocol = negotiateSubProtocol;
    }

    public ISocket Socket { get; set; }

    private readonly Action<IWebSocketConnection> _initialize;
    private readonly Func<WebSocketHttpRequest, IHandler> _handlerFactory;
    private readonly Func<IEnumerable<string>, string> _negotiateSubProtocol;
    readonly Func<byte[], WebSocketHttpRequest> _parseRequest;

    public IHandler Handler { get; set; }

    private bool _closing;
    private bool _closed;
    // 32KB (was 4KB): multi-MB webcam frames arrived in ~1000 reads each, paying
    // the callback + append machinery per read; 8x fewer iterations for ~28KB
    // more steady-state memory per connection (fork, 2026-07-27)
    private const int ReadSize = 1024 * 32;

    public Action OnOpen { get; set; }

    public Action OnClose { get; set; }

    public Action<string> OnMessage { get; set; }

    public Action<byte[]> OnBinary { get; set; }

    public Action<byte[]> OnPing { get; set; }

    public Action<byte[]> OnPong { get; set; }

    public Action<Exception> OnError { get; set; }

    public IWebSocketConnectionInfo ConnectionInfo { get; private set; }

    public bool IsAvailable {
      get { return !_closing && !_closed && Socket.Connected; }
    }

    public Task Send(string message)
    {
      return Send(message, Handler.FrameText);
    }

    public Task Send(byte[] message)
    {
        return Send(message, Handler.FrameBinary);
    }

    public Task SendPing(byte[] message)
    {
        return Send(message, Handler.FramePing);
    }

    public Task SendPong(byte[] message)
    {
        return Send(message, Handler.FramePong);
    }

    private Task Send<T>(T message, Func<T, byte[]> createFrame)
    {
      if (Handler == null)
        throw new InvalidOperationException("Cannot send before handshake");

      if (!IsAvailable)
      {
          const string errorMessage = "Data sent while closing or after close. Ignoring.";
          FleckLog.Warn(errorMessage);
          
          var taskForException = new TaskCompletionSource<object>();
          taskForException.SetException(new ConnectionNotAvailableException(errorMessage));
          return taskForException.Task;
      }

      var bytes = createFrame(message);
      return SendBytes(bytes);
    }

    public void StartReceiving()
    {
      var data = new List<byte>(ReadSize);
      var buffer = new byte[ReadSize];
      Read(data, buffer);
    }

    public void Close()
    {
      Close(WebSocketStatusCodes.NormalClosure);
    }

    public void Close(int code)
    {
      if (!IsAvailable)
        return;

      _closing = true;

      if (Handler == null) {
        CloseSocket();
        return;
      }

      var bytes = Handler.FrameClose(code);
      if (bytes.Length == 0)
        CloseSocket();
      else
        SendBytes(bytes, CloseSocket);
    }

    public void CreateHandler(IEnumerable<byte> data)
    {
      var request = _parseRequest(data.ToArray());
      if (request == null)
        return;
      Handler = _handlerFactory(request);
      if (Handler == null)
        return;

      // plain http (health probes, browsers hitting the endpoint): answer and
      // actively close - rfc 9112 requires the server to initiate closure after
      // a "Connection: close" response, and leaving the socket open would park
      // it in a read loop that silently discards everything (Receive no-ops).
      // the websocket lifecycle (_initialize / OnOpen) is deliberately skipped
      // so a probe never spins up per-connection application state.
      // ShutdownSend (TCP half-close), NOT CloseSocket: an immediate
      // Close/Dispose races the in-flight response with a pending read armed
      // and RSTs the bytes away; FIN-after-flush lets the peer's close drive
      // the normal read-0 teardown.
      if (Handler is Fleck.Handlers.HttpGetHandler)
      {
        SendBytes(Handler.CreateHandshake(null), Socket.ShutdownSend);
        return;
      }

      var subProtocol = _negotiateSubProtocol(request.SubProtocols);
      ConnectionInfo = WebSocketConnectionInfo.Create(request, Socket.RemoteIpAddress, Socket.RemotePort, subProtocol);

      _initialize(this);

      var handshake = Handler.CreateHandshake(subProtocol);
      SendBytes(handshake, OnOpen);
    }

    private void Read(List<byte> data, byte[] buffer)
    {
      if (!IsAvailable)
        return;

      Socket.Receive(buffer, r =>
      {
        if (r <= 0) {
          FleckLog.Debug("0 bytes read. Closing.");
          CloseSocket();
          return;
        }
        FleckLog.Debug(r + " bytes read");
        if (Handler != null) {
          // post-handshake hot path: hand the raw buffer over, no per-byte
          // enumerator copy (fork, 2026-07-27)
          Handler.Receive(buffer, r);
        } else {
          // pre-handshake only - one small http request per connection
          for (var i = 0; i < r; i++)
            data.Add(buffer[i]);
          CreateHandler(data);
        }

        Read(data, buffer);
      },
      HandleReadError);
    }

    private void HandleReadError(Exception e)
    {
      if (e is AggregateException) {
        var agg = e as AggregateException;
        HandleReadError(agg.InnerException);
        return;
      }

      if (e is ObjectDisposedException) {
        FleckLog.Debug("Swallowing ObjectDisposedException", e);
        return;
      }

      OnError(e);

      if (e is WebSocketException) {
        FleckLog.Debug("Error while reading", e);
        Close(((WebSocketException)e).StatusCode);
      } else if (e is SubProtocolNegotiationFailureException) {
        FleckLog.Debug(e.Message);
        Close(WebSocketStatusCodes.ProtocolError);
      } else if (e is IOException) {
        FleckLog.Debug("Error while reading", e);
        Close(WebSocketStatusCodes.AbnormalClosure);
      } else {
        FleckLog.Error("Application Error", e);
        Close(WebSocketStatusCodes.InternalServerError);
      }
    }

    private Task SendBytes(byte[] bytes, Action callback = null)
    {
      return Socket.Send(bytes, () =>
      {
        FleckLog.Debug("Sent " + bytes.Length + " bytes");
        if (callback != null)
          callback();
      },
                        e =>
      {
        if (e is IOException)
          FleckLog.Debug("Failed to send. Disconnecting.", e);
        else
          FleckLog.Info("Failed to send. Disconnecting.", e);
        CloseSocket();
      });
    }

    private void CloseSocket()
    {
      _closing = true;
      OnClose();
      _closed = true;
      Socket.Close();
      Socket.Dispose();
      _closing = false;
    }

  }
}
