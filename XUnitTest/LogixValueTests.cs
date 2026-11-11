using System.ComponentModel;
using NewLife.AB.Protocols;

namespace XUnitTest;

/// <summary>类型编解码测试</summary>
[DisplayName("类型编解码测试")]
public class LogixValueTests
{
    [Theory]
    [DisplayName("原子类型往返")]
    [InlineData("SINT")]
    [InlineData("INT")]
    [InlineData("DINT")]
    [InlineData("LINT")]
    [InlineData("USINT")]
    [InlineData("UINT")]
    [InlineData("UDINT")]
    [InlineData("ULINT")]
    [InlineData("REAL")]
    [InlineData("LREAL")]
    [InlineData("BOOL")]
    public void AtomicRoundTrip(String name)
    {
        Assert.True(CipDataType.TryParseName(name, out var type));
        Byte[] raw;
        switch (type)
        {
            case CipDataType.Bool: raw = LogixValue.EncodeBoolean(true); break;
            case CipDataType.Sint: raw = LogixValue.EncodeSByte(-5); break;
            case CipDataType.Int: raw = LogixValue.EncodeInt16(-300); break;
            case CipDataType.Dint: raw = LogixValue.EncodeInt32(-123456); break;
            case CipDataType.Lint: raw = LogixValue.EncodeInt64(-99); break;
            case CipDataType.Usint: raw = LogixValue.EncodeByte(200); break;
            case CipDataType.Uint: raw = LogixValue.EncodeUInt16(40000); break;
            case CipDataType.Udint: raw = LogixValue.EncodeUInt32(3000000000); break;
            case CipDataType.Ulint: raw = LogixValue.EncodeUInt64(99); break;
            case CipDataType.Real: raw = LogixValue.EncodeSingle(3.5f); break;
            default: raw = LogixValue.EncodeDouble(2.25); break;
        }

        var header = new Byte[2 + raw.Length];
        header[0] = (Byte)type;
        header[1] = (Byte)(type >> 8);
        Buffer.BlockCopy(raw, 0, header, 2, raw.Length);
        var value = LogixValue.Decode(header, 1);
        Assert.Equal(type, value.TypeCode);
        Assert.Equal(CipDataType.GetElementSize(type), raw.Length);
    }

    [Fact]
    [DisplayName("DINT 小端 534")]
    public void Dint534()
    {
        var value = LogixValue.Decode(new Byte[] { 0xC4, 0x00, 0x16, 0x02, 0x00, 0x00 }, 1);
        Assert.Equal(534, value.GetInt32());
    }

    [Fact]
    [DisplayName("BOOL 非零为真，含 0xFF")]
    public void Bool()
    {
        Assert.False(LogixValue.Decode(new Byte[] { 0xC1, 0x00, 0x00 }, 1).GetBoolean());
        Assert.True(LogixValue.Decode(new Byte[] { 0xC1, 0x00, 0x01 }, 1).GetBoolean());
        Assert.True(LogixValue.Decode(new Byte[] { 0xC1, 0x00, 0xFF }, 1).GetBoolean());
    }

    [Fact]
    [DisplayName("STRING 按 LEN 截取，保留单字节字符")]
    public void StringValue()
    {
        var payload = LogixValue.EncodeString("AB", 82);
        Assert.Equal(86, payload.Length);
        var body = new Byte[4 + payload.Length];
        body[0] = 0xA0;
        body[1] = 0x02;
        body[2] = 0xCE;
        body[3] = 0x00;
        Buffer.BlockCopy(payload, 0, body, 4, payload.Length);
        var value = LogixValue.Decode(body, 1);
        Assert.Equal(LogixServer.DefaultStringHandle, value.StructureHandle);
        Assert.Equal("AB", value.GetString());
    }

    [Fact]
    [DisplayName("DINT 数组按元素展开")]
    public void Array()
    {
        var raw = new Byte[] { 0xC4, 0x00, 0x01, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00 };
        var value = LogixValue.Decode(raw, 2);
        var items = Assert.IsType<Int32[]>(value.ToArray());
        Assert.Equal(new[] { 1, 2 }, items);
    }

    [Fact]
    [DisplayName("类型不符时拒绝按另一种整数解读")]
    public void TypeMismatch()
    {
        var value = LogixValue.Decode(new Byte[] { 0xC4, 0x00, 0x01, 0x00, 0x00, 0x00 }, 1);
        Assert.Throws<InvalidOperationException>(() => value.GetInt16());
    }

    [Fact]
    [DisplayName("字符串超过容量时拒绝")]
    public void StringTooLong()
    {
        Assert.Throws<ArgumentException>(() => LogixValue.EncodeString(new String('A', 83), 82));
    }
}
