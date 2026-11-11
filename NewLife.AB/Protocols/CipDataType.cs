namespace NewLife.AB.Protocols;

/// <summary>CIP / Logix 数据类型码。原子类型取 CIP Volume 1 的 16 位类型码；STRING 为 Logix 结构 0x02A0。</summary>
public static class CipDataType
{
    /// <summary>BOOL。高字节低四位可为位号，形如 0x0nC1。</summary>
    public const UInt16 Bool = 0x00C1;

    /// <summary>SINT，1 字节有符号</summary>
    public const UInt16 Sint = 0x00C2;

    /// <summary>INT，2 字节有符号</summary>
    public const UInt16 Int = 0x00C3;

    /// <summary>DINT，4 字节有符号</summary>
    public const UInt16 Dint = 0x00C4;

    /// <summary>LINT，8 字节有符号</summary>
    public const UInt16 Lint = 0x00C5;

    /// <summary>USINT，1 字节无符号</summary>
    public const UInt16 Usint = 0x00C6;

    /// <summary>UINT，2 字节无符号</summary>
    public const UInt16 Uint = 0x00C7;

    /// <summary>UDINT，4 字节无符号</summary>
    public const UInt16 Udint = 0x00C8;

    /// <summary>ULINT，8 字节无符号</summary>
    public const UInt16 Ulint = 0x00C9;

    /// <summary>REAL，4 字节浮点</summary>
    public const UInt16 Real = 0x00CA;

    /// <summary>LREAL，8 字节浮点</summary>
    public const UInt16 Lreal = 0x00CB;

    /// <summary>DWORD，4 字节位串</summary>
    public const UInt16 Dword = 0x00D3;

    /// <summary>Logix STRING 结构类型码。线上为 A0 02，后跟 16 位结构句柄。</summary>
    public const UInt16 String = 0x02A0;

    /// <summary>是否为 BOOL（含位号 0x00C1–0x07C1）</summary>
    /// <param name="typeCode">类型码</param>
    /// <returns>是否为 BOOL</returns>
    public static Boolean IsBool(UInt16 typeCode) => (typeCode & 0x00FF) == 0xC1 && (typeCode >> 8) <= 7;

    /// <summary>是否为 Logix 结构（首发识别 STRING 的 0x02A0）</summary>
    /// <param name="typeCode">类型码</param>
    /// <returns>是否为结构</returns>
    public static Boolean IsStructure(UInt16 typeCode) => typeCode == String;

    /// <summary>原子类型的元素字节数。结构类型返回 0。</summary>
    /// <param name="typeCode">类型码</param>
    /// <returns>字节数，未知类型返回 -1</returns>
    public static Int32 GetElementSize(UInt16 typeCode)
    {
        if (IsBool(typeCode)) return 1;
        switch (typeCode)
        {
            case Sint:
            case Usint: return 1;
            case Int:
            case Uint: return 2;
            case Dint:
            case Udint:
            case Real:
            case Dword: return 4;
            case Lint:
            case Ulint:
            case Lreal: return 8;
            case String: return 0;
            default: return -1;
        }
    }

    /// <summary>类型名称</summary>
    /// <param name="typeCode">类型码</param>
    /// <returns>名称</returns>
    public static String GetName(UInt16 typeCode)
    {
        if (IsBool(typeCode)) return "BOOL";
        switch (typeCode)
        {
            case Sint: return "SINT";
            case Int: return "INT";
            case Dint: return "DINT";
            case Lint: return "LINT";
            case Usint: return "USINT";
            case Uint: return "UINT";
            case Udint: return "UDINT";
            case Ulint: return "ULINT";
            case Real: return "REAL";
            case Lreal: return "LREAL";
            case Dword: return "DWORD";
            case String: return "STRING";
            default: return "0x" + typeCode.ToString("X4");
        }
    }

    /// <summary>按名称解析类型码。接受 BOOL/BOOLEAN、REAL/FLOAT、LREAL/DOUBLE。</summary>
    /// <param name="name">类型名</param>
    /// <param name="typeCode">类型码</param>
    /// <returns>是否识别</returns>
    public static Boolean TryParseName(String name, out UInt16 typeCode)
    {
        typeCode = 0;
        if (name == null) return false;
        switch (name.Trim().ToUpperInvariant())
        {
            case "BOOL":
            case "BOOLEAN": typeCode = Bool; return true;
            case "SINT": typeCode = Sint; return true;
            case "INT":
            case "INT16": typeCode = Int; return true;
            case "DINT":
            case "INT32": typeCode = Dint; return true;
            case "LINT":
            case "INT64": typeCode = Lint; return true;
            case "USINT":
            case "BYTE": typeCode = Usint; return true;
            case "UINT":
            case "UINT16":
            case "WORD": typeCode = Uint; return true;
            case "UDINT":
            case "UINT32": typeCode = Udint; return true;
            case "ULINT":
            case "UINT64": typeCode = Ulint; return true;
            case "REAL":
            case "FLOAT":
            case "SINGLE": typeCode = Real; return true;
            case "LREAL":
            case "DOUBLE": typeCode = Lreal; return true;
            case "DWORD": typeCode = Dword; return true;
            case "STRING": typeCode = String; return true;
            default: return false;
        }
    }
}
