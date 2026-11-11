using System.ComponentModel;
using NewLife.IoT.Drivers;

namespace NewLife.AB.Drivers;

/// <summary>AB PLC 驱动参数</summary>
public class AbParameter : IDriverParameter
{
    /// <summary>地址。例如 127.0.0.1:44818</summary>
    [Description("地址。例如 127.0.0.1:44818")]
    public String Address { get; set; }

    /// <summary>CPU 槽号。默认 0。</summary>
    [Description("CPU 槽号。默认 0")]
    public Int32 Slot { get; set; }

    /// <summary>自定义路由。为空时使用 1,槽号。</summary>
    [Description("自定义路由。例如 1,0 或 2,192.168.1.10,1,0")]
    public String RoutePath { get; set; }

    /// <summary>是否经背板路由。默认 true。</summary>
    [Description("是否经背板路由。默认 true")]
    public Boolean RouteToSlot { get; set; } = true;
}
