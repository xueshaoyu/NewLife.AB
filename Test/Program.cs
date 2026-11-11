using NewLife.AB.Drivers;
using NewLife.AB.Protocols;
using NewLife.IoT.Drivers;
using NewLife.IoT.ThingModels;

// 指向真实 PLC：dotnet run --project Test -- 192.168.1.10:44818 --slot 0 --tag Program:MainProgram.Counter
// 不带参数时进入交互模式，默认 127.0.0.1:44818、槽号 0。
var options = Options.Parse(args);
Console.WriteLine("NewLife.AB Logix 控制台");
Console.WriteLine("主机 {0}  槽号 {1}  路由 {2}", options.Endpoint, options.Slot, options.RouteToSlot ? "1," + options.Slot : "直接");

if (options.Tag != null)
{
    using var client = CreateClient(options);
    client.Connect();
    if (options.WriteValue != null)
    {
        client.WriteInt32(options.Tag, Int32.Parse(options.WriteValue));
        Console.WriteLine("已写入 {0} = {1}", options.Tag, options.WriteValue);
    }
    var value = client.ReadTag(options.Tag, 1);
    Console.WriteLine("读取 {0} 类型 {1} 值 {2}", options.Tag, CipDataType.GetName(value.TypeCode), value.ToObject());
    return;
}

Console.Write("服务端地址（回车保持 {0}）：", options.Endpoint);
var address = Console.ReadLine();
if (!String.IsNullOrWhiteSpace(address)) options.Endpoint = address.Trim();
Console.Write("槽号（回车保持 {0}）：", options.Slot);
var slotText = Console.ReadLine();
if (!String.IsNullOrWhiteSpace(slotText)) options.Slot = Int32.Parse(slotText.Trim());

var driver = new AbDriver();
var pm = new AbParameter
{
    Address = options.Endpoint,
    Slot = options.Slot,
    RouteToSlot = options.RouteToSlot
};
var node = driver.Open(null, pm);
Console.WriteLine("连接成功 => {0}", options.Endpoint);
Console.WriteLine("输入 标签 读取，或 标签=值 写入 DINT。q 退出。");

var line = Console.ReadLine();
while (line != null && line != "q")
{
    line = line.Trim();
    if (line.Length > 0)
    {
        var eq = line.IndexOf('=');
        var tag = eq < 0 ? line : line.Substring(0, eq).Trim();
        var point = new Point { Name = tag, Address = tag, Type = "DINT", Length = 4 };
        if (eq >= 0)
        {
            var number = Int32.Parse(line.Substring(eq + 1).Trim());
            driver.Write(node, point, number);
        }

        var result = driver.Read(node, new IPoint[] { point });
        Console.WriteLine("读取 {0} = {1}", tag, result.GetValue(tag));
    }

    Console.WriteLine("输入 标签 或 标签=值，q 退出：");
    line = Console.ReadLine();
}

driver.Close(node);

static LogixClient CreateClient(Options options)
{
    var p = options.Endpoint.LastIndexOf(':');
    var host = p > 0 ? options.Endpoint.Substring(0, p) : options.Endpoint;
    var port = p > 0 ? Int32.Parse(options.Endpoint.Substring(p + 1)) : LogixClient.DefaultPort;
    return new LogixClient(host, port) { Slot = options.Slot, RouteToSlot = options.RouteToSlot };
}

internal sealed class Options
{
    public String Endpoint { get; set; } = "127.0.0.1:44818";
    public Int32 Slot { get; set; }
    public Boolean RouteToSlot { get; set; } = true;
    public String Tag { get; set; }
    public String WriteValue { get; set; }

    public static Options Parse(String[] args)
    {
        var options = new Options();
        if (args == null) return options;
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg == "--slot" && i + 1 < args.Length) options.Slot = Int32.Parse(args[++i]);
            else if (arg == "--tag" && i + 1 < args.Length) options.Tag = args[++i];
            else if (arg == "--write" && i + 1 < args.Length) options.WriteValue = args[++i];
            else if (arg == "--direct") options.RouteToSlot = false;
            else if (!arg.StartsWith("--") && arg.IndexOf(':') > 0) options.Endpoint = arg;
        }
        return options;
    }
}

internal sealed class Point : IPoint
{
    public String Name { get; set; }
    public String Address { get; set; }
    public String Type { get; set; }
    public Int32 Length { get; set; }
}
