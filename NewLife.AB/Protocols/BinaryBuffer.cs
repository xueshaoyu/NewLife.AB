namespace NewLife.AB.Protocols;

/// <summary>小端字节读写。CIP 多字节整数按低字节在前传输，且不依赖 net45 没有的 BinaryPrimitives。</summary>
internal static class BinaryBuffer
{
    /// <summary>读取 UInt16</summary>
    /// <param name="buffer">缓冲区</param>
    /// <param name="offset">偏移</param>
    /// <returns>值</returns>
    public static UInt16 ReadUInt16(Byte[] buffer, Int32 offset) =>
        (UInt16)(buffer[offset] | (buffer[offset + 1] << 8));

    /// <summary>读取 UInt32</summary>
    /// <param name="buffer">缓冲区</param>
    /// <param name="offset">偏移</param>
    /// <returns>值</returns>
    public static UInt32 ReadUInt32(Byte[] buffer, Int32 offset) =>
        (UInt32)(buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16) | (buffer[offset + 3] << 24));

    /// <summary>读取 UInt64</summary>
    /// <param name="buffer">缓冲区</param>
    /// <param name="offset">偏移</param>
    /// <returns>值</returns>
    public static UInt64 ReadUInt64(Byte[] buffer, Int32 offset)
    {
        var lo = ReadUInt32(buffer, offset);
        var hi = ReadUInt32(buffer, offset + 4);
        return lo | ((UInt64)hi << 32);
    }

    /// <summary>写入 UInt16</summary>
    /// <param name="buffer">缓冲区</param>
    /// <param name="offset">偏移</param>
    /// <param name="value">值</param>
    public static void WriteUInt16(Byte[] buffer, Int32 offset, UInt16 value)
    {
        buffer[offset] = (Byte)value;
        buffer[offset + 1] = (Byte)(value >> 8);
    }

    /// <summary>写入 UInt32</summary>
    /// <param name="buffer">缓冲区</param>
    /// <param name="offset">偏移</param>
    /// <param name="value">值</param>
    public static void WriteUInt32(Byte[] buffer, Int32 offset, UInt32 value)
    {
        buffer[offset] = (Byte)value;
        buffer[offset + 1] = (Byte)(value >> 8);
        buffer[offset + 2] = (Byte)(value >> 16);
        buffer[offset + 3] = (Byte)(value >> 24);
    }
}

/// <summary>可增长的小端写入器</summary>
internal sealed class ByteWriter
{
    private Byte[] _buffer = new Byte[64];
    private Int32 _length;

    /// <summary>已写入长度</summary>
    public Int32 Length => _length;

    /// <summary>写入一个字节</summary>
    /// <param name="value">值</param>
    public void U8(Byte value)
    {
        Ensure(1);
        _buffer[_length++] = value;
    }

    /// <summary>写入 UInt16</summary>
    /// <param name="value">值</param>
    public void U16(UInt16 value)
    {
        Ensure(2);
        BinaryBuffer.WriteUInt16(_buffer, _length, value);
        _length += 2;
    }

    /// <summary>写入 UInt32</summary>
    /// <param name="value">值</param>
    public void U32(UInt32 value)
    {
        Ensure(4);
        BinaryBuffer.WriteUInt32(_buffer, _length, value);
        _length += 4;
    }

    /// <summary>写入 UInt64</summary>
    /// <param name="value">值</param>
    public void U64(UInt64 value)
    {
        U32((UInt32)value);
        U32((UInt32)(value >> 32));
    }

    /// <summary>写入字节块</summary>
    /// <param name="data">数据，允许 null</param>
    public void Bytes(Byte[] data)
    {
        if (data == null || data.Length == 0) return;
        Ensure(data.Length);
        Buffer.BlockCopy(data, 0, _buffer, _length, data.Length);
        _length += data.Length;
    }

    /// <summary>复制为数组</summary>
    /// <returns>已写入字节</returns>
    public Byte[] ToArray()
    {
        var data = new Byte[_length];
        if (_length > 0) Buffer.BlockCopy(_buffer, 0, data, 0, _length);
        return data;
    }

    private void Ensure(Int32 count)
    {
        var need = _length + count;
        if (need <= _buffer.Length) return;
        var size = _buffer.Length;
        while (size < need) size *= 2;
        var next = new Byte[size];
        Buffer.BlockCopy(_buffer, 0, next, 0, _length);
        _buffer = next;
    }
}

/// <summary>顺序小端读取器</summary>
internal sealed class ByteReader
{
    private readonly Byte[] _buffer;
    private Int32 _offset;

    /// <summary>实例化</summary>
    /// <param name="buffer">缓冲区</param>
    public ByteReader(Byte[] buffer)
    {
        _buffer = buffer ?? new Byte[0];
    }

    /// <summary>剩余字节数</summary>
    public Int32 Remaining => _buffer.Length - _offset;

    /// <summary>当前位置</summary>
    public Int32 Offset => _offset;

    /// <summary>是否还有字节</summary>
    /// <param name="count">需要的字节数</param>
    /// <returns>是否足够</returns>
    public Boolean Has(Int32 count) => Remaining >= count;

    /// <summary>读取一个字节</summary>
    /// <returns>值</returns>
    public Byte U8()
    {
        Ensure(1);
        return _buffer[_offset++];
    }

    /// <summary>读取 UInt16</summary>
    /// <returns>值</returns>
    public UInt16 U16()
    {
        Ensure(2);
        var value = BinaryBuffer.ReadUInt16(_buffer, _offset);
        _offset += 2;
        return value;
    }

    /// <summary>读取 UInt32</summary>
    /// <returns>值</returns>
    public UInt32 U32()
    {
        Ensure(4);
        var value = BinaryBuffer.ReadUInt32(_buffer, _offset);
        _offset += 4;
        return value;
    }

    /// <summary>读取指定长度</summary>
    /// <param name="count">长度</param>
    /// <returns>副本</returns>
    public Byte[] Bytes(Int32 count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        Ensure(count);
        var data = new Byte[count];
        if (count > 0) Buffer.BlockCopy(_buffer, _offset, data, 0, count);
        _offset += count;
        return data;
    }

    /// <summary>跳过字节</summary>
    /// <param name="count">长度</param>
    public void Skip(Int32 count)
    {
        Ensure(count);
        _offset += count;
    }

    private void Ensure(Int32 count)
    {
        if (!Has(count)) throw new InvalidOperationException("报文长度不足");
    }
}
