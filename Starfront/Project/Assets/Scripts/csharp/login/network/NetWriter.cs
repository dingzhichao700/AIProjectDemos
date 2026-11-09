using System.IO;
using System.Text;

/// <summary>
/// 按服务端 ByteBufferUtil 写入标量。int、long 为 ZigZag varint，short 为大端。
/// </summary>
public sealed class NetWriter {

    readonly MemoryStream stream = new MemoryStream();

    public void WriteBool(bool value) {
        stream.WriteByte(value ? (byte)1 : (byte)0);
    }

    public void WriteInt(int value) {
        WriteRawVarint32(EncodeZigZag32(value));
    }

    public void WriteLong(long value) {
        WriteRawVarint64(EncodeZigZag64(value));
    }

    public void WriteShort(short value) {
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }

    /// <summary>
    /// null 与空串都写成长度 0。
    /// </summary>
    public void WriteString(string value) {
        if (string.IsNullOrEmpty(value)) {
            WriteInt(0);
            return;
        }
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        WriteInt(bytes.Length);
        stream.Write(bytes, 0, bytes.Length);
    }

    public void WriteBytes(byte[] value) {
        if (value == null || value.Length == 0) {
            return;
        }
        stream.Write(value, 0, value.Length);
    }

    public byte[] ToArray() {
        return stream.ToArray();
    }

    void WriteRawVarint32(int value) {
        uint unsigned = (uint)value;
        while (true) {
            if ((unsigned & ~0x7Fu) == 0) {
                stream.WriteByte((byte)unsigned);
                return;
            }
            stream.WriteByte((byte)((unsigned & 0x7Fu) | 0x80u));
            unsigned >>= 7;
        }
    }

    void WriteRawVarint64(long value) {
        ulong unsigned = (ulong)value;
        while (true) {
            if ((unsigned & ~0x7FUL) == 0) {
                stream.WriteByte((byte)unsigned);
                return;
            }
            stream.WriteByte((byte)((unsigned & 0x7FUL) | 0x80UL));
            unsigned >>= 7;
        }
    }

    static int EncodeZigZag32(int value) {
        return (value << 1) ^ (value >> 31);
    }

    static long EncodeZigZag64(long value) {
        return (value << 1) ^ (value >> 63);
    }

}
