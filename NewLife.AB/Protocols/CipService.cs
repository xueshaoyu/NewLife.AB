namespace NewLife.AB.Protocols;

/// <summary>CIP 服务码。取自 CIP 公共规范与 Logix 5000 公开的数据访问服务，不使用未公开的私有码。</summary>
public static class CipService
{
    /// <summary>读取全部属性</summary>
    public const Byte GetAttributesAll = 0x01;

    /// <summary>读取单个属性</summary>
    public const Byte GetAttributeSingle = 0x0E;

    /// <summary>多服务打包（Message Router）</summary>
    public const Byte MultipleServicePacket = 0x0A;

    /// <summary>读标签（Logix Read Tag）</summary>
    public const Byte ReadTag = 0x4C;

    /// <summary>写标签（Logix Write Tag）</summary>
    public const Byte WriteTag = 0x4D;

    /// <summary>按位读改写（Logix Read Modify Write Tag）。与 Forward Close 同码，靠路径区分。</summary>
    public const Byte ReadModifyWriteTag = 0x4E;

    /// <summary>分段读标签（Logix Read Tag Fragmented）。与 Unconnected Send 同码，靠路径区分。</summary>
    public const Byte ReadTagFragmented = 0x52;

    /// <summary>分段写标签（Logix Write Tag Fragmented）</summary>
    public const Byte WriteTagFragmented = 0x53;

    /// <summary>连接管理器 Forward Open</summary>
    public const Byte ForwardOpen = 0x54;

    /// <summary>连接管理器 Forward Close。服务码与 Read Modify Write 相同。</summary>
    public const Byte ForwardClose = 0x4E;

    /// <summary>连接管理器 Unconnected Send。服务码与 Read Tag Fragmented 相同。</summary>
    public const Byte UnconnectedSend = 0x52;

    /// <summary>应答服务的最高位</summary>
    public const Byte ReplyFlag = 0x80;

    /// <summary>身份对象</summary>
    public const Byte ClassIdentity = 0x01;

    /// <summary>消息路由器</summary>
    public const Byte ClassMessageRouter = 0x02;

    /// <summary>连接管理器</summary>
    public const Byte ClassConnectionManager = 0x06;

    /// <summary>Logix 符号对象。符号实例寻址留作扩展，首发使用符号段。</summary>
    public const Byte ClassSymbol = 0x6B;

    /// <summary>是否为指定服务的应答</summary>
    /// <param name="service">应答中的服务字节</param>
    /// <param name="request">请求服务码</param>
    /// <returns>是否匹配</returns>
    public static Boolean IsReply(Byte service, Byte request) => service == (Byte)(request | ReplyFlag);
}
