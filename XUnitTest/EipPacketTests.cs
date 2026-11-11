using System.ComponentModel;
using NewLife.AB.Protocols;

namespace XUnitTest;

/// <summary>封装头测试</summary>
[DisplayName("EtherNet/IP 封装头测试")]
public class EipPacketTests
{
    [Fact]
    [DisplayName("命令码使用公开的封装命令值")]
    public void CommandCodes()
    {
        Assert.Equal(0x0065, EipCommand.RegisterSession);
        Assert.Equal(0x0066, EipCommand.UnRegisterSession);
        Assert.Equal(0x006F, EipCommand.SendRRData);
        Assert.Equal(0x0070, EipCommand.SendUnitData);
        Assert.Equal(0x0063, EipCommand.ListIdentity);
        Assert.Equal(0x0004, EipCommand.ListServices);
    }

    [Fact]
    [DisplayName("封装头往返保留命令、会话、状态和上下文")]
    public void RoundTrip()
    {
        var packet = new EipPacket
        {
            Command = EipCommand.RegisterSession,
            SessionHandle = 0x11223344,
            Status = EipStatus.Success,
            SenderContext = 0x0102030405060708,
            Options = 0,
            Data = new Byte[] { 0x01, 0x00, 0x00, 0x00 }
        };

        var bytes = packet.ToBytes();
        Assert.Equal(28, bytes.Length);
        Assert.Equal(4, EipPacket.ReadDataLength(bytes));

        var parsed = EipPacket.Parse(bytes);
        Assert.Equal(packet.Command, parsed.Command);
        Assert.Equal(packet.SessionHandle, parsed.SessionHandle);
        Assert.Equal(packet.Status, parsed.Status);
        Assert.Equal(packet.SenderContext, parsed.SenderContext);
        Assert.Equal(packet.Data, parsed.Data);
    }

    [Fact]
    [DisplayName("头不足时解析失败")]
    public void ShortHeader()
    {
        Assert.Throws<InvalidOperationException>(() => EipPacket.Parse(new Byte[] { 0x65, 0x00 }));
    }

    [Fact]
    [DisplayName("SendRRData 含空地址项和未连接数据项")]
    public void SendRRDataFraming()
    {
        var cip = new Byte[] { 0x4C, 0x02, 0x91, 0x01, 0x41, 0x00, 0x01, 0x00 };
        var data = EipCommonPacket.BuildSendRRData(cip, 10);
        Assert.Equal(0, BitConverter.ToInt32(data, 0));
        Assert.Equal(10, BitConverter.ToUInt16(data, 4));
        Assert.Equal(2, BitConverter.ToUInt16(data, 6));
        Assert.Equal(EipCommonPacket.NullAddress, BitConverter.ToUInt16(data, 8));
        Assert.Equal(EipCommonPacket.UnconnectedData, BitConverter.ToUInt16(data, 12));

        var extracted = EipCommonPacket.ExtractCip(data, out var sequence);
        Assert.Equal(0, sequence);
        Assert.Equal(cip, extracted);
    }

    [Fact]
    [DisplayName("SendUnitData 带连接号和序号")]
    public void SendUnitDataFraming()
    {
        var cip = new Byte[] { 0x01, 0x02, 0x03 };
        var data = EipCommonPacket.BuildSendUnitData(0xAABBCCDD, 7, cip, 10);
        var extracted = EipCommonPacket.ExtractCip(data, out var sequence);
        Assert.Equal(7, sequence);
        Assert.Equal(cip, extracted);
    }
}
