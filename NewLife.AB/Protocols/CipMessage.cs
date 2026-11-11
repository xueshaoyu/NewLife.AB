namespace NewLife.AB.Protocols;

/// <summary>CIP 显式报文（Message Router 请求 / 应答）</summary>
public sealed class CipMessage
{
    /// <summary>服务码。应答中含 0x80。</summary>
    public Byte Service { get; set; }

    /// <summary>请求路径</summary>
    public Byte[] Path { get; set; } = new Byte[0];

    /// <summary>服务数据。应答中为 Reply Data。</summary>
    public Byte[] Data { get; set; } = new Byte[0];

    /// <summary>通用状态。仅应答有意义。</summary>
    public Byte GeneralStatus { get; set; }

    /// <summary>扩展状态</summary>
    public UInt16[] ExtendedStatus { get; set; } = new UInt16[0];

    /// <summary>通用状态是否为成功</summary>
    public Boolean IsSuccess => GeneralStatus == CipStatus.Success;

    /// <summary>序列化请求</summary>
    /// <returns>字节</returns>
    public Byte[] ToRequestBytes()
    {
        var path = Path ?? new Byte[0];
        var data = Data ?? new Byte[0];
        var writer = new ByteWriter();
        writer.U8(Service);
        writer.U8(CipPath.SizeInWords(path));
        writer.Bytes(path);
        writer.Bytes(data);
        return writer.ToArray();
    }

    /// <summary>序列化成功或失败应答</summary>
    /// <returns>字节</returns>
    public Byte[] ToResponseBytes()
    {
        var ext = ExtendedStatus ?? new UInt16[0];
        var data = Data ?? new Byte[0];
        var writer = new ByteWriter();
        writer.U8((Byte)(Service | CipService.ReplyFlag));
        writer.U8(0);
        writer.U8(GeneralStatus);
        writer.U8((Byte)ext.Length);
        foreach (var item in ext)
        {
            writer.U16(item);
        }
        writer.Bytes(data);
        return writer.ToArray();
    }

    /// <summary>解析请求</summary>
    /// <param name="buffer">字节</param>
    /// <returns>请求</returns>
    public static CipMessage ParseRequest(Byte[] buffer)
    {
        if (buffer == null || buffer.Length < 2) throw new InvalidOperationException("CIP 请求过短");
        var words = buffer[1];
        var pathLength = words * 2;
        if (buffer.Length < 2 + pathLength) throw new InvalidOperationException("CIP 请求路径不完整");

        var path = new Byte[pathLength];
        if (pathLength > 0) Buffer.BlockCopy(buffer, 2, path, 0, pathLength);
        var dataLength = buffer.Length - 2 - pathLength;
        var data = new Byte[dataLength];
        if (dataLength > 0) Buffer.BlockCopy(buffer, 2 + pathLength, data, 0, dataLength);

        return new CipMessage
        {
            Service = buffer[0],
            Path = path,
            Data = data
        };
    }

    /// <summary>解析应答</summary>
    /// <param name="buffer">字节</param>
    /// <returns>应答</returns>
    public static CipMessage ParseResponse(Byte[] buffer)
    {
        if (buffer == null || buffer.Length < 4) throw new InvalidOperationException("CIP 应答过短");
        var extCount = buffer[3];
        var header = 4 + extCount * 2;
        if (buffer.Length < header) throw new InvalidOperationException("CIP 扩展状态不完整");

        var ext = new UInt16[extCount];
        for (var i = 0; i < extCount; i++)
        {
            ext[i] = BinaryBuffer.ReadUInt16(buffer, 4 + i * 2);
        }

        var dataLength = buffer.Length - header;
        var data = new Byte[dataLength];
        if (dataLength > 0) Buffer.BlockCopy(buffer, header, data, 0, dataLength);

        return new CipMessage
        {
            Service = buffer[0],
            GeneralStatus = buffer[2],
            ExtendedStatus = ext,
            Data = data
        };
    }

    /// <summary>构造读标签请求</summary>
    /// <param name="path">符号路径</param>
    /// <param name="count">元素个数</param>
    /// <returns>请求</returns>
    public static CipMessage ReadTag(Byte[] path, UInt16 count)
    {
        var data = new Byte[2];
        BinaryBuffer.WriteUInt16(data, 0, count);
        return new CipMessage { Service = CipService.ReadTag, Path = path, Data = data };
    }

    /// <summary>构造写标签请求。结构类型会在类型码后附加结构句柄。</summary>
    /// <param name="path">符号路径</param>
    /// <param name="typeCode">类型码</param>
    /// <param name="structureHandle">结构句柄，仅结构类型使用</param>
    /// <param name="count">元素个数</param>
    /// <param name="payload">元素数据</param>
    /// <returns>请求</returns>
    public static CipMessage WriteTag(Byte[] path, UInt16 typeCode, UInt16 structureHandle, UInt16 count, Byte[] payload)
    {
        var writer = new ByteWriter();
        writer.U16(typeCode);
        if (CipDataType.IsStructure(typeCode)) writer.U16(structureHandle);
        writer.U16(count);
        writer.Bytes(payload);
        return new CipMessage { Service = CipService.WriteTag, Path = path, Data = writer.ToArray() };
    }

    /// <summary>构造分段读请求</summary>
    /// <param name="path">符号路径</param>
    /// <param name="count">元素个数</param>
    /// <param name="byteOffset">数据字节偏移</param>
    /// <returns>请求</returns>
    public static CipMessage ReadTagFragmented(Byte[] path, UInt16 count, UInt32 byteOffset)
    {
        var writer = new ByteWriter();
        writer.U16(count);
        writer.U32(byteOffset);
        return new CipMessage { Service = CipService.ReadTagFragmented, Path = path, Data = writer.ToArray() };
    }

    /// <summary>构造分段写请求</summary>
    /// <param name="path">符号路径</param>
    /// <param name="typeCode">类型码</param>
    /// <param name="structureHandle">结构句柄</param>
    /// <param name="count">元素总个数</param>
    /// <param name="byteOffset">数据字节偏移</param>
    /// <param name="payload">本片数据</param>
    /// <returns>请求</returns>
    public static CipMessage WriteTagFragmented(Byte[] path, UInt16 typeCode, UInt16 structureHandle, UInt16 count, UInt32 byteOffset, Byte[] payload)
    {
        var writer = new ByteWriter();
        writer.U16(typeCode);
        if (CipDataType.IsStructure(typeCode)) writer.U16(structureHandle);
        writer.U16(count);
        writer.U32(byteOffset);
        writer.Bytes(payload);
        return new CipMessage { Service = CipService.WriteTagFragmented, Path = path, Data = writer.ToArray() };
    }

    /// <summary>构造读改写请求。掩码为小端，位 0 在首字节最低位。</summary>
    /// <param name="path">符号路径</param>
    /// <param name="orMask">OR 掩码，置 1 的位被置位</param>
    /// <param name="andMask">AND 掩码，置 0 的位被清零</param>
    /// <returns>请求</returns>
    public static CipMessage ReadModifyWrite(Byte[] path, Byte[] orMask, Byte[] andMask)
    {
        if (orMask == null) throw new ArgumentNullException(nameof(orMask));
        if (andMask == null) throw new ArgumentNullException(nameof(andMask));
        if (orMask.Length != andMask.Length) throw new ArgumentException("OR 与 AND 掩码长度必须相同");
        if (orMask.Length != 1 && orMask.Length != 2 && orMask.Length != 4 && orMask.Length != 8 && orMask.Length != 12)
            throw new ArgumentException("掩码长度只接受 1、2、4、8、12");

        var writer = new ByteWriter();
        writer.U16((UInt16)orMask.Length);
        writer.Bytes(orMask);
        writer.Bytes(andMask);
        return new CipMessage { Service = CipService.ReadModifyWriteTag, Path = path, Data = writer.ToArray() };
    }

    /// <summary>为位读写生成掩码。与公开示例一致：置位 bit2、清零 bit5 的 DINT 为 OR=04 00 00 00、AND=DF FF FF FF。</summary>
    /// <param name="byteSize">元素字节数</param>
    /// <param name="bit">位号</param>
    /// <param name="value">目标电平</param>
    /// <param name="orMask">OR 掩码</param>
    /// <param name="andMask">AND 掩码</param>
    public static void BuildBitMasks(Int32 byteSize, Int32 bit, Boolean value, out Byte[] orMask, out Byte[] andMask)
    {
        if (bit < 0 || bit >= byteSize * 8) throw new ArgumentOutOfRangeException(nameof(bit), "位号超出元素宽度");
        orMask = new Byte[byteSize];
        andMask = new Byte[byteSize];
        for (var i = 0; i < byteSize; i++) andMask[i] = 0xFF;

        var index = bit / 8;
        var mask = (Byte)(1 << (bit % 8));
        if (value) orMask[index] = mask;
        else andMask[index] = (Byte)~mask;
    }

    /// <summary>把多个 CIP 请求打成 Multiple Service Packet</summary>
    /// <param name="requests">子请求</param>
    /// <returns>请求</returns>
    public static CipMessage Multiple(Byte[][] requests)
    {
        if (requests == null || requests.Length == 0) throw new ArgumentException("至少需要一个子服务", nameof(requests));
        if (requests.Length > UInt16.MaxValue) throw new ArgumentException("子服务过多", nameof(requests));

        var offset = 2 + 2 * requests.Length;
        var writer = new ByteWriter();
        writer.U16((UInt16)requests.Length);
        foreach (var request in requests)
        {
            writer.U16((UInt16)offset);
            offset += request.Length;
        }
        foreach (var request in requests)
        {
            writer.Bytes(request);
        }

        return new CipMessage
        {
            Service = CipService.MultipleServicePacket,
            Path = CipPath.ClassInstance(CipService.ClassMessageRouter, 1),
            Data = writer.ToArray()
        };
    }

    /// <summary>拆开多服务应答数据</summary>
    /// <param name="replyData">应答数据</param>
    /// <returns>子应答</returns>
    public static CipMessage[] ParseMultiple(Byte[] replyData)
    {
        if (replyData == null || replyData.Length < 2) throw new InvalidOperationException("多服务应答过短");
        var count = BinaryBuffer.ReadUInt16(replyData, 0);
        if (replyData.Length < 2 + count * 2) throw new InvalidOperationException("多服务偏移不完整");

        var messages = new CipMessage[count];
        for (var i = 0; i < count; i++)
        {
            var start = BinaryBuffer.ReadUInt16(replyData, 2 + i * 2);
            var end = i + 1 < count ? BinaryBuffer.ReadUInt16(replyData, 2 + (i + 1) * 2) : replyData.Length;
            if (start > end || end > replyData.Length) throw new InvalidOperationException("多服务偏移无效");
            var slice = new Byte[end - start];
            if (slice.Length > 0) Buffer.BlockCopy(replyData, start, slice, 0, slice.Length);
            messages[i] = ParseResponse(slice);
        }
        return messages;
    }

    /// <summary>Unconnected Send。把嵌入请求经路由路径发到背板槽位。</summary>
    /// <param name="embedded">嵌入的 CIP 请求</param>
    /// <param name="routePath">路由路径</param>
    /// <param name="priorityTimeTick">优先级与时间片。默认 0x07，时间片 128ms。</param>
    /// <param name="timeoutTicks">超时片数。默认 0x10，与 0x07 合为约 2 秒。</param>
    /// <returns>请求</returns>
    public static CipMessage BuildUnconnectedSend(Byte[] embedded, Byte[] routePath, Byte priorityTimeTick, Byte timeoutTicks)
    {
        if (embedded == null) throw new ArgumentNullException(nameof(embedded));
        routePath = routePath ?? new Byte[0];
        if ((routePath.Length & 1) == 1) throw new ArgumentException("路由路径必须按字对齐", nameof(routePath));

        var writer = new ByteWriter();
        writer.U8(priorityTimeTick);
        writer.U8(timeoutTicks);
        writer.U16((UInt16)embedded.Length);
        writer.Bytes(embedded);
        if ((embedded.Length & 1) == 1) writer.U8(0);
        writer.U8((Byte)(routePath.Length / 2));
        writer.U8(0);
        writer.Bytes(routePath);

        return new CipMessage
        {
            Service = CipService.UnconnectedSend,
            Path = CipPath.ClassInstance(CipService.ClassConnectionManager, 1),
            Data = writer.ToArray()
        };
    }

    /// <summary>从 Unconnected Send 请求数据中取出嵌入报文</summary>
    /// <param name="data">服务数据</param>
    /// <param name="routePath">路由路径</param>
    /// <returns>嵌入请求</returns>
    public static Byte[] ParseUnconnectedSend(Byte[] data, out Byte[] routePath)
    {
        var reader = new ByteReader(data);
        if (!reader.Has(4)) throw new InvalidOperationException("Unconnected Send 数据不足");
        reader.U8();
        reader.U8();
        var size = reader.U16();
        var embedded = reader.Bytes(size);
        if ((size & 1) == 1)
        {
            if (!reader.Has(1)) throw new InvalidOperationException("Unconnected Send 缺少对齐字节");
            reader.U8();
        }
        if (!reader.Has(2)) throw new InvalidOperationException("Unconnected Send 缺少路由");
        var words = reader.U8();
        reader.U8();
        routePath = reader.Bytes(words * 2);
        return embedded;
    }

    /// <summary>Forward Open（服务 0x54）。连接参数为 16 位，适合不超过 511 字节的显式连接。</summary>
    /// <param name="o2tConnectionId">O→T 连接 ID</param>
    /// <param name="t2oConnectionId">T→O 连接 ID</param>
    /// <param name="connectionSerial">连接序列号</param>
    /// <param name="vendorId">发起方厂商号，只用于连接匹配</param>
    /// <param name="originatorSerial">发起方序列号</param>
    /// <param name="rpiMicroseconds">请求包间隔，微秒</param>
    /// <param name="connectionParameters">网络连接参数</param>
    /// <param name="transportTrigger">传输类与触发</param>
    /// <param name="connectionPath">连接路径</param>
    /// <returns>请求</returns>
    public static CipMessage BuildForwardOpen(
        UInt32 o2tConnectionId,
        UInt32 t2oConnectionId,
        UInt16 connectionSerial,
        UInt16 vendorId,
        UInt32 originatorSerial,
        UInt32 rpiMicroseconds,
        UInt16 connectionParameters,
        Byte transportTrigger,
        Byte[] connectionPath)
    {
        connectionPath = connectionPath ?? new Byte[0];
        var writer = new ByteWriter();
        writer.U8(0x07);
        writer.U8(0x10);
        writer.U32(o2tConnectionId);
        writer.U32(t2oConnectionId);
        writer.U16(connectionSerial);
        writer.U16(vendorId);
        writer.U32(originatorSerial);
        writer.U8(0x01);
        writer.U8(0);
        writer.U8(0);
        writer.U8(0);
        writer.U32(rpiMicroseconds);
        writer.U16(connectionParameters);
        writer.U32(rpiMicroseconds);
        writer.U16(connectionParameters);
        writer.U8(transportTrigger);
        writer.U8(CipPath.SizeInWords(connectionPath));
        writer.Bytes(connectionPath);

        return new CipMessage
        {
            Service = CipService.ForwardOpen,
            Path = CipPath.ClassInstance(CipService.ClassConnectionManager, 1),
            Data = writer.ToArray()
        };
    }

    /// <summary>Forward Close</summary>
    /// <param name="connectionSerial">连接序列号</param>
    /// <param name="vendorId">发起方厂商号</param>
    /// <param name="originatorSerial">发起方序列号</param>
    /// <param name="connectionPath">与 Forward Open 相同的连接路径</param>
    /// <returns>请求</returns>
    public static CipMessage BuildForwardClose(UInt16 connectionSerial, UInt16 vendorId, UInt32 originatorSerial, Byte[] connectionPath)
    {
        connectionPath = connectionPath ?? new Byte[0];
        var writer = new ByteWriter();
        writer.U8(0x07);
        writer.U8(0x10);
        writer.U16(connectionSerial);
        writer.U16(vendorId);
        writer.U32(originatorSerial);
        writer.U8(CipPath.SizeInWords(connectionPath));
        writer.U8(0);
        writer.Bytes(connectionPath);

        return new CipMessage
        {
            Service = CipService.ForwardClose,
            Path = CipPath.ClassInstance(CipService.ClassConnectionManager, 1),
            Data = writer.ToArray()
        };
    }

    /// <summary>身份对象 Get_Attributes_All</summary>
    /// <returns>请求</returns>
    public static CipMessage ReadIdentity() => new CipMessage
    {
        Service = CipService.GetAttributesAll,
        Path = CipPath.ClassInstance(CipService.ClassIdentity, 1),
        Data = new Byte[0]
    };

    /// <summary>路径是否指向连接管理器实例 1</summary>
    /// <param name="path">路径</param>
    /// <returns>是否是连接管理器</returns>
    public static Boolean IsConnectionManager(Byte[] path) =>
        path != null && path.Length >= 4 && path[0] == 0x20 && path[1] == CipService.ClassConnectionManager && path[2] == 0x24 && path[3] == 0x01;
}
