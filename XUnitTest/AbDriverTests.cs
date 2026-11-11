using System.ComponentModel;
using NewLife.AB.Drivers;
using NewLife.AB.Protocols;
using NewLife.IoT.Drivers;
using NewLife.IoT.ThingModels;

namespace XUnitTest;

/// <summary>驱动测试</summary>
[DisplayName("AbDriver 驱动测试")]
public class AbDriverTests
{
    [Fact]
    [DisplayName("GetAddress 保留程序作用域和位")]
    public void GetAddressKeepsTag()
    {
        var driver = new AbDriver();
        Assert.Equal("Program:MainProgram.Tag.0", driver.GetAddress(new TestPoint { Address = " Program:MainProgram.Tag.0 " }));
    }

    [Fact]
    [DisplayName("GetAddress 点位为空时抛出")]
    public void GetAddressNull()
    {
        var driver = new AbDriver();
        Assert.Throws<ArgumentException>(() => driver.GetAddress(null!));
    }

    [Fact]
    [DisplayName("CreateParameter 默认指向 44818")]
    public void CreateParameter()
    {
        var driver = new AbDriver();
        var pm = Assert.IsType<AbParameter>(driver.CreateParameter(null));
        Assert.Equal("127.0.0.1:44818", pm.Address);
        Assert.Equal(0, pm.Slot);
        Assert.True(pm.RouteToSlot);
    }

    [Fact]
    [DisplayName("Open 参数为空时抛出")]
    public void OpenNull()
    {
        var driver = new AbDriver();
        var ex = Assert.Throws<ArgumentException>(() => driver.Open(null!, (IDriverParameter?)null));
        Assert.Contains("address", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [DisplayName("Open 地址为空时抛出")]
    public void OpenEmpty()
    {
        var driver = new AbDriver();
        var ex = Assert.Throws<ArgumentException>(() => driver.Open(null!, new AbParameter { Address = "" }));
        Assert.Contains("address", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [DisplayName("Open 地址没有端口时抛出")]
    public void OpenWithoutPort()
    {
        var driver = new AbDriver();
        var ex = Assert.Throws<ArgumentException>(() => driver.Open(null!, new AbParameter { Address = "192168100" }));
        Assert.Contains("address", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [DisplayName("未打开直接关闭不抛出")]
    public void CloseWithoutOpen()
    {
        var driver = new AbDriver();
        var ex = Record.Exception(() => driver.Close(null!));
        Assert.Null(ex);
    }

    [Fact]
    [DisplayName("空点位读取返回成功")]
    public void ReadEmpty()
    {
        var driver = new AbDriver();
        var result = driver.Read(null!, Array.Empty<IPoint>());
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Points);
        result = driver.Read(null!, null!);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    [DisplayName("不支持的写入类型直接拒绝")]
    public void WriteUnsupported()
    {
        var driver = new AbDriver();
        var point = new TestPoint { Address = "Counter" };
        Assert.Throws<ArgumentException>(() => driver.Write(null!, point, 1.5m));
    }

    [Fact]
    [DisplayName("驱动经模拟器完成打开、读写和关闭")]
    public void RoundTrip()
    {
        using var server = new LogixServer();
        server.Start();
        var driver = new AbDriver();
        var pm = new AbParameter { Address = "127.0.0.1:" + server.Port, Slot = 0 };
        var node = driver.Open(null!, pm);
        var second = driver.Open(null!, pm);

        var point = new TestPoint { Name = "Counter", Address = "Counter", Type = "DINT", Length = 4 };
        driver.Write(node, point, 42);
        var result = driver.Read(node, new IPoint[] { point });
        Assert.Equal(42, result.GetValue("Counter"));

        driver.Close(node);
        driver.Close(second);
        Assert.Equal(42, BitConverter.ToInt32(server.GetTag("Counter")!, 0));
    }

    [Fact]
    [DisplayName("驱动读到不存在的标签时失败")]
    public void BadPath()
    {
        using var server = new LogixServer { AutoCreateTags = false };
        server.Start();
        var driver = new AbDriver();
        var node = driver.Open(null!, new AbParameter { Address = "127.0.0.1:" + server.Port });
        var point = new TestPoint { Name = "Missing", Address = "Missing", Type = "DINT" };
        Assert.Throws<CipException>(() => driver.Read(node, new IPoint[] { point }));
        driver.Close(node);
    }
}

/// <summary>测试点位</summary>
public class TestPoint : IPoint
{
    /// <summary>名称</summary>
    public String? Name { get; set; }

    /// <summary>地址</summary>
    public String? Address { get; set; }

    /// <summary>类型</summary>
    public String? Type { get; set; }

    /// <summary>长度</summary>
    public Int32 Length { get; set; }
}
