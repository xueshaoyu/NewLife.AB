using System.ComponentModel;
using NewLife.AB.Protocols;

namespace XUnitTest;

/// <summary>模拟器往返测试</summary>
[DisplayName("LogixServer 往返测试")]
public class LogixServerTests
{
    [Fact]
    [DisplayName("会话注册后句柄非零，DINT 读写往返")]
    public void SessionAndDint()
    {
        using var server = new LogixServer();
        server.Start();
        using var client = Client(server);
        client.Connect();
        Assert.NotEqual(0u, client.SessionHandle);
        client.WriteInt32("Counter", 534);
        Assert.Equal(534, client.ReadInt32("Counter"));
        client.Close();
        Assert.False(client.IsConnected);
    }

    [Fact]
    [DisplayName("原子类型与字符串往返")]
    public void TypedValues()
    {
        using var server = new LogixServer();
        server.Start();
        using var client = Client(server);
        client.Connect();

        client.WriteBoolean("Running", true);
        client.WriteSByte("Tiny", -3);
        client.WriteInt16("Word", -300);
        client.WriteInt64("Big", -99);
        client.WriteByte("Raw", 200);
        client.WriteUInt16("UWord", 40000);
        client.WriteUInt32("UDword", 3000000000);
        client.WriteUInt64("UBig", 99);
        client.WriteSingle("Temp", 3.5f);
        client.WriteDouble("Wide", 2.25);
        client.StringStructureHandle = LogixServer.DefaultStringHandle;
        client.WriteString("Message", "AB");

        Assert.True(client.ReadBoolean("Running"));
        Assert.Equal((SByte)(-3), client.ReadSByte("Tiny"));
        Assert.Equal((Int16)(-300), client.ReadInt16("Word"));
        Assert.Equal(-99, client.ReadInt64("Big"));
        Assert.Equal(200, client.ReadByte("Raw"));
        Assert.Equal(40000, client.ReadUInt16("UWord"));
        Assert.Equal(3000000000, client.ReadUInt32("UDword"));
        Assert.Equal(99ul, client.ReadUInt64("UBig"));
        Assert.Equal(3.5f, client.ReadSingle("Temp"));
        Assert.Equal(2.25, client.ReadDouble("Wide"));
        Assert.Equal("AB", client.ReadString("Message"));
    }

    [Fact]
    [DisplayName("一维数组、程序作用域和位读写")]
    public void ArrayProgramAndBit()
    {
        using var server = new LogixServer();
        server.Start();
        using var client = Client(server);
        client.Connect();

        client.WriteInt32("Positions", new[] { 10, 20, 30, 40 });
        Assert.Equal(new[] { 20, 30 }, client.ReadInt32("Positions[1]", 2));

        client.WriteInt32("Program:MainProgram.Tag", 7);
        Assert.Equal(7, client.ReadInt32("Program:MainProgram.Tag"));

        client.WriteInt32("ControlWord", 0);
        client.WriteBoolean("ControlWord.2", true);
        client.WriteBoolean("ControlWord.5", false);
        Assert.True(client.ReadBoolean("ControlWord.2"));
        Assert.False(client.ReadBoolean("ControlWord.5"));
        Assert.Equal(4, client.ReadInt32("ControlWord"));
    }

    [Fact]
    [DisplayName("多标签读取")]
    public void Multiple()
    {
        using var server = new LogixServer();
        server.Start();
        using var client = Client(server);
        client.Connect();
        client.WriteInt32("A", 1);
        client.WriteInt32("B", 2);
        var values = client.ReadTags(new[] { "A", "B" });
        Assert.Equal(1, values[0].GetInt32());
        Assert.Equal(2, values[1].GetInt32());
    }

    [Fact]
    [DisplayName("身份对象和 ListIdentity")]
    public void Identity()
    {
        using var server = new LogixServer { ProductName = "LogixServer" };
        server.Start();
        using var client = Client(server);
        client.Connect();
        var attributes = client.ReadIdentity();
        Assert.Equal("LogixServer", attributes.ProductName);
        Assert.Equal(0x000E, attributes.DeviceType);
        var listed = client.ListIdentity();
        Assert.Equal("LogixServer", listed.ProductName);
        Assert.Contains("127.0.0.1", listed.Address);
    }

    [Fact]
    [DisplayName("不存在的标签返回路径目标未知")]
    public void BadPath()
    {
        using var server = new LogixServer { AutoCreateTags = false };
        server.Start();
        using var client = Client(server);
        client.Connect();
        var ex = Assert.Throws<CipException>(() => client.ReadInt32("Missing"));
        Assert.Equal(CipStatus.PathDestinationUnknown, ex.GeneralStatus);
    }

    [Fact]
    [DisplayName("类型不符返回扩展状态 0x2107")]
    public void TypeMismatch()
    {
        using var server = new LogixServer();
        server.Start();
        server.SetTag("Counter", CipDataType.Dint, new Byte[] { 1, 0, 0, 0 });
        using var client = Client(server);
        client.Connect();
        var ex = Assert.Throws<CipException>(() => client.WriteSingle("Counter", 1.5f));
        Assert.Equal(0xFF, ex.GeneralStatus);
        Assert.Equal(CipStatus.ExtTypeMismatch, ex.ExtendedStatus[0]);
    }

    [Fact]
    [DisplayName("槽号不符时路由失败")]
    public void BadSlot()
    {
        using var server = new LogixServer { RequiredSlot = 0 };
        server.Start();
        using var client = Client(server);
        client.Slot = 3;
        client.Connect();
        var ex = Assert.Throws<CipException>(() => client.ReadInt32("A"));
        Assert.Equal(CipStatus.PathSegmentError, ex.GeneralStatus);
    }

    [Fact]
    [DisplayName("强制 CIP 失败状态会抛出")]
    public void ForcedStatus()
    {
        using var server = new LogixServer { ForceGeneralStatus = CipStatus.ServiceNotSupported };
        server.Start();
        using var client = Client(server);
        client.Connect();
        var ex = Assert.Throws<CipException>(() => client.ReadInt32("A"));
        Assert.Equal(CipStatus.ServiceNotSupported, ex.GeneralStatus);
    }

    [Fact]
    [DisplayName("会话失效后自动重新注册")]
    public void SessionTimeout()
    {
        using var server = new LogixServer();
        server.Start();
        using var client = Client(server);
        client.RetryDelayMilliseconds = 0;
        client.Connect();
        client.WriteInt32("Counter", 5);
        server.InvalidateSessionOnce = true;
        Assert.Equal(5, client.ReadInt32("Counter"));
    }

    [Fact]
    [DisplayName("连接被断开后自动重连")]
    public void Disconnect()
    {
        using var server = new LogixServer();
        server.Start();
        using var client = Client(server);
        client.RetryDelayMilliseconds = 0;
        client.Connect();
        client.WriteInt32("Counter", 8);
        server.DropClients();
        Assert.Equal(8, client.ReadInt32("Counter"));
    }

    [Fact]
    [DisplayName("分段读取拼回完整数组")]
    public void FragmentedRead()
    {
        using var server = new LogixServer { MaxResponseBytes = 8 };
        server.Start();
        using var client = Client(server);
        client.Connect();
        client.WriteInt32("Block", new[] { 1, 2, 3, 4 });
        Assert.Equal(new[] { 1, 2, 3, 4 }, client.ReadInt32("Block", 4));
    }

    [Fact]
    [DisplayName("分段写入拼回完整数组")]
    public void FragmentedWrite()
    {
        using var server = new LogixServer();
        server.Start();
        using var client = Client(server);
        client.WriteFragmentSize = 4;
        client.Connect();
        client.WriteInt32("Block", new[] { 5, 6, 7, 8 });
        Assert.Equal(new[] { 5, 6, 7, 8 }, client.ReadInt32("Block", 4));
    }

    [Fact]
    [DisplayName("Forward Open 之后仍能读写")]
    public void Connected()
    {
        using var server = new LogixServer();
        server.Start();
        using var client = Client(server);
        client.ConnectedMessaging = true;
        client.Connect();
        client.WriteInt32("Counter", 11);
        Assert.Equal(11, client.ReadInt32("Counter"));
    }

    [Fact]
    [DisplayName("异步读写")]
    public async Task AsyncRoundTrip()
    {
        using var server = new LogixServer();
        server.Start();
        using var client = Client(server);
        await client.ConnectAsync();
        await client.WriteInt32Async("Counter", 15);
        Assert.Equal(15, await client.ReadInt32Async("Counter"));
        await client.WriteBooleanAsync("Flag", true);
        Assert.True(await client.ReadBooleanAsync("Flag"));
    }

    [Fact]
    [DisplayName("直接 UCMM 不经背板路由")]
    public void DirectRoute()
    {
        using var server = new LogixServer { RequiredSlot = 0 };
        server.Start();
        using var client = Client(server);
        client.RouteToSlot = false;
        client.Connect();
        client.WriteInt16("N", 9);
        Assert.Equal(9, client.ReadInt16("N"));
    }

    private static LogixClient Client(LogixServer server) => new LogixClient("127.0.0.1", server.Port)
    {
        ConnectTimeout = 2000,
        ReceiveTimeout = 2000,
        RetryDelayMilliseconds = 0
    };
}
