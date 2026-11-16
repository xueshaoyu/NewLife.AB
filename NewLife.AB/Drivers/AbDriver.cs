using System.ComponentModel;
using NewLife.AB.Protocols;
using NewLife.IoT;
using NewLife.IoT.Drivers;
using NewLife.IoT.ThingModels;

namespace NewLife.AB.Drivers;

/// <summary>罗克韦尔 AB PLC 驱动</summary>
[Driver("ABPLC")]
[DisplayName("AB PLC")]
public class AbDriver : DriverBase
{
    private LogixClient _client;
    private Int32 _nodes;

    /// <summary>创建参数模板</summary>
    /// <returns>参数</returns>
    protected override IDriverParameter OnCreateParameter() => new AbParameter
    {
        Address = "127.0.0.1:44818",
        Slot = 0,
        RouteToSlot = true
    };

    /// <summary>点位地址原样返回。冒号和点号属于标签路径，不能截断。</summary>
    /// <param name="point">点位</param>
    /// <returns>地址</returns>
    public virtual String GetAddress(IPoint point)
    {
        if (point == null) throw new ArgumentException("点位信息不能为空！");
        if (point.Address.IsNullOrEmpty()) throw new ArgumentException("点位地址不能为空！");
        return point.Address.Trim();
    }

    /// <summary>打开通道。同一驱动上的多个节点共享一个 Logix 连接。</summary>
    /// <param name="device">设备</param>
    /// <param name="parameter">参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>节点</returns>
    public override Task<INode> OpenAsync(IDevice device, IDriverParameter parameter, CancellationToken cancellationToken = default)
    {
        var pm = parameter as AbParameter;
        var address = pm?.Address;
        if (address.IsNullOrEmpty()) throw new ArgumentException("参数中未指定地址address");

        String host;
        Int32 port;
        ParseEndpoint(address, out host, out port);

        var node = new AbNode
        {
            Address = address,
            Driver = this,
            Device = device,
            Parameter = pm
        };

        if (_client == null)
        {
            lock (this)
            {
                if (_client == null)
                {
                    var client = new LogixClient
                    {
                        Host = host,
                        Port = port,
                        Slot = pm.Slot,
                        RoutePath = pm.RoutePath,
                        RouteToSlot = pm.RouteToSlot,
                        ConnectTimeout = 3000,
                        ReceiveTimeout = 5000
                    };
                    client.Connect();
                    _client = client;
                }
            }
        }

        Interlocked.Increment(ref _nodes);
        return TaskEx.FromResult<INode>(node);
    }

    /// <summary>关闭。最后一个节点关闭时释放连接。</summary>
    /// <param name="node">节点</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public override Task CloseAsync(INode node, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Decrement(ref _nodes) <= 0)
        {
            _client?.Close();
            _client.TryDispose();
            _client = null;
        }

        return TaskEx.CompletedTask;
    }

    /// <summary>读取点位。指定类型时 Length 为字节数，再换成元素个数；未指定类型且 Length 大于 1 时，Length 就是元素个数。</summary>
    /// <param name="node">节点</param>
    /// <param name="points">点位</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>结果</returns>
    public override Task<ReadResult> ReadAsync(INode node, IPoint[] points, CancellationToken cancellationToken = default)
    {
        if (points == null || points.Length == 0)
            return TaskEx.FromResult(ReadResult.Success(new IPoint[0], new Object[0]));

        var values = new Object[points.Length];
        var simple = new List<Int32>();
        for (var i = 0; i < points.Length; i++)
        {
            var count = ElementCount(points[i]);
            if (count == 1) simple.Add(i);
            else values[i] = _client.ReadTag(GetAddress(points[i]), count).ToObject();
        }

        if (simple.Count == points.Length)
        {
            var addresses = new String[points.Length];
            for (var i = 0; i < points.Length; i++) addresses[i] = GetAddress(points[i]);
            var tags = _client.ReadTags(addresses);
            for (var i = 0; i < tags.Length; i++) values[i] = tags[i].ToObject();
        }
        else
        {
            foreach (var index in simple)
            {
                values[index] = _client.ReadTag(GetAddress(points[index]), 1).ToObject();
            }
        }

        return TaskEx.FromResult(ReadResult.Success(points, values));
    }

    /// <summary>写入点位</summary>
    /// <param name="node">节点</param>
    /// <param name="requests">请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>结果</returns>
    public override Task<WriteResult> WriteAsync(INode node, WriteRequest[] requests, CancellationToken cancellationToken = default)
    {
        if (requests == null) throw new ArgumentNullException(nameof(requests));
        foreach (var request in requests)
        {
            var point = request.Point;
            var value = request.Value;
            var address = GetAddress(point);
            WriteValue(address, point, value);
        }

        return TaskEx.FromResult(WriteResult.SuccessBatch(requests.Length));
    }

    private void WriteValue(String address, IPoint point, Object value)
    {
        var addr = LogixAddress.Parse(address);
        if (value is Boolean boolean)
        {
            _client.WriteBoolean(address, boolean);
            return;
        }
        if (value is SByte sb)
        {
            _client.WriteSByte(address, sb);
            return;
        }
        if (value is Byte b)
        {
            _client.WriteByte(address, b);
            return;
        }
        if (value is Int16 i16)
        {
            _client.WriteInt16(address, i16);
            return;
        }
        if (value is UInt16 u16)
        {
            _client.WriteUInt16(address, u16);
            return;
        }
        if (value is Int32 i32)
        {
            _client.WriteInt32(address, i32);
            return;
        }
        if (value is UInt32 u32)
        {
            _client.WriteUInt32(address, u32);
            return;
        }
        if (value is Int64 i64)
        {
            _client.WriteInt64(address, i64);
            return;
        }
        if (value is UInt64 u64)
        {
            _client.WriteUInt64(address, u64);
            return;
        }
        if (value is Single single)
        {
            _client.WriteSingle(address, single);
            return;
        }
        if (value is Double dbl)
        {
            _client.WriteDouble(address, dbl);
            return;
        }
        if (value is String text)
        {
            _client.WriteString(address, text);
            return;
        }
        if (value is Byte[] raw)
        {
            UInt16 typeCode;
            if (point != null && CipDataType.TryParseName(point.Type, out typeCode))
            {
                _client.WriteTag(address, typeCode, raw, 1);
                return;
            }
            if (addr.TypeCode.HasValue)
            {
                _client.WriteBytes(address, raw);
                return;
            }
            throw new ArgumentException("写入字节数组时需要点位类型或地址中的类型提示");
        }

        throw new ArgumentException("暂不支持写入该类型数据: " + (value == null ? "null" : value.GetType().Name));
    }

    private static UInt16 ElementCount(IPoint point)
    {
        var length = point == null ? 0 : point.Length;
        if (length <= 1) return 1;
        UInt16 typeCode;
        if (point.Type != null && CipDataType.TryParseName(point.Type, out typeCode))
        {
            if (typeCode == CipDataType.String) return 1;
            var size = CipDataType.GetElementSize(typeCode);
            if (size > 0 && length >= size) return (UInt16)(length / size);
            return 1;
        }

        if (length > UInt16.MaxValue) throw new ArgumentOutOfRangeException(nameof(point), "元素个数超过 65535");
        return (UInt16)length;
    }

    private static void ParseEndpoint(String address, out String host, out Int32 port)
    {
        var p = address.LastIndexOf(':');
        if (p <= 0 || p >= address.Length - 1) throw new ArgumentException("参数中地址address格式错误:" + address);
        host = address.Substring(0, p);
        var portText = address.Substring(p + 1);
        if (!Int32.TryParse(portText, out port) || port <= 0 || port > 65535)
            throw new ArgumentException("参数中地址address格式错误:" + address);
    }
}
