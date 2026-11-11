namespace NewLife.AB.Protocols;

/// <summary>CIP 或 EtherNet/IP 封装层返回的失败</summary>
public class CipException : Exception
{
    /// <summary>CIP 通用状态。非 CIP 失败时为 0。</summary>
    public Byte GeneralStatus { get; }

    /// <summary>CIP 扩展状态</summary>
    public UInt16[] ExtendedStatus { get; }

    /// <summary>封装头状态。非封装失败时为 0。</summary>
    public UInt32 EncapsulationStatus { get; }

    /// <summary>实例化 CIP 失败</summary>
    /// <param name="message">说明</param>
    /// <param name="generalStatus">通用状态</param>
    /// <param name="extendedStatus">扩展状态</param>
    /// <param name="encapsulationStatus">封装状态</param>
    public CipException(String message, Byte generalStatus, UInt16[] extendedStatus, UInt32 encapsulationStatus)
        : base(message)
    {
        GeneralStatus = generalStatus;
        ExtendedStatus = extendedStatus ?? new UInt16[0];
        EncapsulationStatus = encapsulationStatus;
    }

    /// <summary>由 CIP 应答构造异常</summary>
    /// <param name="response">应答</param>
    /// <param name="action">动作说明</param>
    /// <returns>异常</returns>
    public static CipException FromResponse(CipMessage response, String action)
    {
        var text = CipStatus.GetMessage(response.GeneralStatus, response.ExtendedStatus);
        var ext = response.ExtendedStatus != null && response.ExtendedStatus.Length > 0
            ? "，扩展 0x" + response.ExtendedStatus[0].ToString("X4")
            : "";
        return new CipException(
            action + "失败：" + text + "（0x" + response.GeneralStatus.ToString("X2") + ext + "）",
            response.GeneralStatus,
            response.ExtendedStatus,
            0);
    }

    /// <summary>由封装状态构造异常</summary>
    /// <param name="status">封装状态</param>
    /// <returns>异常</returns>
    public static CipException FromEncapsulation(UInt32 status) =>
        new CipException("EtherNet/IP 封装失败：" + EipStatus.GetMessage(status), 0, new UInt16[0], status);
}
