using System.Net;
using System.Text;
using NewLife.Data;
using NewLife.Log;
using NewLife.Net;

namespace NewLife.AB.Protocols;

/// <summary>Logix EtherNet/IP 模拟服务器</summary>
/// <remarks>
/// 用于开发和单元测试，不需要真实 PLC。支持注册会话、ListIdentity、未连接与已连接显式消息、
/// 符号标签读写、分段、按位读改写和多服务打包。
/// 写入不存在的标签时，若 <see cref="AutoCreateTags"/> 为 true 会按请求类型创建；真实控制器不会自动建点。
///
/// 使用示例：
/// <code>
/// using var server = new LogixServer();
/// server.Start();
/// using var client = new LogixClient("127.0.0.1", server.Port);
/// client.Connect();
/// client.WriteInt32("Counter", 123);
/// </code>
/// </remarks>
public sealed class LogixServer : NetServer<LogixServer.EipSession>
{
    /// <summary>模拟 STRING 的默认结构句柄</summary>
    public const UInt16 DefaultStringHandle = 0x00CE;

    private readonly Object _gate = new Object();
    private readonly Dictionary<String, TagData> _tags = new Dictionary<String, TagData>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<UInt32> _sessions = new HashSet<UInt32>();
    private UInt32 _nextSession = 1;

    /// <summary>是否在运行</summary>
    public Boolean IsRunning => Active;

    /// <summary>写入未知标签时是否创建。默认 true。真实 PLC 不会创建标签。</summary>
    public Boolean AutoCreateTags { get; set; } = true;

    /// <summary>非 0 时，所有 CIP 服务返回该通用状态</summary>
    public Byte ForceGeneralStatus { get; set; }

    /// <summary>要求 Unconnected Send 的背板槽号。null 表示接受任意槽。</summary>
    public Int32? RequiredSlot { get; set; }

    /// <summary>下一次 SendRRData / SendUnitData 返回会话无效，用于测试重注册</summary>
    public Boolean InvalidateSessionOnce { get; set; }

    /// <summary>单次应答中的最大元素字节数。更小的值会迫使分段读取。默认 480。</summary>
    public Int32 MaxResponseBytes { get; set; } = 480;

    /// <summary>身份厂商号。默认 1，仅作模拟数据。</summary>
    public UInt16 VendorId { get; set; } = 1;

    /// <summary>设备类型。默认 0x000E，可编程控制器。</summary>
    public UInt16 DeviceType { get; set; } = 0x000E;

    /// <summary>产品代码</summary>
    public UInt16 ProductCode { get; set; } = 1;

    /// <summary>主版本</summary>
    public Byte RevisionMajor { get; set; } = 1;

    /// <summary>次版本</summary>
    public Byte RevisionMinor { get; set; } = 1;

    /// <summary>身份状态字</summary>
    public UInt16 IdentityStatus { get; set; }

    /// <summary>序列号</summary>
    public UInt32 SerialNumber { get; set; } = 0x00AB2026;

    /// <summary>产品名称</summary>
    public String ProductName { get; set; } = "LogixServer";

    /// <summary>启动监听</summary>
    /// <param name="port">端口。0 表示由系统分配。</param>
    public void Start(Int32 port = 0)
    {
        if (Active) return;
        Name = "Logix";
        SessionTimeout = 0;
        Local = new NetUri(NetType.Tcp, IPAddress.Loopback, port);
        Port = port;
        base.Start();
        XTrace.WriteLine("LogixServer 已启动: " + Port);
    }

    /// <summary>停止监听并断开客户端</summary>
    public void Stop() => base.Stop("stop");

    /// <summary>断开当前客户端，监听保持</summary>
    public void DropClients()
    {
        var sessions = Sessions;
        if (sessions == null || sessions.Count == 0) return;
        var list = new List<INetSession>(sessions.Count);
        foreach (var item in sessions) list.Add(item.Value);
        foreach (var session in list)
        {
            try { session.Close("drop"); } catch { }
        }
    }

    /// <summary>预置标签。data 为全部元素的原始字节。</summary>
    /// <param name="address">地址</param>
    /// <param name="typeCode">类型码</param>
    /// <param name="data">数据</param>
    public void SetTag(String address, UInt16 typeCode, Byte[] data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        var key = LogixAddress.Parse(address).Key;
        var size = CipDataType.GetElementSize(typeCode);
        if (typeCode == CipDataType.String) size = data.Length;
        if (size <= 0 || data.Length == 0 || data.Length % size != 0) throw new ArgumentException("数据长度不是元素大小的整数倍");

        var copy = new Byte[data.Length];
        Buffer.BlockCopy(data, 0, copy, 0, data.Length);
        lock (_gate)
        {
            _tags[key] = new TagData
            {
                TypeCode = CipDataType.IsBool(typeCode) ? CipDataType.Bool : typeCode,
                StructureHandle = typeCode == CipDataType.String ? DefaultStringHandle : (UInt16)0,
                ElementSize = size,
                Data = copy
            };
        }
    }

    /// <summary>读取模拟器中的标签原始字节</summary>
    /// <param name="address">地址</param>
    /// <returns>不存在时返回 null</returns>
    public Byte[] GetTag(String address)
    {
        var key = LogixAddress.Parse(address).Key;
        lock (_gate)
        {
            if (!_tags.TryGetValue(key, out var tag)) return null;
            var copy = new Byte[tag.Data.Length];
            Buffer.BlockCopy(tag.Data, 0, copy, 0, copy.Length);
            return copy;
        }
    }

    private void Forget(UInt32 session)
    {
        if (session == 0) return;
        lock (_gate) _sessions.Remove(session);
    }

    private EipPacket HandlePacket(EipPacket request, ClientState state)
    {
        var response = new EipPacket
        {
            Command = request.Command,
            SenderContext = request.SenderContext,
            SessionHandle = request.SessionHandle
        };

        try
        {
            switch (request.Command)
            {
                case EipCommand.RegisterSession:
                    response.Data = Register(request, state, response);
                    break;
                case EipCommand.ListIdentity:
                    response.Data = BuildIdentity().ToListIdentityBytes((UInt16)Port, IPAddress.Loopback);
                    break;
                case EipCommand.ListServices:
                    response.Data = BuildServices();
                    break;
                case EipCommand.SendRRData:
                case EipCommand.SendUnitData:
                    response.Data = HandleExplicit(request, state, response);
                    break;
                default:
                    response.Status = EipStatus.InvalidCommand;
                    response.Data = new Byte[0];
                    break;
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("LogixServer 处理失败: " + ex.Message);
            response.Status = EipStatus.IncorrectData;
            response.Data = new Byte[0];
        }

        if (response.Data == null) response.Data = new Byte[0];
        return response;
    }

    private Byte[] Register(EipPacket request, ClientState state, EipPacket response)
    {
        if (request.Data == null || request.Data.Length < 4 || BinaryBuffer.ReadUInt16(request.Data, 0) != 1)
        {
            response.Status = EipStatus.UnsupportedProtocol;
            return request.Data ?? new Byte[0];
        }

        UInt32 session;
        lock (_gate)
        {
            session = _nextSession++;
            if (session == 0) session = _nextSession++;
            _sessions.Add(session);
        }
        state.Session = session;
        response.SessionHandle = session;
        return new Byte[] { 0x01, 0x00, 0x00, 0x00 };
    }

    private Byte[] HandleExplicit(EipPacket request, ClientState state, EipPacket response)
    {
        var invalid = false;
        lock (_gate)
        {
            if (InvalidateSessionOnce)
            {
                InvalidateSessionOnce = false;
                if (request.SessionHandle != 0) _sessions.Remove(request.SessionHandle);
                invalid = true;
            }
            else if (!_sessions.Contains(request.SessionHandle))
            {
                invalid = true;
            }
        }
        if (invalid)
        {
            response.Status = EipStatus.InvalidSession;
            return new Byte[0];
        }

        UInt32 connectionId;
        UInt16 sequence;
        Byte[] cip;
        ParseExplicit(request.Data, out connectionId, out sequence, out cip);
        if (request.Command == EipCommand.SendUnitData && (state.O2TConnectionId == 0 || connectionId != state.O2TConnectionId))
        {
            response.Status = EipStatus.IncorrectData;
            return new Byte[0];
        }

        var requestMessage = CipMessage.ParseRequest(cip);
        var reply = Dispatch(requestMessage, state);
        var body = reply.ToResponseBytes();
        if (request.Command == EipCommand.SendUnitData)
            return EipCommonPacket.BuildSendUnitData(state.T2OConnectionId, sequence, body, 0);
        return EipCommonPacket.BuildSendRRData(body, 0);
    }

    private CipMessage Dispatch(CipMessage request, ClientState state)
    {
        if (ForceGeneralStatus != 0)
            return new CipMessage { Service = request.Service, GeneralStatus = ForceGeneralStatus };

        if (CipMessage.IsConnectionManager(request.Path))
        {
            if (request.Service == CipService.UnconnectedSend) return HandleUnconnected(request, state);
            if (request.Service == CipService.ForwardOpen) return HandleForwardOpen(request, state);
            if (request.Service == CipService.ForwardClose) return HandleForwardClose(request, state);
            return Fail(request.Service, CipStatus.ServiceNotSupported);
        }

        if (request.Service == CipService.MultipleServicePacket) return HandleMultiple(request, state);
        if (request.Service == CipService.GetAttributesAll && IsIdentity(request.Path))
        {
            return new CipMessage { Service = request.Service, Data = BuildIdentity().ToAttributeBytes() };
        }

        return HandleTag(request);
    }

    private CipMessage HandleUnconnected(CipMessage request, ClientState state)
    {
        Byte[] route;
        Byte[] embedded;
        try
        {
            embedded = CipMessage.ParseUnconnectedSend(request.Data, out route);
        }
        catch
        {
            return Fail(request.Service, CipStatus.NotEnoughData);
        }

        if (RequiredSlot.HasValue && !RouteMatches(route, RequiredSlot.Value))
            return Fail(request.Service, CipStatus.PathSegmentError);

        var inner = Dispatch(CipMessage.ParseRequest(embedded), state);
        return new CipMessage { Service = request.Service, Data = inner.ToResponseBytes() };
    }

    private CipMessage HandleForwardOpen(CipMessage request, ClientState state)
    {
        if (request.Data == null || request.Data.Length < 36) return Fail(request.Service, CipStatus.NotEnoughData);
        var o2t = BinaryBuffer.ReadUInt32(request.Data, 2);
        var t2o = BinaryBuffer.ReadUInt32(request.Data, 6);
        state.O2TConnectionId = o2t;
        state.T2OConnectionId = t2o;

        var data = new Byte[26];
        BinaryBuffer.WriteUInt32(data, 0, o2t);
        BinaryBuffer.WriteUInt32(data, 4, t2o);
        Buffer.BlockCopy(request.Data, 10, data, 8, 8);
        BinaryBuffer.WriteUInt32(data, 16, 100000);
        BinaryBuffer.WriteUInt32(data, 20, 100000);
        return new CipMessage { Service = request.Service, Data = data };
    }

    private CipMessage HandleForwardClose(CipMessage request, ClientState state)
    {
        state.O2TConnectionId = 0;
        state.T2OConnectionId = 0;
        return new CipMessage { Service = request.Service };
    }

    private CipMessage HandleMultiple(CipMessage request, ClientState state)
    {
        try
        {
            if (request.Data == null || request.Data.Length < 2) return Fail(request.Service, CipStatus.NotEnoughData);
            var count = BinaryBuffer.ReadUInt16(request.Data, 0);
            var anyError = false;
            var replies = new Byte[count][];
            for (var i = 0; i < count; i++)
            {
                var start = BinaryBuffer.ReadUInt16(request.Data, 2 + i * 2);
                var end = i + 1 < count ? BinaryBuffer.ReadUInt16(request.Data, 2 + (i + 1) * 2) : request.Data.Length;
                var slice = new Byte[end - start];
                Buffer.BlockCopy(request.Data, start, slice, 0, slice.Length);
                var reply = Dispatch(CipMessage.ParseRequest(slice), state);
                if (reply.GeneralStatus != CipStatus.Success) anyError = true;
                replies[i] = reply.ToResponseBytes();
            }

            var offset = 2 + 2 * count;
            var writer = new ByteWriter();
            writer.U16((UInt16)count);
            foreach (var reply in replies)
            {
                writer.U16((UInt16)offset);
                offset += reply.Length;
            }
            foreach (var reply in replies) writer.Bytes(reply);
            return new CipMessage
            {
                Service = request.Service,
                GeneralStatus = anyError ? CipStatus.EmbeddedServiceError : CipStatus.Success,
                Data = writer.ToArray()
            };
        }
        catch
        {
            return Fail(request.Service, CipStatus.NotEnoughData);
        }
    }

    private CipMessage HandleTag(CipMessage request)
    {
        LogixAddress address;
        try
        {
            address = AddressFromPath(request.Path);
        }
        catch
        {
            return Fail(request.Service, CipStatus.PathSegmentError);
        }

        if (address == null) return Fail(request.Service, CipStatus.PathDestinationUnknown);
        var elements = address.AllElements;
        if (elements.Length > 1) return Fail(request.Service, CipStatus.InvalidParameter);
        var index = elements.Length == 0 ? 0 : (Int32)elements[0];

        switch (request.Service)
        {
            case CipService.ReadTag: return ReadTag(address.Key, index, request, 0);
            case CipService.ReadTagFragmented: return ReadFragment(address.Key, index, request);
            case CipService.WriteTag: return WriteTag(address.Key, index, request, false);
            case CipService.WriteTagFragmented: return WriteTag(address.Key, index, request, true);
            case CipService.ReadModifyWriteTag: return ModifyTag(address.Key, index, request);
            default: return Fail(request.Service, CipStatus.ServiceNotSupported);
        }
    }

    private CipMessage ReadTag(String key, Int32 index, CipMessage request, UInt32 byteOffset)
    {
        if (request.Data == null || request.Data.Length < 2) return Fail(request.Service, CipStatus.NotEnoughData);
        var count = BinaryBuffer.ReadUInt16(request.Data, 0);
        TagData tag;
        lock (_gate)
        {
            if (!_tags.TryGetValue(key, out tag)) return Fail(request.Service, CipStatus.PathDestinationUnknown);
        }
        if (count == 0 || index < 0 || index + count > ElementCount(tag))
            return Fail(request.Service, 0xFF, CipStatus.ExtBeyondEnd);

        var full = Slice(tag.Data, index * tag.ElementSize, count * tag.ElementSize);
        if (byteOffset > (UInt32)full.Length) return Fail(request.Service, 0xFF, CipStatus.ExtOffsetBeyondEnd);
        var remain = full.Length - (Int32)byteOffset;
        var take = remain;
        var status = CipStatus.Success;
        if (take > MaxResponseBytes)
        {
            take = MaxResponseBytes;
            if (tag.ElementSize > 1 && take >= tag.ElementSize) take -= take % tag.ElementSize;
            status = CipStatus.PartialTransfer;
        }
        if (take < 0) take = 0;

        var writer = new ByteWriter();
        writer.U16(tag.TypeCode);
        if (tag.TypeCode == CipDataType.String) writer.U16(tag.StructureHandle);
        writer.Bytes(Slice(full, (Int32)byteOffset, take));
        return new CipMessage { Service = request.Service, GeneralStatus = status, Data = writer.ToArray() };
    }

    private CipMessage ReadFragment(String key, Int32 index, CipMessage request)
    {
        if (request.Data == null || request.Data.Length < 6) return Fail(request.Service, CipStatus.NotEnoughData);
        var offset = BinaryBuffer.ReadUInt32(request.Data, 2);
        return ReadTag(key, index, request, offset);
    }

    private CipMessage WriteTag(String key, Int32 index, CipMessage request, Boolean fragmented)
    {
        if (request.Data == null || request.Data.Length < 4) return Fail(request.Service, CipStatus.NotEnoughData);
        var typeCode = BinaryBuffer.ReadUInt16(request.Data, 0);
        var cursor = 2;
        UInt16 handle = 0;
        if (CipDataType.IsStructure(typeCode))
        {
            if (request.Data.Length < cursor + 2) return Fail(request.Service, CipStatus.NotEnoughData);
            handle = BinaryBuffer.ReadUInt16(request.Data, cursor);
            cursor += 2;
        }
        if (request.Data.Length < cursor + 2) return Fail(request.Service, CipStatus.NotEnoughData);
        var count = BinaryBuffer.ReadUInt16(request.Data, cursor);
        cursor += 2;
        UInt32 byteOffset = 0;
        if (fragmented)
        {
            if (request.Data.Length < cursor + 4) return Fail(request.Service, CipStatus.NotEnoughData);
            byteOffset = BinaryBuffer.ReadUInt32(request.Data, cursor);
            cursor += 4;
        }
        var payload = new Byte[request.Data.Length - cursor];
        if (payload.Length > 0) Buffer.BlockCopy(request.Data, cursor, payload, 0, payload.Length);

        var elementSize = CipDataType.GetElementSize(typeCode);
        if (typeCode == CipDataType.String)
        {
            if (!fragmented) elementSize = payload.Length / (count == 0 ? 1 : count);
            else elementSize = 0;
        }
        if (count == 0) return Fail(request.Service, CipStatus.NotEnoughData);

        lock (_gate)
        {
            TagData tag;
            var exists = _tags.TryGetValue(key, out tag);
            if (!exists)
            {
                if (!AutoCreateTags) return Fail(request.Service, CipStatus.PathDestinationUnknown);
                var size = elementSize;
                if (typeCode == CipDataType.String)
                {
                    size = fragmented ? payload.Length + (Int32)byteOffset : payload.Length / count;
                    if (size <= 0) return Fail(request.Service, CipStatus.NotEnoughData);
                }
                if (size <= 0) return Fail(request.Service, CipStatus.InvalidParameter);
                tag = new TagData
                {
                    TypeCode = CipDataType.IsBool(typeCode) ? CipDataType.Bool : typeCode,
                    StructureHandle = handle == 0 ? DefaultStringHandle : handle,
                    ElementSize = size,
                    Data = new Byte[size * (index + count)]
                };
                _tags[key] = tag;
            }
            else if (!SameType(tag.TypeCode, typeCode))
            {
                return Fail(request.Service, 0xFF, CipStatus.ExtTypeMismatch);
            }

            if (typeCode == CipDataType.String && !fragmented && payload.Length != tag.ElementSize * count)
            {
                if (payload.Length > tag.ElementSize * count) return Fail(request.Service, 0xFF, CipStatus.ExtTypeMismatch);
            }

            var start = index * tag.ElementSize + (Int32)byteOffset;
            var need = start + payload.Length;
            if (need > tag.Data.Length)
            {
                if (index + count > ElementCount(tag) && byteOffset == 0 && !fragmented)
                    return Fail(request.Service, 0xFF, CipStatus.ExtBeyondEnd);
                var bigger = new Byte[Math.Max(need, tag.ElementSize * (index + count))];
                Buffer.BlockCopy(tag.Data, 0, bigger, 0, tag.Data.Length);
                tag.Data = bigger;
            }
            if (start < 0 || start + payload.Length > tag.Data.Length)
                return Fail(request.Service, 0xFF, CipStatus.ExtBeyondEnd);
            Buffer.BlockCopy(payload, 0, tag.Data, start, payload.Length);
            if (handle != 0) tag.StructureHandle = handle;
        }

        return new CipMessage { Service = request.Service };
    }

    private CipMessage ModifyTag(String key, Int32 index, CipMessage request)
    {
        if (request.Data == null || request.Data.Length < 2) return Fail(request.Service, CipStatus.NotEnoughData);
        var size = BinaryBuffer.ReadUInt16(request.Data, 0);
        if (request.Data.Length < 2 + size * 2) return Fail(request.Service, CipStatus.NotEnoughData);
        var orMask = new Byte[size];
        var andMask = new Byte[size];
        Buffer.BlockCopy(request.Data, 2, orMask, 0, size);
        Buffer.BlockCopy(request.Data, 2 + size, andMask, 0, size);

        lock (_gate)
        {
            if (!_tags.TryGetValue(key, out var tag)) return Fail(request.Service, CipStatus.PathDestinationUnknown);
            if (size > tag.ElementSize) return Fail(request.Service, CipStatus.InvalidParameter);
            if (index < 0 || index >= ElementCount(tag)) return Fail(request.Service, 0xFF, CipStatus.ExtBeyondEnd);
            var at = index * tag.ElementSize;
            for (var i = 0; i < size; i++)
            {
                tag.Data[at + i] = (Byte)((tag.Data[at + i] & andMask[i]) | orMask[i]);
            }
        }
        return new CipMessage { Service = request.Service };
    }

    private LogixIdentity BuildIdentity() => new LogixIdentity
    {
        VendorId = VendorId,
        DeviceType = DeviceType,
        ProductCode = ProductCode,
        RevisionMajor = RevisionMajor,
        RevisionMinor = RevisionMinor,
        Status = IdentityStatus,
        SerialNumber = SerialNumber,
        ProductName = ProductName,
        State = 3
    };

    private static Byte[] BuildServices()
    {
        var name = Encoding.ASCII.GetBytes("Communications");
        var writer = new ByteWriter();
        writer.U16(1);
        writer.U16(0x0100);
        writer.U16(20);
        writer.U16(1);
        writer.U16(0x0020);
        writer.Bytes(name);
        writer.Bytes(new Byte[16 - name.Length]);
        return writer.ToArray();
    }

    private static void ParseExplicit(Byte[] data, out UInt32 connectionId, out UInt16 sequence, out Byte[] cip)
    {
        connectionId = 0;
        sequence = 0;
        cip = null;
        var reader = new ByteReader(data);
        reader.U32();
        reader.U16();
        var count = reader.U16();
        for (var i = 0; i < count; i++)
        {
            var type = reader.U16();
            var length = reader.U16();
            var item = reader.Bytes(length);
            if (type == EipCommonPacket.ConnectedAddress && item.Length >= 4)
                connectionId = BinaryBuffer.ReadUInt32(item, 0);
            else if (type == EipCommonPacket.UnconnectedData)
                cip = item;
            else if (type == EipCommonPacket.ConnectedData && item.Length >= 2)
            {
                sequence = BinaryBuffer.ReadUInt16(item, 0);
                cip = new Byte[item.Length - 2];
                Buffer.BlockCopy(item, 2, cip, 0, cip.Length);
            }
        }
        if (cip == null) throw new InvalidOperationException("没有 CIP 数据");
    }

    private static Boolean RouteMatches(Byte[] route, Int32 slot)
    {
        if (route == null) return false;
        for (var i = 0; i + 1 < route.Length; )
        {
            var port = route[i];
            if ((port & 0x10) == 0 && port <= 14)
            {
                if (port == 1 && route[i + 1] == slot) return true;
                i += 2;
            }
            else
            {
                var linkLength = route[i + 1];
                var step = 2 + linkLength;
                if ((step & 1) == 1) step++;
                i += step;
            }
        }
        return false;
    }

    private static Boolean IsIdentity(Byte[] path) =>
        path != null && path.Length >= 4 && path[0] == 0x20 && path[1] == CipService.ClassIdentity && path[2] == 0x24 && path[3] == 1;

    private static LogixAddress AddressFromPath(Byte[] path)
    {
        if (path == null || path.Length == 0 || path[0] != 0x91) return null;
        var symbols = new List<LogixSymbol>();
        var i = 0;
        while (i < path.Length)
        {
            if (path[i] != 0x91) break;
            var length = path[i + 1];
            var chars = new Char[length];
            for (var n = 0; n < length; n++) chars[n] = (Char)path[i + 2 + n];
            i += 2 + length;
            if ((length & 1) == 1) i++;
            var elements = new List<UInt32>();
            while (i < path.Length && path[i] != 0x91)
            {
                if (path[i] == 0x28)
                {
                    elements.Add(path[i + 1]);
                    i += 2;
                }
                else if (path[i] == 0x29)
                {
                    elements.Add(BinaryBuffer.ReadUInt16(path, i + 2));
                    i += 4;
                }
                else if (path[i] == 0x2A)
                {
                    elements.Add(BinaryBuffer.ReadUInt32(path, i + 2));
                    i += 6;
                }
                else break;
            }
            symbols.Add(new LogixSymbol(new String(chars), elements.ToArray()));
        }
        if (symbols.Count == 0) return null;
        return new LogixAddress(null, symbols.ToArray(), null);
    }

    private static Boolean SameType(UInt16 stored, UInt16 requested)
    {
        if (CipDataType.IsBool(stored) && CipDataType.IsBool(requested)) return true;
        return stored == requested;
    }

    private static Int32 ElementCount(TagData tag) => tag.ElementSize == 0 ? 0 : tag.Data.Length / tag.ElementSize;

    private static Byte[] Slice(Byte[] source, Int32 offset, Int32 count)
    {
        var data = new Byte[count];
        if (count > 0) Buffer.BlockCopy(source, offset, data, 0, count);
        return data;
    }

    private static CipMessage Fail(Byte service, Byte status) => new CipMessage { Service = service, GeneralStatus = status };

    private static CipMessage Fail(Byte service, Byte status, UInt16 extended) =>
        new CipMessage { Service = service, GeneralStatus = status, ExtendedStatus = new UInt16[] { extended } };

    private sealed class TagData
    {
        public UInt16 TypeCode;
        public UInt16 StructureHandle;
        public Int32 ElementSize;
        public Byte[] Data;
    }

    private sealed class ClientState
    {
        public UInt32 Session;
        public UInt32 O2TConnectionId;
        public UInt32 T2OConnectionId;
    }

    /// <summary>模拟器上的一条 TCP 会话。按 EtherNet/IP 的 24 字节头切帧。</summary>
    public sealed class EipSession : NetSession<LogixServer>
    {
        private readonly List<Byte> _inbox = new List<Byte>();
        private readonly ClientState _state = new ClientState();

        /// <summary>收到数据后切出完整封装帧并应答</summary>
        /// <param name="e">接收参数</param>
        protected override void OnReceive(ReceivedEventArgs e)
        {
            var chunk = e.Packet == null ? null : e.Packet.ReadBytes();
            if (chunk == null || chunk.Length == 0) return;
            _inbox.AddRange(chunk);
            while (TryTakePacket(out var request))
            {
                if (request.Command == EipCommand.Nop || request.Command == EipCommand.UnRegisterSession)
                {
                    if (request.Command == EipCommand.UnRegisterSession) Host.Forget(request.SessionHandle);
                    continue;
                }

                var response = Host.HandlePacket(request, _state);
                var bytes = response.ToBytes();
                Send(bytes, 0, bytes.Length);
            }
        }

        /// <summary>连接断开时丢掉会话句柄</summary>
        /// <param name="reason">原因</param>
        protected override void OnDisconnected(String reason)
        {
            Host?.Forget(_state.Session);
            base.OnDisconnected(reason);
        }

        private Boolean TryTakePacket(out EipPacket packet)
        {
            packet = null;
            if (_inbox.Count < EipPacket.HeaderSize) return false;
            var header = new Byte[EipPacket.HeaderSize];
            _inbox.CopyTo(0, header, 0, header.Length);
            var length = EipPacket.ReadDataLength(header);
            if (length < 0 || length > 1024 * 1024)
            {
                _inbox.Clear();
                return false;
            }
            var total = EipPacket.HeaderSize + length;
            if (_inbox.Count < total) return false;
            var data = new Byte[length];
            if (length > 0)
            {
                for (var i = 0; i < length; i++) data[i] = _inbox[EipPacket.HeaderSize + i];
            }
            packet = EipPacket.FromHeader(header, data);
            _inbox.RemoveRange(0, total);
            return true;
        }
    }
}
