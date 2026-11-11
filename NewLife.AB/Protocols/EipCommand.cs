namespace NewLife.AB.Protocols;

/// <summary>EtherNet/IP 封装命令（Encapsulation Commands，CIP Volume 2）</summary>
public static class EipCommand
{
    /// <summary>空操作，无应答</summary>
    public const UInt16 Nop = 0x0000;

    /// <summary>列出通信服务</summary>
    public const UInt16 ListServices = 0x0004;

    /// <summary>列出身份</summary>
    public const UInt16 ListIdentity = 0x0063;

    /// <summary>注册会话</summary>
    public const UInt16 RegisterSession = 0x0065;

    /// <summary>注销会话，无应答</summary>
    public const UInt16 UnRegisterSession = 0x0066;

    /// <summary>发送未连接的请求/响应数据（UCMM，SendRRData）</summary>
    public const UInt16 SendRRData = 0x006F;

    /// <summary>在已建立的连接上发送数据（SendUnitData）</summary>
    public const UInt16 SendUnitData = 0x0070;
}
