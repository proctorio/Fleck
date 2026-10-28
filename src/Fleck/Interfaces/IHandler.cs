using System.Collections.Generic;

namespace Fleck
{
    public interface IHandler
    {
        byte[] CreateHandshake(string subProtocol = null);
        // fork 2026-07-27: byte[]+count instead of IEnumerable<byte> - the old
        // shape forced a per-byte enumerator copy of every socket read
        void Receive(byte[] data, int count);
        byte[] FrameText(string text);
        byte[] FrameBinary(byte[] bytes);
        byte[] FramePing(byte[] bytes);
        byte[] FramePong(byte[] bytes);
        byte[] FrameClose(int code);
    }
}

