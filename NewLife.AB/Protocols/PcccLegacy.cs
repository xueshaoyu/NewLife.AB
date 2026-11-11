namespace NewLife.AB.Protocols;

/// <summary>SLC / PLC-5 / MicroLogix 的 PCCC（DF1）扩展点。首发不实现这些家族。</summary>
/// <remarks>
/// 后续可在已注册的 EtherNet/IP 会话上发送 Execute PCCC（服务 0x4B），把 DF1 命令嵌入 CIP。
/// 调用本类型会明确失败，避免把未实现的家族当成已支持。
/// </remarks>
public static class PcccLegacy
{
    /// <summary>CIP Execute PCCC 服务码</summary>
    public const Byte ExecutePccc = 0x4B;

    /// <summary>PCCC 对象类（历史嵌入路径中常见）</summary>
    public const Byte ClassPccc = 0x67;

    /// <summary>说明首发未实现，并抛出 NotSupportedException</summary>
    public static void ThrowNotSupported() =>
        throw new NotSupportedException("SLC、PLC-5、MicroLogix 的 PCCC/DF1 不在 NewLife.AB 首发范围内。请使用 Logix 5000 的符号标签。扩展时可复用 EipPacket 会话，发送服务 0x4B。");
}
