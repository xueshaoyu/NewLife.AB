namespace NewLife.AB.Protocols;

/// <summary>通用包格式（Common Packet Format）。SendRRData / SendUnitData 的命令数据。</summary>
public static class EipCommonPacket
{
    /// <summary>空地址项，未连接消息使用</summary>
    public const UInt16 NullAddress = 0x0000;

    /// <summary>已连接地址项，内容为连接 ID</summary>
    public const UInt16 ConnectedAddress = 0x00A1;

    /// <summary>已连接数据项，前两字节为序号</summary>
    public const UInt16 ConnectedData = 0x00B1;

    /// <summary>未连接数据项</summary>
    public const UInt16 UnconnectedData = 0x00B2;

    /// <summary>ListIdentity 的身份项</summary>
    public const UInt16 IdentityItem = 0x000C;

    /// <summary>组装 SendRRData 命令数据</summary>
    /// <param name="cip">CIP 报文</param>
    /// <param name="timeoutSeconds">超时秒数</param>
    /// <returns>命令数据</returns>
    public static Byte[] BuildSendRRData(Byte[] cip, UInt16 timeoutSeconds)
    {
        cip = cip ?? new Byte[0];
        var writer = new ByteWriter();
        writer.U32(0);
        writer.U16(timeoutSeconds);
        writer.U16(2);
        writer.U16(NullAddress);
        writer.U16(0);
        writer.U16(UnconnectedData);
        writer.U16((UInt16)cip.Length);
        writer.Bytes(cip);
        return writer.ToArray();
    }

    /// <summary>组装 SendUnitData 命令数据</summary>
    /// <param name="connectionId">O→T 连接 ID</param>
    /// <param name="sequence">序号</param>
    /// <param name="cip">CIP 报文</param>
    /// <param name="timeoutSeconds">超时秒数</param>
    /// <returns>命令数据</returns>
    public static Byte[] BuildSendUnitData(UInt32 connectionId, UInt16 sequence, Byte[] cip, UInt16 timeoutSeconds)
    {
        cip = cip ?? new Byte[0];
        var writer = new ByteWriter();
        writer.U32(0);
        writer.U16(timeoutSeconds);
        writer.U16(2);
        writer.U16(ConnectedAddress);
        writer.U16(4);
        writer.U32(connectionId);
        writer.U16(ConnectedData);
        writer.U16((UInt16)(2 + cip.Length));
        writer.U16(sequence);
        writer.Bytes(cip);
        return writer.ToArray();
    }

    /// <summary>从命令数据中取出 CIP 字节</summary>
    /// <param name="data">SendRRData 或 SendUnitData 的数据</param>
    /// <param name="sequence">已连接数据项的序号，未连接时为 0</param>
    /// <returns>CIP 字节</returns>
    public static Byte[] ExtractCip(Byte[] data, out UInt16 sequence)
    {
        sequence = 0;
        if (data == null || data.Length < 8) throw new InvalidOperationException("通用包数据不足");

        var reader = new ByteReader(data);
        reader.U32();
        reader.U16();
        var count = reader.U16();
        Byte[] cip = null;
        for (var i = 0; i < count; i++)
        {
            if (!reader.Has(4)) throw new InvalidOperationException("通用包项不完整");
            var type = reader.U16();
            var length = reader.U16();
            if (!reader.Has(length)) throw new InvalidOperationException("通用包项长度超出报文");
            var item = reader.Bytes(length);
            if (type == UnconnectedData)
            {
                cip = item;
            }
            else if (type == ConnectedData)
            {
                if (item.Length < 2) throw new InvalidOperationException("已连接数据项过短");
                sequence = BinaryBuffer.ReadUInt16(item, 0);
                cip = new Byte[item.Length - 2];
                if (cip.Length > 0) Buffer.BlockCopy(item, 2, cip, 0, cip.Length);
            }
        }

        if (cip == null) throw new InvalidOperationException("通用包中没有 CIP 数据项");
        return cip;
    }
}
