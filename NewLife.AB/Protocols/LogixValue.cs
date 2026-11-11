namespace NewLife.AB.Protocols;

/// <summary>一次标签读取的结果。多字节数值为小端。</summary>
public sealed class LogixValue
{
    /// <summary>类型码</summary>
    public UInt16 TypeCode { get; }

    /// <summary>结构句柄。原子类型为 0。</summary>
    public UInt16 StructureHandle { get; }

    /// <summary>元素原始字节，不含类型头</summary>
    public Byte[] Raw { get; }

    /// <summary>元素个数。由调用方传入；单值解码时为 1。</summary>
    public Int32 ElementCount { get; }

    /// <summary>实例化</summary>
    /// <param name="typeCode">类型码</param>
    /// <param name="structureHandle">结构句柄</param>
    /// <param name="raw">元素数据</param>
    /// <param name="elementCount">元素个数</param>
    public LogixValue(UInt16 typeCode, UInt16 structureHandle, Byte[] raw, Int32 elementCount)
    {
        TypeCode = typeCode;
        StructureHandle = structureHandle;
        Raw = raw ?? new Byte[0];
        ElementCount = elementCount;
    }

    /// <summary>是否为结构</summary>
    public Boolean IsStructure => CipDataType.IsStructure(TypeCode);

    /// <summary>从读标签应答数据解码</summary>
    /// <param name="replyData">应答数据</param>
    /// <param name="elementCount">请求的元素个数</param>
    /// <returns>值</returns>
    public static LogixValue Decode(Byte[] replyData, Int32 elementCount)
    {
        if (replyData == null || replyData.Length < 2) throw new InvalidOperationException("标签应答没有类型码");
        var typeCode = BinaryBuffer.ReadUInt16(replyData, 0);
        var offset = 2;
        UInt16 handle = 0;
        if (CipDataType.IsStructure(typeCode))
        {
            if (replyData.Length < 4) throw new InvalidOperationException("结构应答缺少句柄");
            handle = BinaryBuffer.ReadUInt16(replyData, 2);
            offset = 4;
        }

        var raw = new Byte[replyData.Length - offset];
        if (raw.Length > 0) Buffer.BlockCopy(replyData, offset, raw, 0, raw.Length);
        return new LogixValue(typeCode, handle, raw, elementCount);
    }

    /// <summary>BOOL</summary>
    /// <returns>值</returns>
    public Boolean GetBoolean()
    {
        EnsureType(CipDataType.IsBool(TypeCode), "BOOL");
        return Raw.Length > 0 && Raw[0] != 0;
    }

    /// <summary>SINT</summary>
    /// <returns>值</returns>
    public SByte GetSByte()
    {
        EnsureSize(CipDataType.Sint, 1);
        return (SByte)Raw[0];
    }

    /// <summary>INT</summary>
    /// <returns>值</returns>
    public Int16 GetInt16()
    {
        EnsureSize(CipDataType.Int, 2);
        return (Int16)BinaryBuffer.ReadUInt16(Raw, 0);
    }

    /// <summary>DINT</summary>
    /// <returns>值</returns>
    public Int32 GetInt32()
    {
        EnsureSize(CipDataType.Dint, 4);
        return (Int32)BinaryBuffer.ReadUInt32(Raw, 0);
    }

    /// <summary>LINT</summary>
    /// <returns>值</returns>
    public Int64 GetInt64()
    {
        EnsureSize(CipDataType.Lint, 8);
        return (Int64)BinaryBuffer.ReadUInt64(Raw, 0);
    }

    /// <summary>USINT</summary>
    /// <returns>值</returns>
    public Byte GetByte()
    {
        EnsureSize(CipDataType.Usint, 1);
        return Raw[0];
    }

    /// <summary>UINT</summary>
    /// <returns>值</returns>
    public UInt16 GetUInt16()
    {
        EnsureSize(CipDataType.Uint, 2);
        return BinaryBuffer.ReadUInt16(Raw, 0);
    }

    /// <summary>UDINT</summary>
    /// <returns>值</returns>
    public UInt32 GetUInt32()
    {
        EnsureSize(CipDataType.Udint, 4);
        return BinaryBuffer.ReadUInt32(Raw, 0);
    }

    /// <summary>ULINT</summary>
    /// <returns>值</returns>
    public UInt64 GetUInt64()
    {
        EnsureSize(CipDataType.Ulint, 8);
        return BinaryBuffer.ReadUInt64(Raw, 0);
    }

    /// <summary>REAL</summary>
    /// <returns>值</returns>
    public Single GetSingle()
    {
        EnsureSize(CipDataType.Real, 4);
        var bytes = new Byte[4];
        Buffer.BlockCopy(Raw, 0, bytes, 0, 4);
        return BitConverter.ToSingle(bytes, 0);
    }

    /// <summary>LREAL</summary>
    /// <returns>值</returns>
    public Double GetDouble()
    {
        EnsureSize(CipDataType.Lreal, 8);
        var bytes = new Byte[8];
        Buffer.BlockCopy(Raw, 0, bytes, 0, 8);
        return BitConverter.ToDouble(bytes, 0);
    }

    /// <summary>Logix STRING。DATA 按字节映射为字符，长度由 LEN 决定。</summary>
    /// <returns>字符串</returns>
    public String GetString()
    {
        if (TypeCode != CipDataType.String) throw new InvalidOperationException("标签类型为 " + CipDataType.GetName(TypeCode) + "，不是 STRING");
        if (Raw.Length < 4) throw new InvalidOperationException("STRING 数据不足");
        var length = (Int32)BinaryBuffer.ReadUInt32(Raw, 0);
        if (length < 0 || 4 + length > Raw.Length) throw new InvalidOperationException("STRING 长度无效");
        var chars = new Char[length];
        for (var i = 0; i < length; i++) chars[i] = (Char)Raw[4 + i];
        return new String(chars);
    }

    /// <summary>按类型变成标量。元素个数大于 1 时返回数组。</summary>
    /// <returns>标量或数组</returns>
    public Object ToObject()
    {
        if (ElementCount > 1) return ToArray();
        if (CipDataType.IsBool(TypeCode)) return GetBoolean();
        switch (TypeCode)
        {
            case CipDataType.Sint: return GetSByte();
            case CipDataType.Int: return GetInt16();
            case CipDataType.Dint: return GetInt32();
            case CipDataType.Lint: return GetInt64();
            case CipDataType.Usint: return GetByte();
            case CipDataType.Uint: return GetUInt16();
            case CipDataType.Udint: return GetUInt32();
            case CipDataType.Ulint: return GetUInt64();
            case CipDataType.Real: return GetSingle();
            case CipDataType.Lreal: return GetDouble();
            case CipDataType.String: return GetString();
            default: return Raw;
        }
    }

    /// <summary>按元素拆成数组</summary>
    /// <returns>数组</returns>
    public Object ToArray()
    {
        var size = CipDataType.GetElementSize(TypeCode);
        if (size <= 0) throw new InvalidOperationException("该类型不能按原子数组展开");
        var count = Raw.Length / size;
        if (CipDataType.IsBool(TypeCode))
        {
            var values = new Boolean[count];
            for (var i = 0; i < count; i++) values[i] = Raw[i] != 0;
            return values;
        }

        switch (TypeCode)
        {
            case CipDataType.Sint:
                var sbytes = new SByte[count];
                for (var i = 0; i < count; i++) sbytes[i] = (SByte)Raw[i];
                return sbytes;
            case CipDataType.Int:
                var shorts = new Int16[count];
                for (var i = 0; i < count; i++) shorts[i] = (Int16)BinaryBuffer.ReadUInt16(Raw, i * 2);
                return shorts;
            case CipDataType.Dint:
                var ints = new Int32[count];
                for (var i = 0; i < count; i++) ints[i] = (Int32)BinaryBuffer.ReadUInt32(Raw, i * 4);
                return ints;
            case CipDataType.Lint:
                var longs = new Int64[count];
                for (var i = 0; i < count; i++) longs[i] = (Int64)BinaryBuffer.ReadUInt64(Raw, i * 8);
                return longs;
            case CipDataType.Usint:
                var bytes = new Byte[count];
                Buffer.BlockCopy(Raw, 0, bytes, 0, count);
                return bytes;
            case CipDataType.Uint:
                var ushorts = new UInt16[count];
                for (var i = 0; i < count; i++) ushorts[i] = BinaryBuffer.ReadUInt16(Raw, i * 2);
                return ushorts;
            case CipDataType.Udint:
                var uints = new UInt32[count];
                for (var i = 0; i < count; i++) uints[i] = BinaryBuffer.ReadUInt32(Raw, i * 4);
                return uints;
            case CipDataType.Ulint:
                var ulongs = new UInt64[count];
                for (var i = 0; i < count; i++) ulongs[i] = BinaryBuffer.ReadUInt64(Raw, i * 8);
                return ulongs;
            case CipDataType.Real:
                var singles = new Single[count];
                for (var i = 0; i < count; i++)
                {
                    var tmp = new Byte[4];
                    Buffer.BlockCopy(Raw, i * 4, tmp, 0, 4);
                    singles[i] = BitConverter.ToSingle(tmp, 0);
                }
                return singles;
            case CipDataType.Lreal:
                var doubles = new Double[count];
                for (var i = 0; i < count; i++)
                {
                    var tmp = new Byte[8];
                    Buffer.BlockCopy(Raw, i * 8, tmp, 0, 8);
                    doubles[i] = BitConverter.ToDouble(tmp, 0);
                }
                return doubles;
            default:
                return Raw;
        }
    }

    /// <summary>编码 BOOL</summary>
    /// <param name="value">值</param>
    /// <returns>1 字节</returns>
    public static Byte[] EncodeBoolean(Boolean value) => new Byte[] { value ? (Byte)1 : (Byte)0 };

    /// <summary>编码 SINT</summary>
    /// <param name="value">值</param>
    /// <returns>字节</returns>
    public static Byte[] EncodeSByte(SByte value) => new Byte[] { (Byte)value };

    /// <summary>编码 INT</summary>
    /// <param name="value">值</param>
    /// <returns>字节</returns>
    public static Byte[] EncodeInt16(Int16 value)
    {
        var data = new Byte[2];
        BinaryBuffer.WriteUInt16(data, 0, (UInt16)value);
        return data;
    }

    /// <summary>编码 DINT</summary>
    /// <param name="value">值</param>
    /// <returns>字节</returns>
    public static Byte[] EncodeInt32(Int32 value)
    {
        var data = new Byte[4];
        BinaryBuffer.WriteUInt32(data, 0, (UInt32)value);
        return data;
    }

    /// <summary>编码 LINT</summary>
    /// <param name="value">值</param>
    /// <returns>字节</returns>
    public static Byte[] EncodeInt64(Int64 value)
    {
        var data = new Byte[8];
        BinaryBuffer.WriteUInt32(data, 0, (UInt32)value);
        BinaryBuffer.WriteUInt32(data, 4, (UInt32)((UInt64)value >> 32));
        return data;
    }

    /// <summary>编码 USINT</summary>
    /// <param name="value">值</param>
    /// <returns>字节</returns>
    public static Byte[] EncodeByte(Byte value) => new Byte[] { value };

    /// <summary>编码 UINT</summary>
    /// <param name="value">值</param>
    /// <returns>字节</returns>
    public static Byte[] EncodeUInt16(UInt16 value)
    {
        var data = new Byte[2];
        BinaryBuffer.WriteUInt16(data, 0, value);
        return data;
    }

    /// <summary>编码 UDINT</summary>
    /// <param name="value">值</param>
    /// <returns>字节</returns>
    public static Byte[] EncodeUInt32(UInt32 value)
    {
        var data = new Byte[4];
        BinaryBuffer.WriteUInt32(data, 0, value);
        return data;
    }

    /// <summary>编码 ULINT</summary>
    /// <param name="value">值</param>
    /// <returns>字节</returns>
    public static Byte[] EncodeUInt64(UInt64 value)
    {
        var data = new Byte[8];
        BinaryBuffer.WriteUInt32(data, 0, (UInt32)value);
        BinaryBuffer.WriteUInt32(data, 4, (UInt32)(value >> 32));
        return data;
    }

    /// <summary>编码 REAL</summary>
    /// <param name="value">值</param>
    /// <returns>字节</returns>
    public static Byte[] EncodeSingle(Single value) => BitConverter.GetBytes(value);

    /// <summary>编码 LREAL</summary>
    /// <param name="value">值</param>
    /// <returns>字节</returns>
    public static Byte[] EncodeDouble(Double value) => BitConverter.GetBytes(value);

    /// <summary>编码 Logix STRING 的 LEN + DATA。容量默认 82，不足补 0。</summary>
    /// <param name="value">字符串</param>
    /// <param name="capacity">DATA 容量</param>
    /// <returns>字节</returns>
    public static Byte[] EncodeString(String value, Int32 capacity)
    {
        if (value == null) value = "";
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (value.Length > capacity) throw new ArgumentException("字符串长度超过 STRING 容量 " + capacity);

        var data = new Byte[4 + capacity];
        BinaryBuffer.WriteUInt32(data, 0, (UInt32)value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] > 255) throw new ArgumentException("Logix STRING 只接受单字节字符");
            data[4 + i] = (Byte)value[i];
        }
        return data;
    }

    /// <summary>构造一个 BOOL 结果，用于位读取</summary>
    /// <param name="value">值</param>
    /// <returns>结果</returns>
    public static LogixValue FromBoolean(Boolean value) =>
        new LogixValue(CipDataType.Bool, 0, EncodeBoolean(value), 1);

    private void EnsureSize(UInt16 expected, Int32 size)
    {
        if (TypeCode != expected) throw new InvalidOperationException("标签类型为 " + CipDataType.GetName(TypeCode) + "，不是 " + CipDataType.GetName(expected));
        if (Raw.Length < size) throw new InvalidOperationException("标签数据长度不足");
    }

    private void EnsureType(Boolean ok, String name)
    {
        if (!ok) throw new InvalidOperationException("标签类型为 " + CipDataType.GetName(TypeCode) + "，不是 " + name);
        if (Raw.Length < 1) throw new InvalidOperationException("标签数据长度不足");
    }
}
