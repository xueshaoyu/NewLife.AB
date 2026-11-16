using System.Net.Sockets;
using NewLife.Data;
using NewLife.Net;
using NewLife.Log;

namespace NewLife.AB.Protocols;

/// <summary>LogixClient 的异步方法</summary>
public partial class LogixClient
{
    private readonly SemaphoreSlim _asyncLock = new SemaphoreSlim(1, 1);

    /// <summary>异步连接并注册会话</summary>
    /// <returns>任务</returns>
    public async Task ConnectAsync()
    {
        await _asyncLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await ConnectCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _asyncLock.Release();
        }
    }

    /// <summary>异步读取标签</summary>
    /// <param name="address">地址</param>
    /// <param name="count">元素个数</param>
    /// <returns>值</returns>
    public async Task<LogixValue> ReadTagAsync(String address, UInt16 count = 1)
    {
        var addr = LogixAddress.Parse(address);
        if (count == 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (addr.Bit.HasValue)
        {
            if (count != 1) throw new ArgumentException("位地址只能读取一个元素", nameof(address));
            var raw = await ReadRawAsync(WithoutBit(addr), 1).ConfigureAwait(false);
            return LogixValue.FromBoolean(ExtractBit(raw, addr.Bit.Value));
        }

        return await ReadRawAsync(addr, count).ConfigureAwait(false);
    }

    /// <summary>异步写入原始元素数据</summary>
    /// <param name="address">地址</param>
    /// <param name="typeCode">类型码</param>
    /// <param name="data">元素数据</param>
    /// <param name="count">元素个数</param>
    /// <returns>任务</returns>
    public async Task WriteTagAsync(String address, UInt16 typeCode, Byte[] data, UInt16 count)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (count == 0) throw new ArgumentOutOfRangeException(nameof(count));
        var addr = LogixAddress.Parse(address);
        if (addr.Bit.HasValue) throw new ArgumentException("位地址请使用 WriteBooleanAsync", nameof(address));
        var handle = typeCode == CipDataType.String ? await ResolveStringHandleAsync(address).ConfigureAwait(false) : (UInt16)0;
        await WritePayloadAsync(addr, typeCode, handle, data, count).ConfigureAwait(false);
    }

    /// <summary>异步读取多个标签</summary>
    /// <param name="addresses">地址</param>
    /// <returns>值</returns>
    public async Task<LogixValue[]> ReadTagsAsync(String[] addresses)
    {
        if (addresses == null) throw new ArgumentNullException(nameof(addresses));
        var results = new LogixValue[addresses.Length];
        for (var i = 0; i < addresses.Length; i++)
        {
            results[i] = await ReadTagAsync(addresses[i], 1).ConfigureAwait(false);
        }
        return results;
    }

    /// <summary>异步列出身份</summary>
    /// <returns>身份</returns>
    public async Task<LogixIdentity> ListIdentityAsync()
    {
        await _asyncLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!IsConnected) await ConnectCoreAsync().ConfigureAwait(false);
            var context = NextContext();
            var packet = await SendPacketAsync(new EipPacket
            {
                Command = EipCommand.ListIdentity,
                SessionHandle = _session,
                SenderContext = context
            }).ConfigureAwait(false);
            if (packet.Status != EipStatus.Success) throw CipException.FromEncapsulation(packet.Status);
            return LogixIdentity.ParseListIdentity(packet.Data);
        }
        finally
        {
            _asyncLock.Release();
        }
    }

    /// <summary>异步读取身份对象</summary>
    /// <returns>身份</returns>
    public async Task<LogixIdentity> ReadIdentityAsync()
    {
        var response = await ExecuteAsync(CipMessage.ReadIdentity().ToRequestBytes(), true).ConfigureAwait(false);
        if (!response.IsSuccess) throw CipException.FromResponse(response, "读取身份");
        return LogixIdentity.ParseAttributes(response.Data);
    }

    /// <summary>异步读取 BOOL</summary>
    /// <param name="address">地址</param>
    /// <returns>值</returns>
    public async Task<Boolean> ReadBooleanAsync(String address) => (await ReadTagAsync(address, 1).ConfigureAwait(false)).GetBoolean();

    /// <summary>异步读取 INT</summary>
    /// <param name="address">地址</param>
    /// <returns>值</returns>
    public async Task<Int16> ReadInt16Async(String address) => (await ReadTagAsync(address, 1).ConfigureAwait(false)).GetInt16();

    /// <summary>异步读取 DINT</summary>
    /// <param name="address">地址</param>
    /// <returns>值</returns>
    public async Task<Int32> ReadInt32Async(String address) => (await ReadTagAsync(address, 1).ConfigureAwait(false)).GetInt32();

    /// <summary>异步读取 LINT</summary>
    /// <param name="address">地址</param>
    /// <returns>值</returns>
    public async Task<Int64> ReadInt64Async(String address) => (await ReadTagAsync(address, 1).ConfigureAwait(false)).GetInt64();

    /// <summary>异步读取 UINT</summary>
    /// <param name="address">地址</param>
    /// <returns>值</returns>
    public async Task<UInt16> ReadUInt16Async(String address) => (await ReadTagAsync(address, 1).ConfigureAwait(false)).GetUInt16();

    /// <summary>异步读取 UDINT</summary>
    /// <param name="address">地址</param>
    /// <returns>值</returns>
    public async Task<UInt32> ReadUInt32Async(String address) => (await ReadTagAsync(address, 1).ConfigureAwait(false)).GetUInt32();

    /// <summary>异步读取 ULINT</summary>
    /// <param name="address">地址</param>
    /// <returns>值</returns>
    public async Task<UInt64> ReadUInt64Async(String address) => (await ReadTagAsync(address, 1).ConfigureAwait(false)).GetUInt64();

    /// <summary>异步读取 REAL</summary>
    /// <param name="address">地址</param>
    /// <returns>值</returns>
    public async Task<Single> ReadSingleAsync(String address) => (await ReadTagAsync(address, 1).ConfigureAwait(false)).GetSingle();

    /// <summary>异步读取 LREAL</summary>
    /// <param name="address">地址</param>
    /// <returns>值</returns>
    public async Task<Double> ReadDoubleAsync(String address) => (await ReadTagAsync(address, 1).ConfigureAwait(false)).GetDouble();

    /// <summary>异步读取 STRING</summary>
    /// <param name="address">地址</param>
    /// <returns>值</returns>
    public async Task<String> ReadStringAsync(String address) => (await ReadTagAsync(address, 1).ConfigureAwait(false)).GetString();

    /// <summary>异步读取 DINT 数组</summary>
    /// <param name="address">地址</param>
    /// <param name="count">元素个数</param>
    /// <returns>数组</returns>
    public async Task<Int32[]> ReadInt32Async(String address, UInt16 count) => (Int32[])(await ReadTagAsync(address, count).ConfigureAwait(false)).ToArray();

    /// <summary>异步写入 BOOL</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    /// <returns>任务</returns>
    public async Task WriteBooleanAsync(String address, Boolean value)
    {
        var addr = LogixAddress.Parse(address);
        if (!addr.Bit.HasValue)
        {
            await WritePayloadAsync(addr, CipDataType.Bool, 0, LogixValue.EncodeBoolean(value), 1).ConfigureAwait(false);
            return;
        }

        var size = await ResolveElementSizeAsync(addr).ConfigureAwait(false);
        Byte[] orMask;
        Byte[] andMask;
        CipMessage.BuildBitMasks(size, addr.Bit.Value, value, out orMask, out andMask);
        var response = await ExecuteAsync(CipMessage.ReadModifyWrite(WithoutBit(addr).ToPath(), orMask, andMask).ToRequestBytes(), true).ConfigureAwait(false);
        if (!response.IsSuccess) throw CipException.FromResponse(response, "写位 " + address);
    }

    /// <summary>异步写入 INT</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    /// <returns>任务</returns>
    public Task WriteInt16Async(String address, Int16 value) => WriteTagAsync(address, CipDataType.Int, LogixValue.EncodeInt16(value), 1);

    /// <summary>异步写入 DINT</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    /// <returns>任务</returns>
    public Task WriteInt32Async(String address, Int32 value) => WriteTagAsync(address, CipDataType.Dint, LogixValue.EncodeInt32(value), 1);

    /// <summary>异步写入 LINT</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    /// <returns>任务</returns>
    public Task WriteInt64Async(String address, Int64 value) => WriteTagAsync(address, CipDataType.Lint, LogixValue.EncodeInt64(value), 1);

    /// <summary>异步写入 UINT</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    /// <returns>任务</returns>
    public Task WriteUInt16Async(String address, UInt16 value) => WriteTagAsync(address, CipDataType.Uint, LogixValue.EncodeUInt16(value), 1);

    /// <summary>异步写入 UDINT</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    /// <returns>任务</returns>
    public Task WriteUInt32Async(String address, UInt32 value) => WriteTagAsync(address, CipDataType.Udint, LogixValue.EncodeUInt32(value), 1);

    /// <summary>异步写入 ULINT</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    /// <returns>任务</returns>
    public Task WriteUInt64Async(String address, UInt64 value) => WriteTagAsync(address, CipDataType.Ulint, LogixValue.EncodeUInt64(value), 1);

    /// <summary>异步写入 REAL</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    /// <returns>任务</returns>
    public Task WriteSingleAsync(String address, Single value) => WriteTagAsync(address, CipDataType.Real, LogixValue.EncodeSingle(value), 1);

    /// <summary>异步写入 LREAL</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    /// <returns>任务</returns>
    public Task WriteDoubleAsync(String address, Double value) => WriteTagAsync(address, CipDataType.Lreal, LogixValue.EncodeDouble(value), 1);

    /// <summary>异步写入 STRING</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    /// <returns>任务</returns>
    public Task WriteStringAsync(String address, String value) => WriteTagAsync(address, CipDataType.String, LogixValue.EncodeString(value, StringCapacity), 1);

    private async Task ConnectCoreAsync()
    {
        if (IsConnected) return;
        if (Host == null || Host.Length == 0) throw new ArgumentException("未指定主机");

        ISocketClient client = null;
        try
        {
            client = CreateSocket();
            if (!await client.OpenAsync(CancellationToken.None).ConfigureAwait(false))
                throw new TimeoutException("连接超时: " + Host + ":" + Port);
            ApplyTimeouts(client);
            _client = client;
            _inbox.Clear();
            await RegisterSessionAsync().ConfigureAwait(false);
            if (ConnectedMessaging) await OpenConnectionAsync().ConfigureAwait(false);
            XTrace.WriteLine("Logix 已连接: " + Host + ":" + Port + " 会话 0x" + _session.ToString("X8"));
        }
        catch (Exception ex)
        {
            try { client?.Close("connect"); } catch { }
            client.TryDispose();
            _client = null;
            _inbox.Clear();
            _session = 0;
            if (ex is ArgumentException || ex is CipException || ex is TimeoutException) throw;
            throw new InvalidOperationException("连接失败: " + ex.Message, ex);
        }
    }

    private async Task RegisterSessionAsync()
    {
        var response = await SendPacketAsync(new EipPacket
        {
            Command = EipCommand.RegisterSession,
            SenderContext = NextContext(),
            Data = new Byte[] { 0x01, 0x00, 0x00, 0x00 }
        }).ConfigureAwait(false);
        if (response.Status != EipStatus.Success) throw CipException.FromEncapsulation(response.Status);
        if (response.SessionHandle == 0) throw new InvalidOperationException("注册会话未返回句柄");
        _session = response.SessionHandle;
    }

    private async Task OpenConnectionAsync()
    {
        _connectionSerial = (UInt16)_random.Next(1, Int32.MaxValue);
        _originatorSerial = (UInt32)_random.Next(1, Int32.MaxValue);
        var o2t = (UInt32)_random.Next(1, Int32.MaxValue);
        var t2o = (UInt32)_random.Next(1, Int32.MaxValue);
        _connectionPath = BuildConnectionPath();
        var request = CipMessage.BuildForwardOpen(o2t, t2o, _connectionSerial, OriginatorVendorId, _originatorSerial, 100000, ExplicitConnectionParameters, ExplicitTransportTrigger, _connectionPath);
        var response = await ExecuteCoreAsync(request.ToRequestBytes(), false).ConfigureAwait(false);
        if (!response.IsSuccess) throw CipException.FromResponse(response, "Forward Open");
        if (response.Data == null || response.Data.Length < 8) throw new InvalidOperationException("Forward Open 应答过短");
        _o2tConnectionId = BinaryBuffer.ReadUInt32(response.Data, 0);
    }

    private async Task<CipMessage> ExecuteAsync(Byte[] cipRequest, Boolean allowRoute)
    {
        await _asyncLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var max = AutoReconnect ? MaxReconnectRetries : 0;
            for (var retry = 0; retry <= max; retry++)
            {
                try
                {
                    if (retry > 0) await SleepRetryAsync(retry).ConfigureAwait(false);
                    if (!IsConnected) await ConnectCoreAsync().ConfigureAwait(false);
                    return await ExecuteCoreAsync(cipRequest, allowRoute && RouteToSlot && _o2tConnectionId == 0).ConfigureAwait(false);
                }
                catch (CipException ex) when (ex.EncapsulationStatus == EipStatus.InvalidSession && AutoReconnect && retry < max)
                {
                    _o2tConnectionId = 0;
                    try
                    {
                        await RegisterSessionAsync().ConfigureAwait(false);
                        if (ConnectedMessaging) await OpenConnectionAsync().ConfigureAwait(false);
                    }
                    catch { CloseCore(); }
                }
                catch (Exception ex) when (IsConnectionError(ex) && AutoReconnect && retry < max)
                {
                    CloseCore();
                }
            }
        }
        finally
        {
            _asyncLock.Release();
        }

        throw new InvalidOperationException("通信失败，已超过最大重试次数");
    }

    private async Task<CipMessage> ExecuteCoreAsync(Byte[] cipRequest, Boolean route)
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
        var packet = await SendPacketAsync(new EipPacket
        {
            Command = command,
            SessionHandle = _session,
            SenderContext = context,
            Data = payload
        }).ConfigureAwait(false);
        return DecodeTransport(packet, context, route);
    }

    private async Task<EipPacket> SendPacketAsync(EipPacket packet)
    {
        if (_client == null || !_client.Active) throw new InvalidOperationException("未连接到服务器");
        ApplyTimeouts(_client);
        var bytes = packet.ToBytes();
        _client.Send(bytes, 0, bytes.Length);
        return DecodeFrame(await ReadFrameAsync().ConfigureAwait(false));
    }

    private async Task<Byte[]> ReadFrameAsync()
    {
        while (true)
        {
            if (TryTakeFrame(out var frame)) return frame;
            IOwnerPacket packet;
            try
            {
                using var cts = new CancellationTokenSource(ReceiveTimeout);
                packet = await _client.ReceiveAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException("接收数据超时");
            }
            catch (SocketException ex) when (IsTimeout(ex))
            {
                throw new TimeoutException("接收数据超时", ex);
            }
            AppendInbox(packet);
        }
    }

    private async Task SleepRetryAsync(Int32 retry)
    {
        var delay = RetryDelayMilliseconds * retry;
        if (delay > 0) await Task.Delay(delay).ConfigureAwait(false);
    }

    private async Task<LogixValue> ReadRawAsync(LogixAddress address, UInt16 count)
    {
        var path = address.ToPath();
        var response = await ExecuteAsync(CipMessage.ReadTag(path, count).ToRequestBytes(), true).ConfigureAwait(false);
        if (response.GeneralStatus != CipStatus.Success && response.GeneralStatus != CipStatus.PartialTransfer)
            throw CipException.FromResponse(response, "读取 " + address);

        var header = TakeHeader(response.Data);
        var payload = new ByteWriter();
        payload.Bytes(ElementBytes(response.Data, header.Length));
        var offset = payload.Length;
        while (response.GeneralStatus == CipStatus.PartialTransfer)
        {
            response = await ExecuteAsync(CipMessage.ReadTagFragmented(path, count, (UInt32)offset).ToRequestBytes(), true).ConfigureAwait(false);
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

    private async Task WritePayloadAsync(LogixAddress address, UInt16 typeCode, UInt16 handle, Byte[] payload, UInt16 count)
    {
        var path = address.ToPath();
        if (WriteFragmentSize < 1) throw new ArgumentOutOfRangeException(nameof(WriteFragmentSize));
        if (payload.Length <= WriteFragmentSize)
        {
            var response = await ExecuteAsync(CipMessage.WriteTag(path, typeCode, handle, count, payload).ToRequestBytes(), true).ConfigureAwait(false);
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
            var response = await ExecuteAsync(CipMessage.WriteTagFragmented(path, typeCode, handle, count, (UInt32)offset, slice).ToRequestBytes(), true).ConfigureAwait(false);
            if (!response.IsSuccess) throw CipException.FromResponse(response, "分段写入 " + address);
            offset += size;
        }
    }

    private async Task<UInt16> ResolveStringHandleAsync(String address)
    {
        if (StringStructureHandle.HasValue) return StringStructureHandle.Value;
        var key = LogixAddress.Parse(address).Key;
        lock (_stringHandles)
        {
            if (_stringHandles.TryGetValue(key, out var cached)) return cached;
        }

        try
        {
            var value = await ReadTagAsync(address, 1).ConfigureAwait(false);
            return value.StructureHandle;
        }
        catch (CipException ex) when (ex.GeneralStatus == CipStatus.PathDestinationUnknown)
        {
            return 0;
        }
    }

    private async Task<Int32> ResolveElementSizeAsync(LogixAddress address)
    {
        if (address.TypeCode.HasValue)
        {
            var hinted = CipDataType.GetElementSize(address.TypeCode.Value);
            if (hinted > 0) return hinted;
        }

        var value = await ReadRawAsync(WithoutBit(address), 1).ConfigureAwait(false);
        var size = CipDataType.GetElementSize(value.TypeCode);
        if (size <= 0) throw new InvalidOperationException("无法确定位操作的元素宽度");
        return size;
    }
}
