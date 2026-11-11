namespace NewLife.AB.Protocols;

/// <summary>EtherNet/IP 封装头状态码</summary>
public static class EipStatus
{
    /// <summary>成功</summary>
    public const UInt32 Success = 0x0000;

    /// <summary>命令无效或格式错误</summary>
    public const UInt32 InvalidCommand = 0x0001;

    /// <summary>内存不足</summary>
    public const UInt32 InsufficientMemory = 0x0002;

    /// <summary>数据不正确</summary>
    public const UInt32 IncorrectData = 0x0003;

    /// <summary>会话句柄无效，常见于会话超时</summary>
    public const UInt32 InvalidSession = 0x0064;

    /// <summary>长度无效</summary>
    public const UInt32 InvalidLength = 0x0065;

    /// <summary>不支持的封装协议版本</summary>
    public const UInt32 UnsupportedProtocol = 0x0069;

    /// <summary>状态说明</summary>
    /// <param name="status">封装状态</param>
    /// <returns>中文说明</returns>
    public static String GetMessage(UInt32 status)
    {
        switch (status)
        {
            case Success: return "成功";
            case InvalidCommand: return "无效命令";
            case InsufficientMemory: return "内存不足";
            case IncorrectData: return "数据不正确";
            case InvalidSession: return "会话无效或已超时";
            case InvalidLength: return "长度无效";
            case UnsupportedProtocol: return "不支持的协议版本";
            default: return "封装状态 0x" + status.ToString("X8");
        }
    }
}
