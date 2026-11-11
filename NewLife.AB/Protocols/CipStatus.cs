namespace NewLife.AB.Protocols;

/// <summary>CIP 通用状态码（Volume 1 Appendix B）及 Logix 公开扩展状态</summary>
public static class CipStatus
{
    /// <summary>成功</summary>
    public const Byte Success = 0x00;

    /// <summary>连接失败</summary>
    public const Byte ConnectionFailure = 0x01;

    /// <summary>资源不可用</summary>
    public const Byte ResourceUnavailable = 0x02;

    /// <summary>参数值无效</summary>
    public const Byte InvalidParameter = 0x03;

    /// <summary>路径段错误</summary>
    public const Byte PathSegmentError = 0x04;

    /// <summary>路径目标未知（标签不存在时 Logix 常用此码）</summary>
    public const Byte PathDestinationUnknown = 0x05;

    /// <summary>部分传输。读标签装不下时返回，并带上已放入的数据。</summary>
    public const Byte PartialTransfer = 0x06;

    /// <summary>服务不支持</summary>
    public const Byte ServiceNotSupported = 0x08;

    /// <summary>权限不足</summary>
    public const Byte PrivilegeViolation = 0x0F;

    /// <summary>设备状态冲突</summary>
    public const Byte DeviceStateConflict = 0x10;

    /// <summary>应答数据过大</summary>
    public const Byte ReplyDataTooLarge = 0x11;

    /// <summary>请求数据不足</summary>
    public const Byte NotEnoughData = 0x13;

    /// <summary>属性不支持</summary>
    public const Byte AttributeNotSupported = 0x14;

    /// <summary>数据过多</summary>
    public const Byte TooMuchData = 0x15;

    /// <summary>对象不存在</summary>
    public const Byte ObjectDoesNotExist = 0x16;

    /// <summary>嵌入服务错误（多服务打包中有子服务失败）</summary>
    public const Byte EmbeddedServiceError = 0x1E;

    /// <summary>路径长度无效</summary>
    public const Byte PathSizeInvalid = 0x26;

    /// <summary>Logix 扩展：偏移超出标签</summary>
    public const UInt16 ExtOffsetBeyondEnd = 0x2104;

    /// <summary>Logix 扩展：元素数量或偏移超出标签末尾</summary>
    public const UInt16 ExtBeyondEnd = 0x2105;

    /// <summary>Logix 扩展：请求的数据类型与标签不符</summary>
    public const UInt16 ExtTypeMismatch = 0x2107;

    /// <summary>状态说明</summary>
    /// <param name="general">通用状态</param>
    /// <param name="extended">扩展状态，可为 null</param>
    /// <returns>中文说明</returns>
    public static String GetMessage(Byte general, UInt16[] extended)
    {
        if (extended != null && extended.Length > 0)
        {
            switch (extended[0])
            {
                case ExtOffsetBeyondEnd: return "偏移超出标签末尾";
                case ExtBeyondEnd: return "访问超出标签末尾";
                case ExtTypeMismatch: return "数据类型与标签不符";
            }
        }

        switch (general)
        {
            case Success: return "成功";
            case ConnectionFailure: return "连接失败";
            case ResourceUnavailable: return "资源不可用";
            case InvalidParameter: return "参数无效";
            case PathSegmentError: return "路径段错误";
            case PathDestinationUnknown: return "路径目标未知";
            case PartialTransfer: return "数据未完全放入应答";
            case ServiceNotSupported: return "服务不支持";
            case PrivilegeViolation: return "权限不足";
            case DeviceStateConflict: return "设备状态冲突";
            case ReplyDataTooLarge: return "应答数据过大";
            case NotEnoughData: return "请求数据不足";
            case AttributeNotSupported: return "属性不支持";
            case TooMuchData: return "数据过多";
            case ObjectDoesNotExist: return "对象不存在";
            case EmbeddedServiceError: return "嵌入服务错误";
            case PathSizeInvalid: return "路径长度无效";
            default: return "CIP 状态 0x" + general.ToString("X2");
        }
    }
}
