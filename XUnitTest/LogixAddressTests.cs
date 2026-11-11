using System.ComponentModel;
using NewLife.AB.Protocols;

namespace XUnitTest;

/// <summary>标签地址测试</summary>
[DisplayName("标签地址测试")]
public class LogixAddressTests
{
    [Fact]
    [DisplayName("普通标签")]
    public void Simple()
    {
        var address = LogixAddress.Parse("Counter");
        Assert.Equal("Counter", address.Key);
        Assert.Null(address.Bit);
        Assert.Empty(address.AllElements);
        Assert.Equal("Counter", address.ToString());
    }

    [Fact]
    [DisplayName("类型提示、数组和位")]
    public void TypeArrayBit()
    {
        var address = LogixAddress.Parse("DINT:Positions[3].2");
        Assert.Equal(CipDataType.Dint, address.TypeCode);
        Assert.Equal("Positions", address.Key);
        Assert.Equal(new UInt32[] { 3 }, address.AllElements);
        Assert.Equal(2, address.Bit);
        Assert.Equal("DINT:Positions[3].2", address.ToString());
    }

    [Fact]
    [DisplayName("程序作用域标签分成两段符号")]
    public void ProgramScope()
    {
        var address = LogixAddress.Parse("Program:MainProgram.Tag");
        Assert.Equal(2, address.Symbols.Length);
        Assert.Equal("Program:MainProgram", address.Symbols[0].Name);
        Assert.Equal("Tag", address.Symbols[1].Name);
        Assert.Equal("Program:MainProgram.Tag", address.Key);

        var path = address.ToPath();
        Assert.Equal(0x91, path[0]);
        Assert.Equal((Byte)"Program:MainProgram".Length, path[1]);
    }

    [Fact]
    [DisplayName("类型提示不会把 Program 当成类型")]
    public void TypeBeforeProgram()
    {
        var address = LogixAddress.Parse("INT:Program:MainProgram.Local");
        Assert.Equal(CipDataType.Int, address.TypeCode);
        Assert.Equal("Program:MainProgram.Local", address.Key);
    }

    [Fact]
    [DisplayName("成员路径保留点号")]
    public void Member()
    {
        var address = LogixAddress.Parse("Motor.Speed");
        Assert.Equal("Motor.Speed", address.Key);
        Assert.Null(address.Bit);
    }

    [Fact]
    [DisplayName("多维下标编码为连续元素段")]
    public void TwoIndexes()
    {
        var path = LogixAddress.Parse("Grid[1,2]").ToPath();
        Assert.Contains((Byte)0x28, path);
        Assert.Equal(1, path[path.Length - 3]);
        Assert.Equal(2, path[path.Length - 1]);
    }

    [Fact]
    [DisplayName("空地址抛出异常")]
    public void Empty()
    {
        Assert.Throws<ArgumentNullException>(() => LogixAddress.Parse(null!));
        Assert.Throws<ArgumentException>(() => LogixAddress.Parse("  "));
    }

    [Fact]
    [DisplayName("下标未闭合抛出异常")]
    public void BadBracket()
    {
        Assert.Throws<ArgumentException>(() => LogixAddress.Parse("Tag[1"));
    }

    [Fact]
    [DisplayName("路由字符串 1,0 和带 IP 的路径")]
    public void Route()
    {
        Assert.Equal(new Byte[] { 0x01, 0x00 }, CipPath.ParseRoute("1,0"));
        var routed = CipPath.ParseRoute("2,192.168.1.10,1,3");
        Assert.Equal(0x12, routed[0]);
        Assert.Equal((Byte)"192.168.1.10".Length, routed[1]);
        Assert.Equal(0x01, routed[routed.Length - 2]);
        Assert.Equal(0x03, routed[routed.Length - 1]);
    }

    [Fact]
    [DisplayName("PCCC 扩展点明确拒绝")]
    public void PcccNotSupported()
    {
        var ex = Assert.Throws<NotSupportedException>(() => PcccLegacy.ThrowNotSupported());
        Assert.Contains("PCCC", ex.Message);
        Assert.Equal(0x4B, PcccLegacy.ExecutePccc);
    }
}
