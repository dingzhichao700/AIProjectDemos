using System;
using System.IO;

/// <summary>
/// 外层帧：[ZigZag varint 长度][ZigZag varint opIndex][大端 short 协议号][消息体]。
/// 长度含 opIndex、协议号和消息体。
/// </summary>
public static class NetFrame {

    public static byte[] Encode(int opIndex, short protocolId, byte[] body) {
        NetWriter payload = new NetWriter();
        payload.WriteInt(opIndex);
        payload.WriteShort(protocolId);
        payload.WriteBytes(body);
        byte[] payloadBytes = payload.ToArray();

        NetWriter frame = new NetWriter();
        frame.WriteInt(payloadBytes.Length);
        frame.WriteBytes(payloadBytes);
        return frame.ToArray();
    }

    /// <summary>
    /// 从流中读满一帧。流在帧开始处结束时返回 false。
    /// </summary>
    public static bool TryRead(Stream input, int maxFrameSize, out int opIndex, out short protocolId, out byte[] body) {
        opIndex = 0;
        protocolId = 0;
        body = null;
        int first = input.ReadByte();
        if (first < 0) {
            return false;
        }

        byte[] lengthBytes = ReadVarintTail(input, (byte)first, 5);
        int length = DecodeZigZag32(ToRawVarint32(lengthBytes));
        if (length < 1 || length > maxFrameSize) {
            throw new InvalidDataException("帧长度超出范围: " + length);
        }

        byte[] payload = ReadExact(input, length);
        NetReader reader = new NetReader(payload);
        opIndex = reader.ReadInt();
        protocolId = reader.ReadShort();
        body = new byte[reader.Remaining];
        for (int i = 0; i < body.Length; i++) {
            body[i] = reader.ReadByte();
        }
        return true;
    }

    static byte[] ReadVarintTail(Stream input, byte first, int maxBytes) {
        byte[] bytes = new byte[maxBytes];
        bytes[0] = first;
        int count = 1;
        while ((first & 0x80) != 0) {
            if (count >= maxBytes) {
                throw new InvalidDataException("长度 varint 过长");
            }
            int next = input.ReadByte();
            if (next < 0) {
                throw new EndOfStreamException("长度 varint 中断");
            }
            first = (byte)next;
            bytes[count++] = first;
        }
        byte[] exact = new byte[count];
        Buffer.BlockCopy(bytes, 0, exact, 0, count);
        return exact;
    }

    static int ToRawVarint32(byte[] bytes) {
        uint result = 0;
        int shift = 0;
        for (int i = 0; i < bytes.Length; i++) {
            result |= (uint)(bytes[i] & 0x7F) << shift;
            shift += 7;
        }
        return (int)result;
    }

    static int DecodeZigZag32(int value) {
        return (int)((uint)value >> 1) ^ -(value & 1);
    }

    static byte[] ReadExact(Stream input, int length) {
        byte[] buffer = new byte[length];
        int read = 0;
        while (read < length) {
            int count = input.Read(buffer, read, length - read);
            if (count <= 0) {
                throw new EndOfStreamException("帧体中断");
            }
            read += count;
        }
        return buffer;
    }

}
