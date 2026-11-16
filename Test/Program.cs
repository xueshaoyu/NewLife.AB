using NewLife.AB.Drivers;
using NewLife.AB.Protocols;
using NewLife.IoT.Drivers;
using NewLife.IoT.ThingModels;

// 指向真实 PLC：dotnet run --project Test -- 192.168.1.10:44818 --slot 0 --tag Program:MainProgram.Counter
// 不指定类型时 --count / --length 是元素个数：dotnet run --project Test -- 192.168.1.32:44818 --direct --tag GET_1 --count 10
// 指定类型时 --length 是字节数：dotnet run --project Test -- 192.168.1.32:44818 --direct --tag GET_1 --type DINT --length 40
// 指定类型且只给 --count 时，按元素宽度换成字节：--type DINT --count 10 等于长度 40
// 不带参数时进入交互模式，默认 127.0.0.1:44818、槽号 0。
var options = Options.Parse(args);
Console.WriteLine("NewLife.AB Logix 控制台");
Console.WriteLine("主机 {0}  槽号 {1}  路由 {2}", options.Endpoint, options.Slot, options.RouteToSlot ? "1," + options.Slot : "直接");

if (options.Tag != null)
{
    var tagDriver = new AbDriver();
    var tagParameter = new AbParameter
    {
        Address = options.Endpoint,
        Slot = options.Slot,
        RouteToSlot = options.RouteToSlot
    };
    var tagNode = tagDriver.Open(null, tagParameter);
    var tagPoint = BuildPoint(options.Tag, options.Type, options.Length, options.Count);
    if (options.WriteValue != null)
    {
        var number = Int32.Parse(options.WriteValue);
        tagDriver.Write(tagNode, tagPoint, number);
        Console.WriteLine("已写入 {0} = {1}", options.Tag, options.WriteValue);
    }

    var tagResult = tagDriver.Read(tagNode, new IPoint[] { tagPoint });
    Console.WriteLine("点位 {0} 类型 {1} 长度 {2}", tagPoint.Address, tagPoint.Type ?? "未指定", tagPoint.Length);
    Console.WriteLine("读取 {0} = {1}", options.Tag, FormatValue(tagResult.GetValue(options.Tag)));
    tagDriver.Close(tagNode);
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
Console.WriteLine("输入 标签、标签 个数、标签 类型 个数 读取，或 标签=值 写入 DINT。q 退出。");

var line = Console.ReadLine();
while (line != null && line != "q")
{
    line = line.Trim();
    if (line.Length > 0)
    {
        var eq = line.IndexOf('=');
        var body = eq < 0 ? line : line.Substring(0, eq).Trim();
        ParseBody(body, out var tag, out var type, out var count);
        var point = BuildPoint(tag, type, 0, count);
        if (eq >= 0)
        {
            var number = Int32.Parse(line.Substring(eq + 1).Trim());
            driver.Write(node, point, number);
        }

        var result = driver.Read(node, new IPoint[] { point });
        Console.WriteLine("点位 {0} 类型 {1} 长度 {2}", point.Address, point.Type ?? "未指定", point.Length);
        Console.WriteLine("读取 {0} = {1}", tag, FormatValue(result.GetValue(tag)));
    }

    Console.WriteLine("输入 标签、标签 个数、标签 类型 个数，或 标签=值，q 退出：");
    line = Console.ReadLine();
}

driver.Close(node);

static Point BuildPoint(String tag, String type, Int32 length, UInt16 count)
{
    var point = new Point { Name = tag, Address = tag };
    if (!String.IsNullOrWhiteSpace(type)) point.Type = type.Trim();
    if (length > 0)
    {
        point.Length = length;
        return point;
    }

    if (point.Type != null && CipDataType.TryParseName(point.Type, out var code))
    {
        var size = CipDataType.GetElementSize(code);
        if (size > 0)
        {
            point.Length = count * size;
            return point;
        }
    }

    point.Length = count;
    return point;
}

static void ParseBody(String body, out String tag, out String type, out UInt16 count)
{
    tag = body.Trim();
    type = null;
    count = 1;
    var parts = tag.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length == 0) return;

    tag = parts[0];
    for (var i = 1; i < parts.Length; i++)
    {
        if (UInt16.TryParse(parts[i], out var number))
        {
            if (number == 0) throw new ArgumentOutOfRangeException(nameof(body), "元素个数必须大于 0");
            count = number;
            continue;
        }

        if (CipDataType.TryParseName(parts[i], out _))
        {
            type = parts[i];
            continue;
        }

        tag = body.Trim();
        type = null;
        count = 1;
        return;
    }
}

static String FormatValue(Object value)
{
    if (value is not Array array) return value == null ? "" : value.ToString();
    var parts = new String[array.Length];
    for (var i = 0; i < array.Length; i++)
    {
        var item = array.GetValue(i);
        parts[i] = item == null ? "" : item.ToString();
    }
    return "[" + String.Join(", ", parts) + "]";
}

internal sealed class Options
{
    public String Endpoint { get; set; } = "192.168.1.32:44818";
    public Int32 Slot { get; set; }
    public Boolean RouteToSlot { get; set; }
    public String Tag { get; set; }
    public String Type { get; set; }
    public Int32 Length { get; set; }
    public UInt16 Count { get; set; } = 1;
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
            else if (arg == "--type" && i + 1 < args.Length) options.Type = args[++i];
            else if (arg == "--length" && i + 1 < args.Length) options.Length = ParseLengthArg(args[++i]);
            else if (arg == "--count" && i + 1 < args.Length) options.Count = ParseCountArg(args[++i]);
            else if (arg == "--write" && i + 1 < args.Length) options.WriteValue = args[++i];
            else if (arg == "--direct") options.RouteToSlot = false;
            else if (!arg.StartsWith("--") && arg.IndexOf(':') > 0) options.Endpoint = arg;
        }
        return options;
    }

    private static Int32 ParseLengthArg(String text)
    {
        var length = Int32.Parse(text);
        if (length <= 0) throw new ArgumentOutOfRangeException(nameof(text), "长度必须大于 0");
        return length;
    }

    private static UInt16 ParseCountArg(String text)
    {
        var count = UInt16.Parse(text);
        if (count == 0) throw new ArgumentOutOfRangeException(nameof(text), "元素个数必须大于 0");
        return count;
    }
}

internal sealed class Point : IPoint
{
    public String Name { get; set; }
    public String Address { get; set; }
    public String Type { get; set; }
    public Int32 Length { get; set; }
}
