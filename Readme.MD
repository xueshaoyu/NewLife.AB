# NewLife.AB - 罗克韦尔 / Allen-Bradley Logix 通信协议库

![GitHub License](https://img.shields.io/github/license/newlifex/NewLife.AB?logo=github)
![Nuget](https://img.shields.io/nuget/v/NewLife.AB?logo=nuget)

NewLife.AB 是一个纯 .NET 实现的罗克韦尔 / Allen-Bradley **Logix** 通信协议库。首发走今天的主流路径：EtherNet/IP（TCP 44818）上的 CIP 显式报文，用符号标签读写 ControlLogix、CompactLogix 以及兼容的 Logix 5000 控制器。

协议层只依赖 BCL 和 NewLife 基础库，不引用罗克韦尔专有运行时。服务码和数据类型码使用 CIP 与 Logix 5000 数据访问手册中的公开值。

**NuGet**：NewLife.AB  
**文档**：https://newlifex.com/iot  
**需求文档**：[Doc/需求文档.md](Doc/需求文档.md)  
**架构设计**：[Doc/架构设计.md](Doc/架构设计.md)

---

## 核心特性

- **EtherNet/IP**：Register Session、List Identity、List Services、SendRRData、SendUnitData
- **符号标签**：Read / Write Tag、分段读写、按位 Read Modify Write、多标签 Multiple Service Packet
- **路由**：默认背板路径 `1,槽号`。可改为自定义路由，或关闭路由、直接对 CPU 网口发 UCMM
- **显式连接**：需要时 Forward Open（服务 0x54），之后走 SendUnitData。默认使用未连接消息
- **类型化 API**：Int16/32/64、UInt16/32/64、Single、Double、Boolean、String、Byte[]，同步和异步都有
- **程序作用域**：`Program:MainProgram.Tag`
- **自动重连**：链路断开按 1s、2s、3s 退避；会话超时（封装状态 0x0064）后重新注册
- **模拟器**：`LogixServer` 用于开发和单元测试，不需要真实 PLC
- **IoT**：`AbDriver` 实现 NewLife.IoT 驱动接口

## 安装

```bash
dotnet add package NewLife.AB
```

```xml
<PackageReference Include="NewLife.AB" Version="1.0.*" />
```

目标框架与 NewLife.Omron 一致：`net45`、`net461`、`netstandard2.0`、`netstandard2.1`。

## 快速开始

```csharp
using NewLife.AB.Protocols;

using var client = new LogixClient("192.168.1.10", 44818);
client.Slot = 0;          // 背板槽号，路径为 1,0
client.Connect();

int value = client.ReadInt32("Program:MainProgram.Counter");
client.WriteInt32("Program:MainProgram.Counter", 123);

bool running = client.ReadBoolean("Running");
client.WriteBoolean("ControlWord.2", true);   // 只改 DINT 的 bit2

float temp = client.ReadSingle("Temperature");
client.WriteString("Message", "OK");

int[] row = client.ReadInt32("Positions", 4);
var batch = client.ReadTags(new[] { "A", "B" });

var info = client.ReadIdentity();
Console.WriteLine(info.ProductName);

client.Close();
```

异步方法以 `Async` 结尾，库内部使用 `ConfigureAwait(false)`。

```csharp
await client.ConnectAsync();
int value = await client.ReadInt32Async("Counter");
await client.WriteSingleAsync("Temperature", 36.6f);
```

直连 CPU 网口、控制器拒绝背板路由时：

```csharp
client.RouteToSlot = false;
```

自定义路径（先到以太网模块，再进背板槽 2）：

```csharp
client.RoutePath = "2,192.168.1.10,1,2";
```

改用已连接显式报文：

```csharp
client.ConnectedMessaging = true;
client.Connect();   // 注册会话后 Forward Open
```

### IoT 驱动

```csharp
using NewLife.AB.Drivers;

var driver = new AbDriver();
var pm = new AbParameter
{
    Address = "192.168.1.10:44818",
    Slot = 0
};
var node = driver.Open(null, pm);
var data = driver.Read(node, points);
driver.Write(node, point, 12345);
driver.Close(node);
```

点位 `Length` 按字节计。类型为 DINT 且长度为 4 时读 1 个元素，长度为 16 时读 4 个元素。多个标量点会走多服务打包。

### 控制台

`Test` 工程可以直接指向以后的真实 PLC，不必再搭通信骨架：

```bash
dotnet run --project Test -- 192.168.1.10:44818 --slot 0 --tag Program:MainProgram.Counter
dotnet run --project Test -- 192.168.1.10:44818 --slot 2 --tag Counter --write 123
```

不带参数时进入交互：输入标签名读取，输入 `标签=整数` 按 DINT 写入。`--direct` 关闭背板路由。

### 模拟器

```csharp
using var server = new LogixServer();
server.Start();    // 端口 0 时由系统分配，看 server.Port

using var client = new LogixClient("127.0.0.1", server.Port);
client.Connect();
client.WriteInt32("Counter", 123);
```

模拟器默认会在写入时创建尚不存在的标签。真实控制器不会建点，不存在的标签返回 CIP 状态 0x05。把 `AutoCreateTags` 设为 false 即可按真实行为测试。

## 支持的控制器

| 系列 | 首发 |
|------|------|
| ControlLogix / GuardLogix（1756） | 支持。经 1756-ENxT 时设置 CPU 槽号 |
| CompactLogix / Compact GuardLogix | 支持。内置网口通常槽号 0，必要时 `RouteToSlot = false` |
| 其它 Logix 5000（如 5069、1769） | 支持符号标签的型号按同一路径访问 |
| SLC 500、PLC-5、MicroLogix | **不支持**。PCCC/DF1 留在 `PcccLegacy`，调用会抛出 `NotSupportedException` |

## 地址

```
[类型:]标签[.成员][下标][.位]
Program:程序名.标签
```

| 地址 | 含义 |
|------|------|
| `Counter` | 控制器作用域标签 |
| `DINT:Counter` | 带类型提示。写 `Byte[]` 或消除歧义时有用 |
| `Positions[3]` | 一维数组元素，下标从 0 起 |
| `Positions[3].0` | 该元素的第 0 位 |
| `ControlWord.2` | 标签的第 2 位。纯数字的最后一段是位，不是成员名 |
| `Motor.Speed` | 结构成员。成员本身应是原子类型 |
| `Program:MainProgram.Tag` | 程序作用域。`Program:MainProgram` 是一段符号 |
| `REAL:Program:Main.Temp` | 类型提示加程序作用域 |
| `Grid[1,2]` | 多维元素的路径会编码；模拟器只保存一维数组 |

类型提示接受 `BOOL`、`SINT`、`INT`、`DINT`、`LINT`、`USINT`、`UINT`、`UDINT`、`ULINT`、`REAL`、`LREAL`、`STRING`，以及 `BOOLEAN`、`FLOAT`、`DOUBLE` 这几个别名。`Program:` 不是类型名。

位写入走 Read Modify Write（0x4E），只改目标位。不知道元素宽度时会先读一次；写成 `DINT:ControlWord.2` 可以跳过这次读取。

## 数据类型

CIP 多字节整数和浮点都是小端，没有欧姆龙那种 ABCD/CDAB 切换。

| 类型 | 类型码 | 宽度 | .NET |
|------|--------|------|------|
| BOOL | 0x00C1（位号可为 0x0nC1） | 1 | Boolean。非 0 为真，兼容旧固件返回的 0xFF |
| SINT | 0x00C2 | 1 | SByte |
| INT | 0x00C3 | 2 | Int16 |
| DINT | 0x00C4 | 4 | Int32 |
| LINT | 0x00C5 | 8 | Int64 |
| USINT | 0x00C6 | 1 | Byte |
| UINT | 0x00C7 | 2 | UInt16 |
| UDINT | 0x00C8 | 4 | UInt32 |
| ULINT | 0x00C9 | 8 | UInt64 |
| REAL | 0x00CA | 4 | Single |
| LREAL | 0x00CB | 8 | Double |
| STRING | 0x02A0 | 结构 | String |

Logix 的 STRING 不是 CIP 的 0x00D0。线上是 `A0 02` 加 16 位结构句柄，后面是 DINT 长度和 SINT 数组。标准 STRING 的 DATA 容量是 82。写之前如果不知道句柄，客户端会先读一次；也可以直接设置 `StringStructureHandle`。模拟器的默认句柄是 `0x00CE`。字符按单字节保存，不支持超过 255 的码位。

读到的类型和所调方法不一致时会抛异常，不会把 DINT 静默当成 INT。

## 命令与服务

### 封装命令

| 命令 | 代码 | 说明 |
|------|------|------|
| NOP | 0x0000 | 无应答 |
| ListServices | 0x0004 | 通信服务 |
| ListIdentity | 0x0063 | 身份项 0x000C |
| RegisterSession | 0x0065 | 协议版本 1 |
| UnRegisterSession | 0x0066 | 无应答 |
| SendRRData | 0x006F | 未连接显式报文 |
| SendUnitData | 0x0070 | 已连接显式报文 |

### CIP 服务

| 服务 | 代码 | 对象 | 说明 |
|------|------|------|------|
| Get_Attributes_All | 0x01 | Identity 0x01 | 控制器身份 |
| Multiple Service Packet | 0x0A | Message Router 0x02 | 多标签打包 |
| Read Tag | 0x4C | 符号路径 | 读标签 |
| Write Tag | 0x4D | 符号路径 | 写标签 |
| Read Modify Write Tag | 0x4E | 符号路径 | 按位修改 |
| Read Tag Fragmented | 0x52 | 符号路径 | 分段读。与 Unconnected Send 同码，靠路径区分 |
| Write Tag Fragmented | 0x53 | 符号路径 | 分段写 |
| Unconnected Send | 0x52 | Connection Manager 0x06 | 经路由转发 |
| Forward Open | 0x54 | Connection Manager 0x06 | 建立 Class 3 显式连接 |
| Forward Close | 0x4E | Connection Manager 0x06 | 关闭连接。与 Read Modify Write 同码，靠路径区分 |

符号段是 ANSI 扩展符号 `0x91`。数组下标是元素逻辑段 `0x28` / `0x29` / `0x2A`。未连接发送的默认时间参数是优先级/时间片 `0x07`、超时片 `0x10`（约 2 秒）。显式连接使用 500 字节、可变长度、点到点的参数 `0x43F4`，传输触发 `0xA3`。更大的标签由分段服务搬运，不使用 Large Forward Open（0x5B）。

应答装不下时通用状态为 `0x06`，客户端改用分段读把数据拼完。常见失败：

| 状态 | 含义 |
|------|------|
| 0x04 | 路径段错误，槽号或路由不对时常见 |
| 0x05 | 路径目标未知，标签不存在 |
| 0x06 | 只返回了一部分，需要分段 |
| 0x1E | 多服务包里有子服务失败 |
| 0xFF / 0x2105 | 下标超出标签 |
| 0xFF / 0x2107 | 写入类型和标签类型不符 |
| 封装 0x0064 | 会话无效或超时，客户端会重新注册 |

## 明确不做

- **SLC、PLC-5、MicroLogix**：不实现 PCCC / DF1。扩展点是 `PcccLegacy`（服务码 0x4B），现在调用会失败，避免被当成已支持
- **UDT 模板**：不下载 Template 对象，不把整个结构体还原成成员树。已知成员名时，`Motor.Speed` 这种原子成员可以读写；整块结构只作为原始字节
- **符号浏览**：不实现类 0x6B 的实例枚举。调用方需要知道标签名
- **多维数组的模拟存储**：路径编码器会写出每一维下标，模拟器只接受一个下标

## 协议说明

- 默认端口 **44818**。多字节字段小端
- 同一 `LogixClient` 上不要并发混用同步和异步方法。同步走 `lock`，异步走 `SemaphoreSlim(1,1)`
- 外部访问被设成 None 的标签，控制器会拒绝，状态原样抛成 `CipException`
- `OriginatorVendorId` 默认 `0x1234`，只参与 Forward Open / Close 的连接匹配，不是 ODVA 厂商注册号
- 模拟器的厂商号、产品名是测试数据，产品名默认 `LogixServer`

## 单元测试

不连接真实 PLC。`dotnet test XUnitTest/XUnitTest.csproj`

| 测试类 | 覆盖 |
|--------|------|
| EipPacketTests | 封装头往返、命令码、SendRRData / SendUnitData |
| CipMessageTests | 读/写标签公开样例、状态、位掩码、多服务偏移、Unconnected Send、Forward Open 形状、类型码 |
| LogixAddressTests | 标签、类型提示、数组、位、程序作用域、路由、PCCC 拒绝 |
| LogixValueTests | 原子类型、BOOL、STRING、数组、类型不符 |
| LogixClientTests | 默认值、连接失败、未连接读取、重复关闭 |
| LogixServerTests | 模拟器读写、位、多标签、身份、坏路径、坏槽、强制失败、会话失效、断线重连、分段、Forward Open、异步 |
| AbDriverTests | 参数、打开/关闭、空读取、不支持的类型、驱动读写、坏路径 |

合计 74 个测试。

## 新生命项目矩阵

各项目默认支持 net10.0/net9.0/netstandard2.1/netstandard2.0/net4.8/net4.6.1/net4.5，旧版（2024.0801）支持 net4.0/net2.0。

| 项目 | 年份 | 说明 |
| :-- | :---: | :-- |
| 基础组件 | | 支撑其它中间件以及产品项目 |
| NewLife.Core | 2002 | 核心库，日志、配置、缓存、网络、序列化、APM 性能追踪 |
| NewLife.XCode | 2005 | 大数据中间件，单表百亿级，MySql/SQLite/SqlServer/Oracle/PostgreSql/达梦，自动分表，读写分离 |
| NewLife.Net | 2005 | 网络库，单机千万级吞吐率，单机百万级连接 |
| NewLife.Remoting | 2011 | 协议通信库，提供 CS 应用通信框架，支持 Http/RPC |
| NewLife.Cube | 2010 | 魔方快速开发平台，集成用户权限、SSO、OAuth |
| NewLife.Agent | 2008 | 服务管理组件，Windows 服务、Linux systemd |
| NewLife.Zero | 2020 | Zero 零代脚手架 |
| 中间件 | | 对接知名中间件平台 |
| NewLife.Redis | 2017 | Redis 客户端 |
| NewLife.RocketMQ | 2018 | RocketMQ 纯托管客户端 |
| NewLife.MQTT | 2019 | 物联网消息协议 |
| NewLife.IoT | 2022 | IoT 标准库 |
| NewLife.Modbus | 2022 | Modbus TCP/RTU/ASCII |
| NewLife.Siemens | 2022 | 西门子 PLC |
| NewLife.Omron | 2022 | 欧姆龙 PLC |
| NewLife.Melsec | 2022 | 三菱 PLC |
| NewLife.AB | 2026 | 罗克韦尔 / Allen-Bradley Logix，EtherNet/IP CIP |
| NewLife.Map | 2022 | 地图组件库 |
| NewLife.Audio | 2023 | 音频编解码库 |
| 产品平台 | | 产品平台级 |
| Stardust | 2018 | 星尘，分布式服务平台 |
| AntJob | 2019 | 蚂蚁调度 |
| NewLife.ERP | 2021 | 企业 ERP |
| CrazyCoder | 2006 | 码神工具 |
| EasyIO | 2023 | 简易文件存储 |
| XProxy | 2005 | 反向代理 |
| FIoT 物联网平台 | 2020 | 物联网整体解决方案 |
| SmartA2 | 2019 | 嵌入式工业计算机 |

## 新生命开发团队

新生命团队（NewLife）成立于 2002 年，是物联网行业解决方案提供者，致力于提供软硬件应用方案咨询、系统架构规划与开发服务。

网站：https://newlifex.com · 开源组织：https://github.com/NewLifeX
