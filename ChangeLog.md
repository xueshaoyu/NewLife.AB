# NewLife.AB 版本更新记录

## v1.0.2026.1111 (2026-11-11)

### 首个正式版本
- **EtherNet/IP**：封装头、Register Session、UnRegister Session、List Services、List Identity、SendRRData、SendUnitData
- **Logix 符号标签**：Read Tag / Write Tag、分段读写、Read Modify Write、Multiple Service Packet
- **路由**：默认背板 `1,槽号`，可关闭路由或填写 `2,IP,1,槽号`；可选 Forward Open（0x54）显式连接
- **数据类型**：BOOL、SINT、INT、DINT、LINT、USINT、UINT、UDINT、ULINT、REAL、LREAL、STRING，以及这些原子类型的一维数组
- **地址**：标签、`Program:程序.标签`、数组下标、位号、可选类型提示
- **类型化 API**：Int16/32/64、UInt16/32/64、Single、Double、Boolean、String、Byte[] 的同步和异步方法
- **自动重连**：连接中断按 1s/2s/3s 退避重连；会话句柄失效（0x0064）后重新注册
- **模拟服务器**：LogixServer 覆盖上述服务，单元测试不需要真实 PLC
- **IoT 集成**：`AbDriver` 实现 NewLife.IoT 驱动接口，多节点共享连接
- **明确不做**：SLC / PLC-5 / MicroLogix 的 PCCC/DF1，见 `PcccLegacy`

### 非目标
- 不下载 UDT 模板，不把整个结构体解析成对象。原子成员仍可用符号路径读取
- 不实现符号实例寻址（类 0x6B）和 Large Forward Open（0x5B）
- 模拟器只保存一维数组。多维下标会编进 CIP 路径，供真实控制器使用
