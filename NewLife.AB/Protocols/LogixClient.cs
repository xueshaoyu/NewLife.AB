using System.Net.Sockets;
using NewLife.Log;

namespace NewLife.AB.Protocols;

/// <summary>Logix EtherNet/IP 客户端</summary>
/// <remarks>
/// 面向 ControlLogix / CompactLogix 及兼容的 Logix 5000 控制器，使用符号标签而不是寄存器文件。
/// 默认经背板端口 1 路由到 <see cref="Slot"/>，用未连接消息读写标签。
///
/// 使用示例：
/// <code>
/// using var client = new LogixClient("192.168.1.10", 44818);
/// client.Slot = 0;
/// client.Connect();
/// var value = client.ReadInt32("Program:MainProgram.Counter");
/// client.WriteInt32("Program:MainProgram.Counter", 123);
/// </code>
/// </remarks>
public partial class LogixClient : IDisposable
{
    #region 字段

    private readonly Object _lock = new Object();
    private readonly Dictionary<String, UInt16> _stringHandles = new Dictionary<String, UInt16>(StringComparer.OrdinalIgnoreCase);
    private readonly Random _random = new Random();
    private TcpClient _client;
    private NetworkStream _stream;
    private UInt32 _session;
    private UInt64 _context = 1;
    private UInt16 _sequence = 1;
    private UInt32 _o2tConnectionId;
    private UInt16 _connectionSerial;
    private UInt32 _originatorSerial;
    private Byte[] _connectionPath = new Byte[0];

    #endregion

    #region 属性

    /// <summary>默认端口 44818</summary>
    public const Int32 DefaultPort = 44818;

    /// <summary>显式连接的 16 位参数：500 字节、可变长度、点对点、低优先级（0x43F4）</summary>
    public const UInt16 ExplicitConnectionParameters = 0x43F4;

    /// <summary>Class 3 显式连接常用的传输触发字节</summary>
    public const Byte ExplicitTransportTrigger = 0xA3;

    /// <summary>主机名或 IP</summary>
    public String Host { get; set; }

    /// <summary>端口。默认 44818。</summary>
    public Int32 Port { get; set; } = DefaultPort;

    /// <summary>CPU 槽号。路由开启时编入背板路径 1,槽号。默认 0。</summary>
    public Int32 Slot { get; set; }

    /// <summary>自定义路由，例如 1,2 或 2,192.168.1.10,1,0。为空时使用 1,槽号。</summary>
    public String RoutePath { get; set; }

    /// <summary>是否把标签请求包进 Unconnected Send 并带上槽号路由。默认 true。</summary>
    public Boolean RouteToSlot { get; set; } = true;

    /// <summary>是否在注册会话后 Forward Open，随后用 SendUnitData。默认 false，使用未连接消息。</summary>
    public Boolean ConnectedMessaging { get; set; }

    /// <summary>是否自动重连。默认 true。</summary>
    public Boolean AutoReconnect { get; set; } = true;

    /// <summary>最大重试次数。默认 3。</summary>
    public Int32 MaxReconnectRetries { get; set; } = 3;

    /// <summary>重试退避基数（毫秒）。第 n 次等待 n 倍。默认 1000。</summary>
    public Int32 RetryDelayMilliseconds { get; set; } = 1000;

    /// <summary>连接超时（毫秒）。默认 3000。</summary>
    public Int32 ConnectTimeout { get; set; } = 3000;

    /// <summary>接收超时（毫秒）。默认 5000。</summary>
    public Int32 ReceiveTimeout { get; set; } = 5000;

    /// <summary>单次写入的最大元素字节数，超出后改用分段写。默认 400。</summary>
    public Int32 WriteFragmentSize { get; set; } = 400;

    /// <summary>Logix STRING 的 DATA 容量。默认 82。</summary>
    public Int32 StringCapacity { get; set; } = 82;

    /// <summary>已知的 STRING 结构句柄。设置后写字符串不再先读。</summary>
    public UInt16? StringStructureHandle { get; set; }

    /// <summary>Forward Open 的发起方厂商号，只用于连接匹配，不代表 ODVA 注册。默认 0x1234。</summary>
    public UInt16 OriginatorVendorId { get; set; } = 0x1234;

    /// <summary>当前会话句柄</summary>
    public UInt32 SessionHandle => _session;

    /// <summary>是否已连接</summary>
    public Boolean IsConnected => _client != null && _client.Connected;

    #endregion

    #region 构造

    /// <summary>实例化</summary>
    public LogixClient() { }

    /// <summary>实例化</summary>
    /// <param name="host">主机</param>
    /// <param name="port">端口，默认 44818</param>
    public LogixClient(String host, Int32 port = DefaultPort)
    {
        Host = host;
        Port = port;
    }

    #endregion

    #region 连接

    /// <summary>连接并注册会话。需要时执行 Forward Open。</summary>
    public void Connect()
    {
        lock (_lock)
        {
            ConnectCore();
        }
    }

    /// <summary>关闭连接。已建立的显式连接会先 Forward Close。</summary>
    public void Close()
    {
        lock (_lock)
        {
            CloseCore();
        }
    }

    /// <summary>释放连接</summary>
    public void Dispose() => Close();

    private void ConnectCore()
    {
        if (IsConnected) return;
        if (Host == null || Host.Length == 0) throw new ArgumentException("未指定主机");

        TcpClient client = null;
        try
        {
            client = new TcpClient { NoDelay = true };
            var task = client.ConnectAsync(Host, Port);
            if (!task.Wait(ConnectTimeout))
            {
                client.Close();
                throw new TimeoutException("连接超时: " + Host + ":" + Port);
            }
            if (task.IsFaulted) throw task.Exception.InnerException ?? task.Exception;

            _client = client;
            _stream = client.GetStream();
            _stream.ReadTimeout = ReceiveTimeout;
            _stream.WriteTimeout = ConnectTimeout;
            RegisterSessionCore();
            if (ConnectedMessaging) OpenConnectionCore();
            XTrace.WriteLine("Logix 已连接: " + Host + ":" + Port + " 会话 0x" + _session.ToString("X8"));
        }
        catch (Exception ex)
        {
            try { client?.Close(); } catch { }
            _client = null;
            _stream = null;
            _session = 0;
            if (ex is ArgumentException || ex is CipException || ex is TimeoutException) throw;
            throw new InvalidOperationException("连接失败: " + ex.Message, ex);
        }
    }

    private void CloseCore()
    {
        if (_o2tConnectionId != 0 && _stream != null)
        {
            try { CloseConnectionCore(); } catch { }
        }

        if (_session != 0 && _stream != null)
        {
            try
            {
                var packet = new EipPacket { Command = EipCommand.UnRegisterSession, SessionHandle = _session };
                var bytes = packet.ToBytes();
                _stream.Write(bytes, 0, bytes.Length);
            }
            catch { }
        }

        try { _stream?.Close(); } catch { }
        try { _client?.Close(); } catch { }
        _stream = null;
        _client = null;
        _session = 0;
        _o2tConnectionId = 0;
    }

    private void RegisterSessionCore()
    {
        var response = SendPacketCore(new EipPacket
        {
            Command = EipCommand.RegisterSession,
            SenderContext = NextContext(),
            Data = new Byte[] { 0x01, 0x00, 0x00, 0x00 }
        });
        if (response.Status != EipStatus.Success) throw CipException.FromEncapsulation(response.Status);
        if (response.SessionHandle == 0) throw new InvalidOperationException("注册会话未返回句柄");
        _session = response.SessionHandle;
    }

    private void OpenConnectionCore()
    {
        _connectionSerial = (UInt16)_random.Next(1, Int32.MaxValue);
        _originatorSerial = (UInt32)_random.Next(1, Int32.MaxValue);
        var o2t = (UInt32)_random.Next(1, Int32.MaxValue);
        var t2o = (UInt32)_random.Next(1, Int32.MaxValue);
        _connectionPath = BuildConnectionPath();
        var request = CipMessage.BuildForwardOpen(o2t, t2o, _connectionSerial, OriginatorVendorId, _originatorSerial, 100000, ExplicitConnectionParameters, ExplicitTransportTrigger, _connectionPath);
        var response = ExecuteCore(request.ToRequestBytes(), false);
        if (!response.IsSuccess) throw CipException.FromResponse(response, "Forward Open");
        if (response.Data == null || response.Data.Length < 8) throw new InvalidOperationException("Forward Open 应答过短");
        _o2tConnectionId = BinaryBuffer.ReadUInt32(response.Data, 0);
    }

    private void CloseConnectionCore()
    {
        var request = CipMessage.BuildForwardClose(_connectionSerial, OriginatorVendorId, _originatorSerial, _connectionPath);
        try { ExecuteCore(request.ToRequestBytes(), false); } catch { }
        _o2tConnectionId = 0;
    }

    private Byte[] BuildConnectionPath()
    {
        var route = RouteToSlot ? GetRoute() : new Byte[0];
        return CipPath.Concat(route, CipPath.ClassInstance(CipService.ClassMessageRouter, 1));
    }

    private Byte[] GetRoute()
    {
        if (RoutePath != null && RoutePath.Trim().Length > 0) return CipPath.ParseRoute(RoutePath);
        if (Slot < 0 || Slot > 255) throw new ArgumentOutOfRangeException(nameof(Slot), "槽号必须在 0 到 255 之间");
        return CipPath.Port(1, (Byte)Slot);
    }

    #endregion

    #region 传输

    private CipMessage Execute(Byte[] cipRequest) => Execute(cipRequest, true);

    private CipMessage Execute(Byte[] cipRequest, Boolean allowRoute)
    {
        lock (_lock)
        {
            var max = AutoReconnect ? MaxReconnectRetries : 0;
            for (var retry = 0; retry <= max; retry++)
            {
                try
                {
                    if (retry > 0) SleepRetry(retry);
                    if (!IsConnected) ConnectCore();
                    return ExecuteCore(cipRequest, allowRoute && RouteToSlot && _o2tConnectionId == 0);
                }
                catch (CipException ex) when (ex.EncapsulationStatus == EipStatus.InvalidSession && AutoReconnect && retry < max)
                {
                    XTrace.WriteLine("Logix 会话失效，重新注册");
                    _o2tConnectionId = 0;
                    try
                    {
                        RegisterSessionCore();
                        if (ConnectedMessaging) OpenConnectionCore();
                    }
                    catch { CloseCore(); }
                }
                catch (Exception ex) when (IsConnectionError(ex) && AutoReconnect && retry < max)
                {
                    XTrace.WriteLine("Logix 通信中断，准备重连: " + ex.Message);
                    CloseCore();
                }
            }
        }

        throw new InvalidOperationException("通信失败，已超过最大重试次数");
    }

    private CipMessage ExecuteCore(Byte[] cipRequest, Boolean route)
    {
        var framed = cipRequest;
        UInt16 command;
        Byte[] payload;
        if (_o2tConnectionId != 0)
        {
            command = EipCommand.SendUnitData;
            payload = EipCommonPacket.BuildSendUnitData(_o2tConnectionId, NextSequence(), framed, 10);
            route = false;
        }
        else
        {
            if (route) framed = CipMessage.BuildUnconnectedSend(cipRequest, GetRoute(), 0x07, 0x10).ToRequestBytes();
            command = EipCommand.SendRRData;
            payload = EipCommonPacket.BuildSendRRData(framed, 10);
        }

        var context = NextContext();
        var packet = SendPacketCore(new EipPacket
        {
            Command = command,
            SessionHandle = _session,
            SenderContext = context,
            Data = payload
        });
        return DecodeTransport(packet, context, route);
    }

    private CipMessage DecodeTransport(EipPacket packet, UInt64 context, Boolean route)
    {
        if (packet.Status != EipStatus.Success) throw CipException.FromEncapsulation(packet.Status);
        if (packet.SenderContext != context) throw new InvalidOperationException("应答上下文与请求不一致");

        var cip = EipCommonPacket.ExtractCip(packet.Data, out _);
        if (route)
        {
            var outer = CipMessage.ParseResponse(cip);
            if (!CipService.IsReply(outer.Service, CipService.UnconnectedSend))
                throw new InvalidOperationException("未收到 Unconnected Send 应答");
            if (!outer.IsSuccess) throw CipException.FromResponse(outer, "路由");
            cip = outer.Data;
        }

        return CipMessage.ParseResponse(cip);
    }

    private EipPacket SendPacketCore(EipPacket packet)
    {
        if (_stream == null) throw new InvalidOperationException("未连接到服务器");
        var bytes = packet.ToBytes();
        _stream.Write(bytes, 0, bytes.Length);

        var header = new Byte[EipPacket.HeaderSize];
        ReadExactly(header, 0, header.Length);
        var length = EipPacket.ReadDataLength(header);
        if (length > 1024 * 1024) throw new InvalidOperationException("应答长度异常");
        var data = new Byte[length];
        if (length > 0) ReadExactly(data, 0, length);
        return EipPacket.FromHeader(header, data);
    }

    private void ReadExactly(Byte[] buffer, Int32 offset, Int32 count)
    {
        var read = 0;
        while (read < count)
        {
            var n = _stream.Read(buffer, offset + read, count - read);
            if (n <= 0) throw new IOException("连接已关闭");
            read += n;
        }
    }

    private void SleepRetry(Int32 retry)
    {
        var delay = RetryDelayMilliseconds * retry;
        if (delay > 0) Thread.Sleep(delay);
    }

    private UInt64 NextContext() => _context++;

    private UInt16 NextSequence()
    {
        var value = _sequence++;
        if (_sequence == 0) _sequence = 1;
        return value;
    }

    private static Boolean IsConnectionError(Exception ex)
    {
        if (ex is CipException) return false;
        if (ex is TimeoutException || ex is IOException || ex is SocketException || ex is ObjectDisposedException) return true;
        return ex.InnerException != null && IsConnectionError(ex.InnerException);
    }

    #endregion

    #region 标签读写

    /// <summary>读取标签</summary>
    /// <param name="address">地址</param>
    /// <param name="count">元素个数</param>
    /// <returns>值</returns>
    public LogixValue ReadTag(String address, UInt16 count = 1)
    {
        var addr = LogixAddress.Parse(address);
        if (count == 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (addr.Bit.HasValue)
        {
            if (count != 1) throw new ArgumentException("位地址只能读取一个元素", nameof(address));
            var raw = ReadRaw(WithoutBit(addr), 1);
            return LogixValue.FromBoolean(ExtractBit(raw, addr.Bit.Value));
        }

        return ReadRaw(addr, count);
    }

    /// <summary>写入原始元素数据</summary>
    /// <param name="address">地址</param>
    /// <param name="typeCode">类型码</param>
    /// <param name="data">元素数据</param>
    /// <param name="count">元素个数</param>
    public void WriteTag(String address, UInt16 typeCode, Byte[] data, UInt16 count)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (count == 0) throw new ArgumentOutOfRangeException(nameof(count));
        var addr = LogixAddress.Parse(address);
        if (addr.Bit.HasValue) throw new ArgumentException("位地址请使用 WriteBoolean", nameof(address));
        var handle = typeCode == CipDataType.String ? ResolveStringHandle(address) : (UInt16)0;
        WritePayload(addr, typeCode, handle, data, count);
    }

    /// <summary>一次读取多个标签。内部使用 Multiple Service Packet，并按约 450 字节分批。</summary>
    /// <param name="addresses">地址</param>
    /// <returns>与地址顺序一致的值</returns>
    public LogixValue[] ReadTags(String[] addresses)
    {
        if (addresses == null) throw new ArgumentNullException(nameof(addresses));
        var results = new LogixValue[addresses.Length];
        var batch = new List<Int32>();
        var size = 2;
        for (var i = 0; i < addresses.Length; i++)
        {
            var addr = LogixAddress.Parse(addresses[i]);
            if (addr.Bit.HasValue)
            {
                FlushBatch(addresses, results, batch);
                batch.Clear();
                size = 2;
                results[i] = ReadTag(addresses[i], 1);
                continue;
            }

            var request = CipMessage.ReadTag(addr.ToPath(), 1).ToRequestBytes();
            if (batch.Count > 0 && size + 2 + request.Length > 450)
            {
                FlushBatch(addresses, results, batch);
                batch.Clear();
                size = 2;
            }

            batch.Add(i);
            size += 2 + request.Length;
        }

        FlushBatch(addresses, results, batch);
        return results;
    }

    /// <summary>读取原始字节</summary>
    /// <param name="address">地址</param>
    /// <param name="count">元素个数</param>
    /// <returns>元素数据</returns>
    public Byte[] ReadBytes(String address, UInt16 count = 1) => ReadTag(address, count).Raw;

    /// <summary>列出身份（封装命令 ListIdentity）</summary>
    /// <returns>身份</returns>
    public LogixIdentity ListIdentity()
    {
        lock (_lock)
        {
            if (!IsConnected) ConnectCore();
            var context = NextContext();
            var packet = SendPacketCore(new EipPacket
            {
                Command = EipCommand.ListIdentity,
                SessionHandle = _session,
                SenderContext = context
            });
            if (packet.Status != EipStatus.Success) throw CipException.FromEncapsulation(packet.Status);
            return LogixIdentity.ParseListIdentity(packet.Data);
        }
    }

    /// <summary>读取身份对象全部属性</summary>
    /// <returns>身份</returns>
    public LogixIdentity ReadIdentity()
    {
        var response = Execute(CipMessage.ReadIdentity().ToRequestBytes());
        if (!response.IsSuccess) throw CipException.FromResponse(response, "读取身份");
        return LogixIdentity.ParseAttributes(response.Data);
    }

    private LogixValue ReadRaw(LogixAddress address, UInt16 count)
    {
        var path = address.ToPath();
        var response = Execute(CipMessage.ReadTag(path, count).ToRequestBytes());
        if (response.GeneralStatus != CipStatus.Success && response.GeneralStatus != CipStatus.PartialTransfer)
            throw CipException.FromResponse(response, "读取 " + address);

        var header = TakeHeader(response.Data);
        var payload = new ByteWriter();
        payload.Bytes(ElementBytes(response.Data, header.Length));
        var offset = payload.Length;
        while (response.GeneralStatus == CipStatus.PartialTransfer)
        {
            response = Execute(CipMessage.ReadTagFragmented(path, count, (UInt32)offset).ToRequestBytes());
            if (response.GeneralStatus != CipStatus.Success && response.GeneralStatus != CipStatus.PartialTransfer)
                throw CipException.FromResponse(response, "分段读取 " + address);
            var chunk = ElementBytes(response.Data, header.Length);
            if (chunk.Length == 0) throw new InvalidOperationException("分段读取没有进展");
            payload.Bytes(chunk);
            offset += chunk.Length;
            if (offset > 8 * 1024 * 1024) throw new InvalidOperationException("分段读取超过上限");
        }

        var body = new ByteWriter();
        body.Bytes(header);
        body.Bytes(payload.ToArray());
        var value = LogixValue.Decode(body.ToArray(), count);
        RememberHandle(address.Key, value);
        return value;
    }

    private void WritePayload(LogixAddress address, UInt16 typeCode, UInt16 handle, Byte[] payload, UInt16 count)
    {
        var path = address.ToPath();
        if (WriteFragmentSize < 1) throw new ArgumentOutOfRangeException(nameof(WriteFragmentSize));
        if (payload.Length <= WriteFragmentSize)
        {
            var response = Execute(CipMessage.WriteTag(path, typeCode, handle, count, payload).ToRequestBytes());
            if (!response.IsSuccess) throw CipException.FromResponse(response, "写入 " + address);
            return;
        }

        var offset = 0;
        while (offset < payload.Length)
        {
            var size = WriteFragmentSize;
            if (size > payload.Length - offset) size = payload.Length - offset;
            var slice = new Byte[size];
            Buffer.BlockCopy(payload, offset, slice, 0, size);
            var response = Execute(CipMessage.WriteTagFragmented(path, typeCode, handle, count, (UInt32)offset, slice).ToRequestBytes());
            if (!response.IsSuccess) throw CipException.FromResponse(response, "分段写入 " + address);
            offset += size;
        }
    }

    private void FlushBatch(String[] addresses, LogixValue[] results, List<Int32> batch)
    {
        if (batch.Count == 0) return;
        if (batch.Count == 1)
        {
            results[batch[0]] = ReadTag(addresses[batch[0]], 1);
            return;
        }

        var requests = new Byte[batch.Count][];
        for (var i = 0; i < batch.Count; i++)
        {
            requests[i] = CipMessage.ReadTag(LogixAddress.Parse(addresses[batch[i]]).ToPath(), 1).ToRequestBytes();
        }

        var response = Execute(CipMessage.Multiple(requests).ToRequestBytes());
        if (response.GeneralStatus != CipStatus.Success && response.GeneralStatus != CipStatus.EmbeddedServiceError)
            throw CipException.FromResponse(response, "批量读取");

        var items = CipMessage.ParseMultiple(response.Data);
        if (items.Length != batch.Count) throw new InvalidOperationException("批量读取的应答个数不符");
        for (var i = 0; i < items.Length; i++)
        {
            if (!items[i].IsSuccess) throw CipException.FromResponse(items[i], "读取 " + addresses[batch[i]]);
            var value = LogixValue.Decode(items[i].Data, 1);
            RememberHandle(LogixAddress.Parse(addresses[batch[i]]).Key, value);
            results[batch[i]] = value;
        }
    }

    private UInt16 ResolveStringHandle(String address)
    {
        if (StringStructureHandle.HasValue) return StringStructureHandle.Value;
        var key = LogixAddress.Parse(address).Key;
        lock (_stringHandles)
        {
            if (_stringHandles.TryGetValue(key, out var cached)) return cached;
        }

        try
        {
            var value = ReadTag(address, 1);
            return value.StructureHandle;
        }
        catch (CipException ex) when (ex.GeneralStatus == CipStatus.PathDestinationUnknown)
        {
            return 0;
        }
    }

    private void RememberHandle(String key, LogixValue value)
    {
        if (value == null || !value.IsStructure) return;
        lock (_stringHandles)
        {
            _stringHandles[key] = value.StructureHandle;
        }
    }

    private static Byte[] TakeHeader(Byte[] reply)
    {
        if (reply == null || reply.Length < 2) throw new InvalidOperationException("标签应答没有类型码");
        var typeCode = BinaryBuffer.ReadUInt16(reply, 0);
        var length = CipDataType.IsStructure(typeCode) ? 4 : 2;
        if (reply.Length < length) throw new InvalidOperationException("标签应答类型头不完整");
        var header = new Byte[length];
        Buffer.BlockCopy(reply, 0, header, 0, length);
        return header;
    }

    private static Byte[] ElementBytes(Byte[] reply, Int32 headerLength)
    {
        if (reply == null || reply.Length < headerLength) return new Byte[0];
        var data = new Byte[reply.Length - headerLength];
        if (data.Length > 0) Buffer.BlockCopy(reply, headerLength, data, 0, data.Length);
        return data;
    }

    private static LogixAddress WithoutBit(LogixAddress address) => new LogixAddress(address.TypeCode, address.Symbols, null);

    private static Boolean ExtractBit(LogixValue value, Int32 bit)
    {
        var size = CipDataType.GetElementSize(value.TypeCode);
        if (size <= 0) size = value.Raw.Length;
        if (bit < 0 || bit >= size * 8) throw new ArgumentOutOfRangeException(nameof(bit), "位号超出元素宽度");
        var index = bit / 8;
        if (index >= value.Raw.Length) throw new InvalidOperationException("位号超出已读数据");
        return (value.Raw[index] & (1 << (bit % 8))) != 0;
    }

    private Int32 ResolveElementSize(LogixAddress address)
    {
        if (address.TypeCode.HasValue)
        {
            var hinted = CipDataType.GetElementSize(address.TypeCode.Value);
            if (hinted > 0) return hinted;
        }

        var value = ReadRaw(WithoutBit(address), 1);
        var size = CipDataType.GetElementSize(value.TypeCode);
        if (size <= 0) throw new InvalidOperationException("无法确定位操作的元素宽度");
        return size;
    }

    #endregion

    #region 类型化读写

    /// <summary>读取 BOOL。带位号时按位读取。</summary>
    /// <param name="address">地址</param>
    /// <returns>值</returns>
    public Boolean ReadBoolean(String address) => ReadTag(address, 1).GetBoolean();

    /// <summary>读取 SINT</summary>
    /// <param name="address">地址</param>
    /// <returns>值</returns>
    public SByte ReadSByte(String address) => ReadTag(address, 1).GetSByte();

    /// <summary>读取 INT</summary>
    /// <param name="address">地址</param>
    /// <returns>值</returns>
    public Int16 ReadInt16(String address) => ReadTag(address, 1).GetInt16();

    /// <summary>读取 DINT</summary>
    /// <param name="address">地址</param>
    /// <returns>值</returns>
    public Int32 ReadInt32(String address) => ReadTag(address, 1).GetInt32();

    /// <summary>读取 LINT</summary>
    /// <param name="address">地址</param>
    /// <returns>值</returns>
    public Int64 ReadInt64(String address) => ReadTag(address, 1).GetInt64();

    /// <summary>读取 USINT</summary>
    /// <param name="address">地址</param>
    /// <returns>值</returns>
    public Byte ReadByte(String address) => ReadTag(address, 1).GetByte();

    /// <summary>读取 UINT</summary>
    /// <param name="address">地址</param>
    /// <returns>值</returns>
    public UInt16 ReadUInt16(String address) => ReadTag(address, 1).GetUInt16();

    /// <summary>读取 UDINT</summary>
    /// <param name="address">地址</param>
    /// <returns>值</returns>
    public UInt32 ReadUInt32(String address) => ReadTag(address, 1).GetUInt32();

    /// <summary>读取 ULINT</summary>
    /// <param name="address">地址</param>
    /// <returns>值</returns>
    public UInt64 ReadUInt64(String address) => ReadTag(address, 1).GetUInt64();

    /// <summary>读取 REAL</summary>
    /// <param name="address">地址</param>
    /// <returns>值</returns>
    public Single ReadSingle(String address) => ReadTag(address, 1).GetSingle();

    /// <summary>读取 LREAL</summary>
    /// <param name="address">地址</param>
    /// <returns>值</returns>
    public Double ReadDouble(String address) => ReadTag(address, 1).GetDouble();

    /// <summary>读取 STRING</summary>
    /// <param name="address">地址</param>
    /// <returns>值</returns>
    public String ReadString(String address) => ReadTag(address, 1).GetString();

    /// <summary>读取 INT 数组</summary>
    /// <param name="address">地址</param>
    /// <param name="count">元素个数</param>
    /// <returns>数组</returns>
    public Int16[] ReadInt16(String address, UInt16 count) => (Int16[])ReadTag(address, count).ToArray();

    /// <summary>读取 DINT 数组</summary>
    /// <param name="address">地址</param>
    /// <param name="count">元素个数</param>
    /// <returns>数组</returns>
    public Int32[] ReadInt32(String address, UInt16 count) => (Int32[])ReadTag(address, count).ToArray();

    /// <summary>读取 LINT 数组</summary>
    /// <param name="address">地址</param>
    /// <param name="count">元素个数</param>
    /// <returns>数组</returns>
    public Int64[] ReadInt64(String address, UInt16 count) => (Int64[])ReadTag(address, count).ToArray();

    /// <summary>读取 REAL 数组</summary>
    /// <param name="address">地址</param>
    /// <param name="count">元素个数</param>
    /// <returns>数组</returns>
    public Single[] ReadSingle(String address, UInt16 count) => (Single[])ReadTag(address, count).ToArray();

    /// <summary>读取 LREAL 数组</summary>
    /// <param name="address">地址</param>
    /// <param name="count">元素个数</param>
    /// <returns>数组</returns>
    public Double[] ReadDouble(String address, UInt16 count) => (Double[])ReadTag(address, count).ToArray();

    /// <summary>写入 BOOL。带位号时使用 Read Modify Write，不改动同元素的其它位。</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    public void WriteBoolean(String address, Boolean value)
    {
        var addr = LogixAddress.Parse(address);
        if (!addr.Bit.HasValue)
        {
            WritePayload(addr, CipDataType.Bool, 0, LogixValue.EncodeBoolean(value), 1);
            return;
        }

        var size = ResolveElementSize(addr);
        Byte[] orMask;
        Byte[] andMask;
        CipMessage.BuildBitMasks(size, addr.Bit.Value, value, out orMask, out andMask);
        var response = Execute(CipMessage.ReadModifyWrite(WithoutBit(addr).ToPath(), orMask, andMask).ToRequestBytes());
        if (!response.IsSuccess) throw CipException.FromResponse(response, "写位 " + address);
    }

    /// <summary>写入 SINT</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    public void WriteSByte(String address, SByte value) => WriteTag(address, CipDataType.Sint, LogixValue.EncodeSByte(value), 1);

    /// <summary>写入 INT</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    public void WriteInt16(String address, Int16 value) => WriteTag(address, CipDataType.Int, LogixValue.EncodeInt16(value), 1);

    /// <summary>写入 DINT</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    public void WriteInt32(String address, Int32 value) => WriteTag(address, CipDataType.Dint, LogixValue.EncodeInt32(value), 1);

    /// <summary>写入 LINT</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    public void WriteInt64(String address, Int64 value) => WriteTag(address, CipDataType.Lint, LogixValue.EncodeInt64(value), 1);

    /// <summary>写入 USINT</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    public void WriteByte(String address, Byte value) => WriteTag(address, CipDataType.Usint, LogixValue.EncodeByte(value), 1);

    /// <summary>写入 UINT</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    public void WriteUInt16(String address, UInt16 value) => WriteTag(address, CipDataType.Uint, LogixValue.EncodeUInt16(value), 1);

    /// <summary>写入 UDINT</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    public void WriteUInt32(String address, UInt32 value) => WriteTag(address, CipDataType.Udint, LogixValue.EncodeUInt32(value), 1);

    /// <summary>写入 ULINT</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    public void WriteUInt64(String address, UInt64 value) => WriteTag(address, CipDataType.Ulint, LogixValue.EncodeUInt64(value), 1);

    /// <summary>写入 REAL</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    public void WriteSingle(String address, Single value) => WriteTag(address, CipDataType.Real, LogixValue.EncodeSingle(value), 1);

    /// <summary>写入 LREAL</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    public void WriteDouble(String address, Double value) => WriteTag(address, CipDataType.Lreal, LogixValue.EncodeDouble(value), 1);

    /// <summary>写入 STRING</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    public void WriteString(String address, String value) =>
        WriteTag(address, CipDataType.String, LogixValue.EncodeString(value, StringCapacity), 1);

    /// <summary>写入字节。地址需带类型提示，或 point 场景之外请改用 WriteTag。</summary>
    /// <param name="address">带类型提示的地址，例如 DINT:Value</param>
    /// <param name="data">元素数据</param>
    public void WriteBytes(String address, Byte[] data)
    {
        var addr = LogixAddress.Parse(address);
        if (!addr.TypeCode.HasValue) throw new ArgumentException("WriteBytes 需要地址中的类型提示，例如 DINT:Tag", nameof(address));
        var size = CipDataType.GetElementSize(addr.TypeCode.Value);
        if (addr.TypeCode.Value == CipDataType.String) size = data == null ? 0 : data.Length;
        if (size <= 0 || data == null || data.Length % size != 0) throw new ArgumentException("数据长度不是元素大小的整数倍");
        WriteTag(address, addr.TypeCode.Value, data, (UInt16)(data.Length / size));
    }

    /// <summary>写入 DINT 数组</summary>
    /// <param name="address">地址</param>
    /// <param name="values">值</param>
    public void WriteInt32(String address, Int32[] values)
    {
        if (values == null) throw new ArgumentNullException(nameof(values));
        var data = new Byte[values.Length * 4];
        for (var i = 0; i < values.Length; i++) BinaryBuffer.WriteUInt32(data, i * 4, (UInt32)values[i]);
        WriteTag(address, CipDataType.Dint, data, (UInt16)values.Length);
    }

    /// <summary>写入 REAL 数组</summary>
    /// <param name="address">地址</param>
    /// <param name="values">值</param>
    public void WriteSingle(String address, Single[] values)
    {
        if (values == null) throw new ArgumentNullException(nameof(values));
        var data = new Byte[values.Length * 4];
        for (var i = 0; i < values.Length; i++)
        {
            var raw = BitConverter.GetBytes(values[i]);
            Buffer.BlockCopy(raw, 0, data, i * 4, 4);
        }
        WriteTag(address, CipDataType.Real, data, (UInt16)values.Length);
    }

    #endregion
}
