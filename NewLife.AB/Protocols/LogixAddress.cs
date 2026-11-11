using System.Text;

namespace NewLife.AB.Protocols;

/// <summary>标签路径中的一段符号，可带数组下标</summary>
public sealed class LogixSymbol
{
    /// <summary>符号名</summary>
    public String Name { get; }

    /// <summary>数组下标。一维数组只有一个下标；多维数组的全部下标会编进路径。</summary>
    public UInt32[] Elements { get; }

    /// <summary>实例化</summary>
    /// <param name="name">符号名</param>
    /// <param name="elements">下标</param>
    public LogixSymbol(String name, UInt32[] elements)
    {
        Name = name;
        Elements = elements ?? new UInt32[0];
    }
}

/// <summary>Logix 标签地址</summary>
/// <remarks>
/// 格式：
/// [类型:]符号[.成员][下标][.位]
/// Program:程序名.标签
/// 示例：DINT:Counter、Positions[3]、Positions[3].0、Program:MainProgram.Tag
/// 纯数字的最后一段视为位号，不视为成员名。
/// </remarks>
public sealed class LogixAddress
{
    /// <summary>类型提示。地址未写类型时为 null。</summary>
    public UInt16? TypeCode { get; }

    /// <summary>符号段</summary>
    public LogixSymbol[] Symbols { get; }

    /// <summary>位号。无位访问时为 null。</summary>
    public Int32? Bit { get; }

    /// <summary>实例化</summary>
    /// <param name="typeCode">类型提示</param>
    /// <param name="symbols">符号段</param>
    /// <param name="bit">位号</param>
    public LogixAddress(UInt16? typeCode, LogixSymbol[] symbols, Int32? bit)
    {
        TypeCode = typeCode;
        Symbols = symbols ?? new LogixSymbol[0];
        Bit = bit;
    }

    /// <summary>解析地址</summary>
    /// <param name="address">地址</param>
    /// <returns>地址对象</returns>
    public static LogixAddress Parse(String address)
    {
        if (address == null) throw new ArgumentNullException(nameof(address));
        var text = address.Trim();
        if (text.Length == 0) throw new ArgumentException("地址不能为空", nameof(address));

        UInt16? typeCode = null;
        var colon = text.IndexOf(':');
        if (colon > 0)
        {
            var prefix = text.Substring(0, colon);
            if (CipDataType.TryParseName(prefix, out var code))
            {
                typeCode = code;
                text = text.Substring(colon + 1).Trim();
                if (text.Length == 0) throw new ArgumentException("类型提示后缺少标签名", nameof(address));
            }
        }

        var parts = SplitDots(text);
        if (parts.Count == 0) throw new ArgumentException("地址不能为空", nameof(address));

        Int32? bit = null;
        var last = parts[parts.Count - 1];
        if (parts.Count > 1 && last.IndexOf('[') < 0 && IsAllDigits(last))
        {
            if (!Int32.TryParse(last, out var bitValue) || bitValue < 0)
                throw new ArgumentException("位号无效: " + last, nameof(address));
            bit = bitValue;
            parts.RemoveAt(parts.Count - 1);
        }

        var symbols = new LogixSymbol[parts.Count];
        for (var i = 0; i < parts.Count; i++)
        {
            symbols[i] = ParseSymbol(parts[i]);
        }

        return new LogixAddress(typeCode, symbols, bit);
    }

    /// <summary>存储键。符号名用点连接，不含下标和位。</summary>
    public String Key
    {
        get
        {
            var builder = new StringBuilder();
            for (var i = 0; i < Symbols.Length; i++)
            {
                if (i > 0) builder.Append('.');
                builder.Append(Symbols[i].Name);
            }
            return builder.ToString();
        }
    }

    /// <summary>全部数组下标</summary>
    public UInt32[] AllElements
    {
        get
        {
            var list = new List<UInt32>();
            foreach (var symbol in Symbols)
            {
                foreach (var element in symbol.Elements)
                {
                    list.Add(element);
                }
            }
            return list.ToArray();
        }
    }

    /// <summary>编成 CIP 符号路径</summary>
    /// <returns>路径</returns>
    public Byte[] ToPath()
    {
        var writer = new ByteWriter();
        foreach (var symbol in Symbols)
        {
            writer.Bytes(CipPath.Symbol(symbol.Name));
            foreach (var element in symbol.Elements)
            {
                writer.Bytes(CipPath.Element(element));
            }
        }
        return writer.ToArray();
    }

    /// <summary>还原为地址字符串</summary>
    /// <returns>地址</returns>
    public override String ToString()
    {
        var builder = new StringBuilder();
        if (TypeCode.HasValue)
        {
            builder.Append(CipDataType.GetName(TypeCode.Value));
            builder.Append(':');
        }

        for (var i = 0; i < Symbols.Length; i++)
        {
            if (i > 0) builder.Append('.');
            builder.Append(Symbols[i].Name);
            if (Symbols[i].Elements.Length > 0)
            {
                builder.Append('[');
                for (var j = 0; j < Symbols[i].Elements.Length; j++)
                {
                    if (j > 0) builder.Append(',');
                    builder.Append(Symbols[i].Elements[j]);
                }
                builder.Append(']');
            }
        }

        if (Bit.HasValue)
        {
            builder.Append('.');
            builder.Append(Bit.Value);
        }
        return builder.ToString();
    }

    private static LogixSymbol ParseSymbol(String part)
    {
        var bracket = part.IndexOf('[');
        var name = part;
        UInt32[] elements = new UInt32[0];
        if (bracket >= 0)
        {
            if (part[part.Length - 1] != ']') throw new ArgumentException("数组下标未闭合: " + part);
            name = part.Substring(0, bracket);
            var inner = part.Substring(bracket + 1, part.Length - bracket - 2);
            var tokens = inner.Split(',');
            elements = new UInt32[tokens.Length];
            for (var i = 0; i < tokens.Length; i++)
            {
                var token = tokens[i].Trim();
                if (!UInt32.TryParse(token, out elements[i])) throw new ArgumentException("数组下标无效: " + token);
            }
        }

        if (name.Length == 0) throw new ArgumentException("标签名不能为空");
        if (name.IndexOf(' ') >= 0 || name.IndexOf('\t') >= 0) throw new ArgumentException("标签名不能包含空白: " + name);
        return new LogixSymbol(name, elements);
    }

    private static List<String> SplitDots(String text)
    {
        var parts = new List<String>();
        var start = 0;
        var depth = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '[') depth++;
            else if (ch == ']') depth--;
            else if (ch == '.' && depth == 0)
            {
                parts.Add(text.Substring(start, i - start));
                start = i + 1;
            }
        }
        parts.Add(text.Substring(start));
        return parts;
    }

    private static Boolean IsAllDigits(String text)
    {
        if (text.Length == 0) return false;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] < '0' || text[i] > '9') return false;
        }
        return true;
    }
}
