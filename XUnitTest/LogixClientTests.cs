using System.ComponentModel;
using NewLife.AB.Protocols;

namespace XUnitTest;

/// <summary>客户端生命周期测试</summary>
[DisplayName("LogixClient 生命周期测试")]
public class LogixClientTests
{
    [Fact]
    [DisplayName("默认端口、槽号和自动重连")]
    public void Defaults()
    {
        var client = new LogixClient();
        Assert.Equal(44818, client.Port);
        Assert.Equal(0, client.Slot);
        Assert.True(client.RouteToSlot);
        Assert.False(client.ConnectedMessaging);
        Assert.True(client.AutoReconnect);
        Assert.Equal(3, client.MaxReconnectRetries);
        Assert.Equal(82, client.StringCapacity);
        Assert.False(client.IsConnected);
    }

    [Fact]
    [DisplayName("构造函数写入主机和端口")]
    public void Constructor()
    {
        var client = new LogixClient("10.0.0.8", 44818);
        Assert.Equal("10.0.0.8", client.Host);
        Assert.Equal(44818, client.Port);
    }

    [Fact]
    [DisplayName("连接拒绝时抛出失败")]
    public void ConnectRefused()
    {
        using var client = new LogixClient("127.0.0.1", 1)
        {
            ConnectTimeout = 500,
            AutoReconnect = false
        };
        Assert.ThrowsAny<Exception>(() => client.Connect());
        Assert.False(client.IsConnected);
    }

    [Fact]
    [DisplayName("未连接且关闭自动重连时读取失败")]
    public void ReadWithoutConnection()
    {
        using var client = new LogixClient("127.0.0.1", 1) { AutoReconnect = false };
        Assert.ThrowsAny<Exception>(() => client.ReadInt32("A"));
    }

    [Fact]
    [DisplayName("关闭和释放可重复调用")]
    public void CloseTwice()
    {
        var client = new LogixClient();
        client.Close();
        client.Dispose();
        Assert.False(client.IsConnected);
    }
}
