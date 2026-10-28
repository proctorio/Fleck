using System;

namespace Fleck
{
    public static class IntExtensions
    {
        public static byte[] ToBigEndianBytes<T>(this int source)
        {
            byte[] bytes;
            
            var type = typeof(T);
            if (type == typeof(ushort))
                bytes = BitConverter.GetBytes((ushort)source);
            else if (type == typeof(ulong))
                bytes = BitConverter.GetBytes((ulong)source);
            else if (type == typeof(int))
                bytes = BitConverter.GetBytes(source);
            else
                throw new InvalidCastException("Cannot be cast to T");
                
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return bytes;
        }

        public static int ToLittleEndianInt(this byte[] source)
        {
            if(BitConverter.IsLittleEndian)
                Array.Reverse(source);

            if(source.Length == 2)
                return BitConverter.ToUInt16(source, 0);

            if(source.Length == 8)
                return (int)BitConverter.ToUInt64(source, 0);

            throw new ArgumentException("Unsupported Size");
        }

        // fork 2026-07-27: lossless variant for wire lengths - the int cast above
        // truncated 64-bit extended payload lengths (2^32 became 0), letting a
        // hostile frame header desync the stream. values above long.MaxValue
        // surface as negative and are rejected by the caller's bounds check.
        public static long ToLittleEndianLong(this byte[] source)
        {
            if (BitConverter.IsLittleEndian)
                Array.Reverse(source);

            if (source.Length == 2)
                return BitConverter.ToUInt16(source, 0);

            if (source.Length == 8)
                return unchecked((long)BitConverter.ToUInt64(source, 0));

            throw new ArgumentException("Unsupported Size");
        }
    }
}

