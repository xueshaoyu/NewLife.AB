using System.Text;

namespace NewLife.AB.Protocols;

/// <summary>CIP 路径段。端口段、逻辑段与 ANSI 扩展符号段。</summary>
public static class CipPath
{
    /// <summary>8 位类段</summary>
    /// <param name="classId">类</param>
    /// <param name="instanceId">实例</param>
    /// <returns>路径</returns>
    public static Byte[] ClassInstance(Byte classId, Byte instanceId) =>
        new Byte[] { 0x20, classId, 0x24, instanceId };

    /// <summary>背板或网络端口，链路地址为单字节（槽号）</summary>
    /// <param name="port">端口，0–14。1 为背板，2 常为以太网。</param>
    /// <param name="link">链路地址</param>
    /// <returns>路径</returns>
    public static Byte[] Port(Byte port, Byte link)
    {
        if (port > 14) throw new ArgumentException("端口号必须在 0 到 14 之间", nameof(port));
        return new Byte[] { port, link };
    }

    /// <summary>端口段，链路地址为字符串（如 IP）</summary>
    /// <param name="port">端口，0–14</param>
    /// <param name="linkAddress">链路地址</param>
    /// <returns>按字对齐的路径</returns>
    public static Byte[] Port(Byte port, String linkAddress)
    {
        if (port > 14) throw new ArgumentException("端口号必须在 0 到 14 之间", nameof(port));
        if (linkAddress == null) throw new ArgumentNullException(nameof(linkAddress));
        var raw = Encoding.ASCII.GetBytes(linkAddress);
        if (raw.Length > 255) throw new ArgumentException("链路地址过长", nameof(linkAddress));

        var size = 2 + raw.Length;
        var pad = (size & 1) == 1;
        var buffer = new Byte[size + (pad ? 1 : 0)];
        buffer[0] = (Byte)(port | 0x10);
        buffer[1] = (Byte)raw.Length;
        Buffer.BlockCopy(raw, 0, buffer, 2, raw.Length);
        return buffer;
    }

    /// <summary>元素下标。8 位用 0x28，16 位用 0x29，32 位用 0x2A。</summary>
    /// <param name="index">下标</param>
    /// <returns>路径</returns>
    public static Byte[] Element(UInt32 index)
    {
        if (index <= 0xFF) return new Byte[] { 0x28, (Byte)index };
        if (index <= 0xFFFF)
        {
            var buffer = new Byte[4];
            buffer[0] = 0x29;
            BinaryBuffer.WriteUInt16(buffer, 2, (UInt16)index);
            return buffer;
        }

        var wide = new Byte[6];
        wide[0] = 0x2A;
        BinaryBuffer.WriteUInt32(wide, 2, index);
        return wide;
    }

    /// <summary>ANSI 扩展符号段（0x91）。奇数字符补 0 对齐到字。</summary>
    /// <param name="symbol">符号名</param>
    /// <returns>路径</returns>
    public static Byte[] Symbol(String symbol)
    {
        if (symbol == null) throw new ArgumentNullException(nameof(symbol));
        if (symbol.Length == 0 || symbol.Length > 255) throw new ArgumentException("符号名长度必须在 1 到 255 之间", nameof(symbol));

        var raw = Encoding.ASCII.GetBytes(symbol);
        var pad = (raw.Length & 1) == 1;
        var buffer = new Byte[2 + raw.Length + (pad ? 1 : 0)];
        buffer[0] = 0x91;
        buffer[1] = (Byte)raw.Length;
        Buffer.BlockCopy(raw, 0, buffer, 2, raw.Length);
        return buffer;
    }

    /// <summary>拼接路径</summary>
    /// <param name="segments">各段</param>
    /// <returns>路径</returns>
    public static Byte[] Concat(params Byte[][] segments)
    {
        var writer = new ByteWriter();
        if (segments != null)
        {
            foreach (var segment in segments)
            {
                writer.Bytes(segment);
            }
        }
        return writer.ToArray();
    }

    /// <summary>解析路由字符串。成对的“端口,链路”，例如 1,0 或 2,192.168.1.10,1,0。</summary>
    /// <param name="route">路由</param>
    /// <returns>路径</returns>
    public static Byte[] ParseRoute(String route)
    {
        if (route == null) throw new ArgumentNullException(nameof(route));
        var text = route.Trim();
        if (text.Length == 0) throw new ArgumentException("路由路径不能为空", nameof(route));

        var tokens = text.Split(',');
        if ((tokens.Length & 1) == 1) throw new ArgumentException("路由路径必须是端口,链路成对出现，例如 1,0", nameof(route));

        var writer = new ByteWriter();
        for (var i = 0; i < tokens.Length; i += 2)
        {
            var portText = tokens[i].Trim();
            var link = tokens[i + 1].Trim();
            if (!Byte.TryParse(portText, out var port)) throw new ArgumentException("路由端口无效: " + portText, nameof(route));

            if (link.IndexOf('.') < 0 && Byte.TryParse(link, out var linkByte))
                writer.Bytes(Port(port, linkByte));
            else
                writer.Bytes(Port(port, link));
        }

        var bytes = writer.ToArray();
        if ((bytes.Length & 1) == 1) throw new ArgumentException("路由路径未按字对齐", nameof(route));
        return bytes;
    }

    /// <summary>路径的字数（CIP Path Size）</summary>
    /// <param name="path">路径</param>
    /// <returns>字数</returns>
    public static Byte SizeInWords(Byte[] path)
    {
        var length = path == null ? 0 : path.Length;
        if ((length & 1) == 1) throw new ArgumentException("CIP 路径必须按字对齐");
        if (length / 2 > 255) throw new ArgumentException("CIP 路径超过 255 字");
        return (Byte)(length / 2);
    }
}
