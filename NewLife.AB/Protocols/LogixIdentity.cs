using System.Text;

namespace NewLife.AB.Protocols;

/// <summary>控制器身份。来自 ListIdentity 或身份对象 Get_Attributes_All。</summary>
public sealed class LogixIdentity
{
    /// <summary>厂商号</summary>
    public UInt16 VendorId { get; set; }

    /// <summary>设备类型。可编程控制器的公共设备类型是 0x000E。</summary>
    public UInt16 DeviceType { get; set; }

    /// <summary>产品代码</summary>
    public UInt16 ProductCode { get; set; }

    /// <summary>主版本</summary>
    public Byte RevisionMajor { get; set; }

    /// <summary>次版本</summary>
    public Byte RevisionMinor { get; set; }

    /// <summary>状态字</summary>
    public UInt16 Status { get; set; }

    /// <summary>序列号</summary>
    public UInt32 SerialNumber { get; set; }

    /// <summary>产品名称</summary>
    public String ProductName { get; set; }

    /// <summary>状态字节。仅 ListIdentity 提供。</summary>
    public Byte State { get; set; }

    /// <summary>套接字地址。仅 ListIdentity 提供。</summary>
    public String Address { get; set; }

    /// <summary>编码身份对象 Get_Attributes_All 的应答数据</summary>
    /// <returns>字节</returns>
    public Byte[] ToAttributeBytes()
    {
        var name = Encoding.ASCII.GetBytes(ProductName ?? "");
        if (name.Length > 255) throw new InvalidOperationException("产品名称过长");
        var writer = new ByteWriter();
        writer.U16(VendorId);
        writer.U16(DeviceType);
        writer.U16(ProductCode);
        writer.U8(RevisionMajor);
        writer.U8(RevisionMinor);
        writer.U16(Status);
        writer.U32(SerialNumber);
        writer.U8((Byte)name.Length);
        writer.Bytes(name);
        return writer.ToArray();
    }

    /// <summary>解析 Get_Attributes_All 应答数据</summary>
    /// <param name="data">应答数据</param>
    /// <returns>身份</returns>
    public static LogixIdentity ParseAttributes(Byte[] data)
    {
        var reader = new ByteReader(data);
        var identity = ReadCommon(reader);
        return identity;
    }

    /// <summary>编码 ListIdentity 的命令数据</summary>
    /// <param name="port">端口，大端写入 sockaddr</param>
    /// <param name="address">IPv4 地址</param>
    /// <returns>命令数据</returns>
    public Byte[] ToListIdentityBytes(UInt16 port, System.Net.IPAddress address)
    {
        var name = Encoding.ASCII.GetBytes(ProductName ?? "");
        var ip = address == null ? new Byte[] { 0, 0, 0, 0 } : address.GetAddressBytes();
        if (ip.Length != 4) ip = new Byte[] { 0, 0, 0, 0 };

        var body = new ByteWriter();
        body.U16(1);
        body.U8(0x00);
        body.U8(0x02);
        WriteUInt16Big(body, port);
        body.U8(ip[0]);
        body.U8(ip[1]);
        body.U8(ip[2]);
        body.U8(ip[3]);
        body.Bytes(new Byte[8]);
        body.U16(VendorId);
        body.U16(DeviceType);
        body.U16(ProductCode);
        body.U8(RevisionMajor);
        body.U8(RevisionMinor);
        body.U16(Status);
        body.U32(SerialNumber);
        body.U8((Byte)name.Length);
        body.Bytes(name);
        body.U8(State);
        var payload = body.ToArray();

        var writer = new ByteWriter();
        writer.U16(1);
        writer.U16(EipCommonPacket.IdentityItem);
        writer.U16((UInt16)payload.Length);
        writer.Bytes(payload);
        return writer.ToArray();
    }

    /// <summary>解析 ListIdentity 命令数据</summary>
    /// <param name="data">命令数据</param>
    /// <returns>身份</returns>
    public static LogixIdentity ParseListIdentity(Byte[] data)
    {
        var reader = new ByteReader(data);
        var count = reader.U16();
        if (count < 1) throw new InvalidOperationException("ListIdentity 没有身份项");
        var type = reader.U16();
        if (type != EipCommonPacket.IdentityItem) throw new InvalidOperationException("ListIdentity 项类型不是 0x000C");
        var length = reader.U16();
        var item = reader.Bytes(length);
        var inner = new ByteReader(item);
        inner.U16();
        inner.Bytes(2);
        var port = ReadUInt16Big(inner);
        var ip = inner.Bytes(4);
        inner.Bytes(8);
        var identity = ReadCommon(inner);
        identity.Address = ip[0] + "." + ip[1] + "." + ip[2] + "." + ip[3] + ":" + port;
        if (inner.Has(1)) identity.State = inner.U8();
        return identity;
    }

    private static LogixIdentity ReadCommon(ByteReader reader)
    {
        var identity = new LogixIdentity
        {
            VendorId = reader.U16(),
            DeviceType = reader.U16(),
            ProductCode = reader.U16(),
            RevisionMajor = reader.U8(),
            RevisionMinor = reader.U8(),
            Status = reader.U16(),
            SerialNumber = reader.U32()
        };
        var length = reader.U8();
        identity.ProductName = Encoding.ASCII.GetString(reader.Bytes(length));
        return identity;
    }

    private static void WriteUInt16Big(ByteWriter writer, UInt16 value)
    {
        writer.U8((Byte)(value >> 8));
        writer.U8((Byte)value);
    }

    private static UInt16 ReadUInt16Big(ByteReader reader)
    {
        var hi = reader.U8();
        var lo = reader.U8();
        return (UInt16)((hi << 8) | lo);
    }
}
