using System.ComponentModel;
using System.Text;
using NewLife.AB.Protocols;

namespace XUnitTest;

/// <summary>CIP 报文测试</summary>
[DisplayName("CIP 报文测试")]
public class CipMessageTests
{
    [Fact]
    [DisplayName("读标签请求与公开示例 TotalCount 一致")]
    public void ReadTagTotalCount()
    {
        var path = CipPath.Symbol("TotalCount");
        var message = CipMessage.ReadTag(path, 1);
        var bytes = message.ToRequestBytes();
        var expected = new Byte[]
        {
            0x4C, 0x06,
            0x91, 0x0A, 0x54, 0x6F, 0x74, 0x61, 0x6C, 0x43, 0x6F, 0x75, 0x6E, 0x74,
            0x01, 0x00
        };
        Assert.Equal(expected, bytes);
    }

    [Fact]
    [DisplayName("写标签请求与公开示例 CartonSize=14 一致")]
    public void WriteTagCartonSize()
    {
        var path = CipPath.Symbol("CartonSize");
        var message = CipMessage.WriteTag(path, CipDataType.Dint, 0, 1, new Byte[] { 0x0E, 0x00, 0x00, 0x00 });
        var bytes = message.ToRequestBytes();
        Assert.Equal(0x4D, bytes[0]);
        Assert.Equal(0x06, bytes[1]);
        Assert.Equal(Encoding.ASCII.GetBytes("CartonSize"), bytes.Skip(4).Take(10).ToArray());
        Assert.Equal(new Byte[] { 0xC4, 0x00, 0x01, 0x00, 0x0E, 0x00, 0x00, 0x00 }, bytes.Skip(14).ToArray());
    }

    [Fact]
    [DisplayName("应答解析出 DINT 534 和成功状态")]
    public void ParseDint534()
    {
        var bytes = new Byte[] { 0xCC, 0x00, 0x00, 0x00, 0xC4, 0x00, 0x16, 0x02, 0x00, 0x00 };
        var message = CipMessage.ParseResponse(bytes);
        Assert.True(CipService.IsReply(message.Service, CipService.ReadTag));
        Assert.True(message.IsSuccess);
        Assert.Equal(534, LogixValue.Decode(message.Data, 1).GetInt32());
    }

    [Fact]
    [DisplayName("失败应答带扩展状态")]
    public void ExtendedStatus()
    {
        var bytes = new Byte[] { 0xCC, 0x00, 0xFF, 0x01, 0x05, 0x21 };
        var message = CipMessage.ParseResponse(bytes);
        Assert.Equal(0xFF, message.GeneralStatus);
        Assert.Equal(new UInt16[] { 0x2105 }, message.ExtendedStatus);
        Assert.Contains("超出", CipStatus.GetMessage(message.GeneralStatus, message.ExtendedStatus));
    }

    [Fact]
    [DisplayName("读改写掩码与公开示例 bit2 置位 bit5 清零一致")]
    public void BitMasks()
    {
        Byte[] orMask;
        Byte[] andMask;
        CipMessage.BuildBitMasks(4, 2, true, out orMask, out andMask);
        Assert.Equal(new Byte[] { 0x04, 0x00, 0x00, 0x00 }, orMask);
        Assert.Equal(new Byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, andMask);

        CipMessage.BuildBitMasks(4, 5, false, out orMask, out andMask);
        Assert.Equal(new Byte[] { 0x00, 0x00, 0x00, 0x00 }, orMask);
        Assert.Equal(new Byte[] { 0xDF, 0xFF, 0xFF, 0xFF }, andMask);
    }

    [Fact]
    [DisplayName("多服务打包的偏移从请求数据起点计算")]
    public void MultipleOffsets()
    {
        var first = CipMessage.ReadTag(CipPath.Symbol("parts"), 1).ToRequestBytes();
        var second = CipMessage.ReadTag(CipPath.Symbol("ControlWord"), 1).ToRequestBytes();
        var message = CipMessage.Multiple(new[] { first, second });
        var data = message.Data;
        Assert.Equal(2, BitConverter.ToUInt16(data, 0));
        Assert.Equal(6, BitConverter.ToUInt16(data, 2));
        Assert.Equal(6 + first.Length, BitConverter.ToUInt16(data, 4));
        Assert.Equal(0x0A, message.ToRequestBytes()[0]);
    }

    [Fact]
    [DisplayName("Unconnected Send 包住读请求并附带槽号")]
    public void UnconnectedSend()
    {
        var read = CipMessage.ReadTag(CipPath.Symbol("A"), 1).ToRequestBytes();
        var message = CipMessage.BuildUnconnectedSend(read, CipPath.Port(1, 2), 0x07, 0x10);
        var bytes = message.ToRequestBytes();
        Assert.Equal(0x52, bytes[0]);
        Assert.Equal(new Byte[] { 0x20, 0x06, 0x24, 0x01 }, bytes.Skip(2).Take(4).ToArray());
        Assert.Equal(0x07, message.Data[0]);
        Assert.Equal(0x10, message.Data[1]);

        var embedded = CipMessage.ParseUnconnectedSend(message.Data, out var route);
        Assert.Equal(read, embedded);
        Assert.Equal(new Byte[] { 0x01, 0x02 }, route);
    }

    [Fact]
    [DisplayName("奇数长度的嵌入请求会补齐")]
    public void UnconnectedSendPadsOddPayload()
    {
        var write = CipMessage.WriteTag(CipPath.Symbol("A"), CipDataType.Sint, 0, 1, new Byte[] { 0x7F }).ToRequestBytes();
        Assert.Equal(1, write.Length % 2);
        var message = CipMessage.BuildUnconnectedSend(write, CipPath.Port(1, 0), 0x07, 0x10);
        var embedded = CipMessage.ParseUnconnectedSend(message.Data, out var route);
        Assert.Equal(write, embedded);
        Assert.Equal(new Byte[] { 0x01, 0x00 }, route);
    }

    [Fact]
    [DisplayName("Forward Open 使用服务 0x54 和传输触发 0xA3")]
    public void ForwardOpenShape()
    {
        var path = CipPath.Concat(CipPath.Port(1, 0), CipPath.ClassInstance(0x02, 1));
        var message = CipMessage.BuildForwardOpen(1, 2, 3, 0x1234, 4, 100000, LogixClient.ExplicitConnectionParameters, LogixClient.ExplicitTransportTrigger, path);
        Assert.Equal(0x54, message.Service);
        Assert.True(CipMessage.IsConnectionManager(message.Path));
        Assert.Contains(LogixClient.ExplicitTransportTrigger, message.Data);
        Assert.Equal(0x43F4, LogixClient.ExplicitConnectionParameters);
    }

    [Fact]
    [DisplayName("数据类型码使用公开值")]
    public void DataTypeCodes()
    {
        Assert.Equal(0x00C1, CipDataType.Bool);
        Assert.Equal(0x00C2, CipDataType.Sint);
        Assert.Equal(0x00C3, CipDataType.Int);
        Assert.Equal(0x00C4, CipDataType.Dint);
        Assert.Equal(0x00C5, CipDataType.Lint);
        Assert.Equal(0x00C6, CipDataType.Usint);
        Assert.Equal(0x00C7, CipDataType.Uint);
        Assert.Equal(0x00C8, CipDataType.Udint);
        Assert.Equal(0x00C9, CipDataType.Ulint);
        Assert.Equal(0x00CA, CipDataType.Real);
        Assert.Equal(0x00CB, CipDataType.Lreal);
        Assert.Equal(0x02A0, CipDataType.String);
    }
}
