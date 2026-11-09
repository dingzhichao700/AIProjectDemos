using System;
using System.IO;
using System.Text;

/// <summary>
/// 按服务端 ByteBufferUtil 读取标量。
/// </summary>
public sealed class NetReader {

    readonly byte[] data;
    int offset;

    public NetReader(byte[] data) {
        this.data = data ?? new byte[0];
    }

    public int Remaining {
        get { return data.Length - offset; }
    }

    public bool ReadBool() {
        return ReadByte() == 1;
    }

    public int ReadInt() {
        return DecodeZigZag32(ReadRawVarint32());
    }

    public long ReadLong() {
        return DecodeZigZag64(ReadRawVarint64());
    }

    public short ReadShort() {
        int high = ReadByte() & 0xFF;
        int low = ReadByte() & 0xFF;
        return (short)((high << 8) | low);
    }

    public string ReadString() {
        int length = ReadInt();
        if (length == 0) {
            return string.Empty;
        }
        if (length < 0 || length > Remaining) {
            throw new InvalidDataException("字符串长度超出剩余字节");
        }
        string value = Encoding.UTF8.GetString(data, offset, length);
        offset += length;
        return value;
    }

    public byte ReadByte() {
        if (offset >= data.Length) {
            throw new EndOfStreamException("协议字节已读完");
        }
        return data[offset++];
    }

    int ReadRawVarint32() {
        uint result = 0;
        int shift = 0;
        for (int i = 0; i < 5; i++) {
            byte current = ReadByte();
            result |= (uint)(current & 0x7F) << shift;
            if ((current & 0x80) == 0) {
                return (int)result;
            }
            shift += 7;
        }
        throw new InvalidDataException("varint32 过长");
    }

    long ReadRawVarint64() {
        ulong result = 0;
        int shift = 0;
        for (int i = 0; i < 10; i++) {
            byte current = ReadByte();
            result |= (ulong)(current & 0x7F) << shift;
            if ((current & 0x80) == 0) {
                return (long)result;
            }
            shift += 7;
        }
        throw new InvalidDataException("varint64 过长");
    }

    static int DecodeZigZag32(int value) {
        return (int)((uint)value >> 1) ^ -(value & 1);
    }

    static long DecodeZigZag64(long value) {
        return (long)((ulong)value >> 1) ^ -(value & 1L);
    }

}
