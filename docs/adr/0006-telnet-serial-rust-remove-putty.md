# ADR-0006: Telnet/Serial 迁移到 Rust 核心并清理 PuTTY/KiTTY

Telnet 与 Serial 会话目前通过 `PuttyRunner` 拉起外部 `putty.exe` 并嵌入其原生窗口；SSH 虽已在 ADR-0001 迁到进程内 Rust（`RustSshRunner`），但 PuTTY/KiTTY 代码、资源与可选 runner 仍全部保留。我们决定：把 Telnet/Serial 也迁到进程内 Rust 核心，并彻底删除 PuTTY 与 KiTTY 的全部代码、资源与可选 runner。

原因：Telnet/Serial 是仅剩的外部进程依赖（SSH 已 Rust 化），清理后终端协议栈完全统一（SSH/Telnet/Serial 共享同一 `TermSession` trait 与 `RustTerminalHost` 宿主）；同时消除 2MB 随包分发的 putty.exe/kitty 资源、一片 `Ui/Utils/PuTTY/` 维护面与两套 runner 设置页。上游 `IPuttyConnectable` 接口自带头注释 `// TODO: delete after 2026-01-01`，本决策兑现该清理意图。

**Status**: accepted

## 决策清单

本轮共 20 个决策（Q1–Q20），按主题分组：

### 清理范围

- **Q1 - 彻底删除**：`PuttyRunner`/`KittyRunner`、`Ui/Utils/PuTTY/` 全部、`putty.exe`/`kitty_portable.exe`/`PuttyThemes.json` 资源、两个 Runner 设置页、`IntegrateHost` 中 PuTTY 专属分支、mRemoteNG 导入器中的引用，全部删除。SSH/Telnet/Serial 只走 Rust（+ 可选的 `ExternalRunner` 自定义 exe）。
- **Q2 - 废弃 net6/net48**：从 csproj 移除 `ReleaseNet6`/`ReleaseNet48` 配置与相关条件编译分支。CI 已不发布这两个目标，`RUST_SSH` 只定义在 net9，保留只会让 `#if` 越积越多。
- **Q9 - 模型层全删**：删除 `IPuttyConnectable` 接口与三个协议模型上的 `ExternalSessionConfigPath`/`ExternalKittySessionConfigPath` 字段、`SerialFormView` 的 KiTTY 行与事件。Newtonsoft `MissingMemberHandling.Ignore` 保证老 JSON 无损反序列化。
- **Q17 - 保留 IntegrateHost**：`IntegrateHost` 是通用外部进程宿主（`ExternalRunner`/`AppRunner` 都走它），不是 PuTTY 专属。只删其 PuTTY 专属逻辑，宿主本身保留。
- **Q19 - 字段直接删**：`ExternalSessionConfigPath`/`ExternalKittySessionConfigPath` 直接删，不保留 `[JsonIgnore]` 空壳。Newtonsoft 默认忽略未知字段。
- **Q20 - 保留 ExternalRunner**：Telnet/Serial 除 Rust runner 外，保留 `ExternalRunner`（用户自定义 exe）作为可选 runner，与 SSH 对齐，走现有 `IntegrateHost` 机制。

### Rust 会话设计

- **Q3 - Telnet 最小 IAC 协商**：对 `WILL`/`DO` 回 `WONT`/`DONT`，支持 `NAWS` resize 上报，其余字节原样透传。不追求 RFC 854 全量（现代设备如 Cisco/华为/linux telnetd 都能覆盖）。
- **Q4 - Serial 用 serialport crate**：社区标准库，Windows 走 WinAPI，参数（baud/parity/stopbits/flowcontrol）与 Serial 模型枚举直接对应。不选 C# `System.IO.Ports`（违背"迁到 Rust"诉求），不自己造 WinAPI 封装。
- **Q12 - 编码按协议分**：SSH/Telnet 用 UTF-8；Serial 加可配置 Code Page（重灾区：嵌入式/PLC 设备常用 GBK/Latin-1）。
- **Q13 - 编码存 code page 数字**：`Serial`/`Telnet` 模型加 `EncodingCodePage`（int），`Encoding.GetEncoding(int)` 零映射（UTF-8=65001、Latin-1=28591、GBK=936 等）。不存 PuTTY 显示名（37 项映射表维护成本高）。
- **Q14 - 编码配置放编辑器表单**：`SerialFormView`/`TelnetFormView` 各加一行 Character set 下拉，显示 `Encoding.GetEncoding(cp).DisplayName`。
- **Q15 - Telnet 同样加编码字段**：复用同一套机制（`EncodingCodePage` + 下拉），PuTTY 时代 Telnet 有编码选项，去掉是功能回退。
- **Q16 - 编码转换在 C# 桥接层**：`RustSshConnection` 构造时收 `Encoding`，`PushRemoteData` 用 `encoding.GetString(data)`、`WriteInput` 用 `encoding.GetBytes(text)`。Rust 不感知编码（`TermSession` trait 五方法不变）。
- **Q18 - trait 不感知协议**：`TermSession` 保持 `write/poll_read/is_closed/error_message/resize` 五方法，SSH/Telnet/Serial 实现统一签名，编码全部在 C# 层。

### 桥接与宿主

- **Q6 - C# 映射串口枚举**：`SerialSession` FFI 收 `(port, baud, databits, parity, stopbits, flowcontrol)`，C# 把 `"NONE"`/`"XON/XOFF"` 等字符串映射成 int 传入。Rust 不写字符串解析。
- **Q8 - 泛化 RustSshHost → RustTerminalHost**：构造函数改吃 `ProtocolBaseWithAddressPortUserPwd`，内部按协议类型选 bridge 参数，避免三份复制。
- **Q10 - 串口枚举留 C#**：`SerialPorts`（编辑器下拉）继续用 `System.IO.Ports.SerialPort.GetPortNames()`，包引用保留。纯系统注册表查询，不值得为它写跨 FFI 列表函数。

### Runner 与迁移

- **Q5 - 反序列化兜底**：删除 `PuttyRunner`/`KittyRunner` 类型后，老配置的 runner 引用需兜底到对应协议的 Rust 默认 runner，保证老用户会话可打开。
- **Q7 - 命名 `RustTelnetRunner`/`RustSerialRunner`**：`Name = "Rust Telnet (Built-in)"`/`"Rust Serial (Built-in)"`，与 `RustSshRunner` 一致。不沿用 "Built-in PuTTY" 名（误导）。
- **Q11 - 保留 StartupAutoCommand**：Telnet/Serial 都保留，Rust 会话连接后按行注入；Serial 表单的输入框取消注释对齐 Telnet。

## Considered Options

各决策的备选方案与选定理由见上述条目内联说明（范围/性能/一致性维度）。关键备选未选者：

- **Q2 备选 B/C（保留 net6/net48）**：`#if !RUST_SSH` 下 PuTTY 仍编译。弃：CI 已不发布，微软 EOL，`#if` 膨胀。
- **Q4 备选 C（C# System.IO.Ports）**：性能与 serialport 等效（同一 Win32 API 底层），弃：违背 Rust 化诉求；且 `GetPortNames` 枚举反正要留 C#，包引用删不掉。
- **Q12 备选 B（全部 UTF-8）**：Serial 对非 UTF-8 设备直接乱码，与体验升级背道而驰。
- **Q12 备选 C（三协议都可配置）**：SSH/Telnet 加编码配置是低频需求，过度设计。

## Consequences

- Telnet/Serial 终端仅 net9.0-windows 构建可用（net48/net6 配置已废弃，同步清理）。
- 终端协议栈统一：SSH/Telnet/Serial 共享 `TermSession` trait + `RustTerminalHost` + `ITerminalConnection`。
- 随包体积减少约 2MB（putty.exe + kitty_portable.exe + PuttyThemes.json）。
- 新增 `RustTelnetRunner`/`RustSerialRunner` 两个 runner，各协议默认注册之。
- Telnet 获得 Rust 侧最小 IAC 协商（比 PuTTY 完整实现略弱，但覆盖主流设备）；Serial 获得可配置 Code Page（PuTTY 时代无此能力，属增强）。
- 老配置数据：`SelectedRunnerName` 引用 PuTTY/KiTTY 的会话经兜底自动切到 Rust runner；协议模型 JSON 里的 PuTTY 专属字段被 Newtonsoft 静默忽略。
- `RustTerminalHost` 改名后，`Ui/Ui.csproj` 的 `<Page Update>` 项同步更新；`RustSshHost` 相关资源随之调整。

## 关联

- 前置：ADR-0001（SSH Rust 化）确立了 cdylib + FFI 桥模式，本 ADR 将其推广到 Telnet/Serial。
- 配套：`ssh-rust/src/session.rs` 已引入 `TermSession` trait 与 `Box<dyn TermSession>` 注册表（半成品，待完成）。
- 清理清单：`Ui/Utils/PuTTY/`、`Ui/Resources/PuTTY/`、`Ui/Resources/KiTTY/`、`Ui/View/Settings/ProtocolConfig/PuttyRunnerSettings*`、`KittyRunnerSettings*`、语言 CSV 中 PuTTY/KiTTY 词条。
