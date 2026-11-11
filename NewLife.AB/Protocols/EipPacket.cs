namespace NewLife.AB.Protocols;

/// <summary>EtherNet/IP 封装报文。固定 24 字节头加命令数据，多字节字段为小端。</summary>
public sealed class EipPacket
{
    /// <summary>封装头长度</summary>
    public const Int32 HeaderSize = 24;

    /// <summary>命令</summary>
    public UInt16 Command { get; set; }

    /// <summary>会话句柄</summary>
    public UInt32 SessionHandle { get; set; }

    /// <summary>状态</summary>
    public UInt32 Status { get; set; }

    /// <summary>发送方上下文，应答原样带回</summary>
    public UInt64 SenderContext { get; set; }

    /// <summary>选项，请求中为 0</summary>
    public UInt32 Options { get; set; }

    /// <summary>命令数据</summary>
    public Byte[] Data { get; set; } = new Byte[0];

    /// <summary>序列化为完整帧</summary>
    /// <returns>字节</returns>
    public Byte[] ToBytes()
    {
        var data = Data ?? new Byte[0];
        if (data.Length > UInt16.MaxValue) throw new InvalidOperationException("封装数据超过 65535 字节");

        var buffer = new Byte[HeaderSize + data.Length];
        BinaryBuffer.WriteUInt16(buffer, 0, Command);
        BinaryBuffer.WriteUInt16(buffer, 2, (UInt16)data.Length);
        BinaryBuffer.WriteUInt32(buffer, 4, SessionHandle);
        BinaryBuffer.WriteUInt32(buffer, 8, Status);
        BinaryBuffer.WriteUInt32(buffer, 12, (UInt32)SenderContext);
        BinaryBuffer.WriteUInt32(buffer, 16, (UInt32)(SenderContext >> 32));
        BinaryBuffer.WriteUInt32(buffer, 20, Options);
        if (data.Length > 0) Buffer.BlockCopy(data, 0, buffer, HeaderSize, data.Length);
        return buffer;
    }

    /// <summary>从完整帧解析</summary>
    /// <param name="buffer">完整帧</param>
    /// <returns>报文</returns>
    public static EipPacket Parse(Byte[] buffer)
    {
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        if (buffer.Length < HeaderSize) throw new InvalidOperationException("封装头不足 24 字节");

        var length = BinaryBuffer.ReadUInt16(buffer, 2);
        if (buffer.Length < HeaderSize + length) throw new InvalidOperationException("封装数据不完整");

        var data = new Byte[length];
        if (length > 0) Buffer.BlockCopy(buffer, HeaderSize, data, 0, length);
        return FromHeader(buffer, data);
    }

    /// <summary>用已拆开的头和数据解析</summary>
    /// <param name="header">24 字节头</param>
    /// <param name="data">命令数据</param>
    /// <returns>报文</returns>
    public static EipPacket FromHeader(Byte[] header, Byte[] data)
    {
        if (header == null || header.Length < HeaderSize) throw new InvalidOperationException("封装头不足 24 字节");
        var length = BinaryBuffer.ReadUInt16(header, 2);
        if ((data == null ? 0 : data.Length) != length) throw new InvalidOperationException("封装长度与数据不符");

        return new EipPacket
        {
            Command = BinaryBuffer.ReadUInt16(header, 0),
            SessionHandle = BinaryBuffer.ReadUInt32(header, 4),
            Status = BinaryBuffer.ReadUInt32(header, 8),
            SenderContext = BinaryBuffer.ReadUInt32(header, 12) | ((UInt64)BinaryBuffer.ReadUInt32(header, 16) << 32),
            Options = BinaryBuffer.ReadUInt32(header, 20),
            Data = data ?? new Byte[0]
        };
    }

    /// <summary>读取头中的数据长度</summary>
    /// <param name="header">至少 4 字节</param>
    /// <returns>数据长度</returns>
    public static Int32 ReadDataLength(Byte[] header)
    {
        if (header == null || header.Length < 4) throw new InvalidOperationException("封装头不足");
        return BinaryBuffer.ReadUInt16(header, 2);
    }
}
