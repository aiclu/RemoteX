# RemoteX 领域术语表

本文档收录 RemoteX 中与协议会话、runner 相关的领域术语，供设计与实现时统一用语。与 ADR 配套维护。

## 协议与会话

| 术语 | 英文 | 含义 |
|---|---|---|
| 协议 | Protocol | 可连接的远程会话类型：RDP、SSH、Telnet、VNC、FTP、SFTP、Serial、LocalApp 等。`Ui/Model/Protocol/` 下各具体类。 |
| 会话 | Session | 一次运行中的远程连接。在 Rust 核心中抽象为 `TermSession` trait。 |
| 终端会话 | TermSession | Rust 侧协议无关的会话接口，五方法：`write`/`poll_read`/`is_closed`/`error_message`/`resize`。SSH/Telnet/Serial 均实现之。 |
| 桥接层 | Bridge | C# 侧连接 Rust cdylib 的适配层（`SshRustBridge`）。管理生命周期 + 后台 poll 线程 + 事件回调。 |
| 会话注册表 | Session Registry | Rust 侧 `i64` handle → `Box<dyn TermSession>` 的全局表，FFI 层通过 handle 分发调用。 |
| 串口参数 | Serial Params | Serial 协议的 COMPort、BaudRate、DataBits、Parity、StopBits、FlowControl。C# 侧映射为枚举 int 传入 Rust。 |

## Runner

| 术语 | 英文 | 含义 |
|---|---|---|
| Runner | Runner | 一个协议可用的启动器实现。`Ui/Model/ProtocolRunner/`。 |
| 内置 Runner | Built-in Runner | 进程内实现的 runner，不依赖外部 exe：`RustSshRunner`、`RustTelnetRunner`、`RustSerialRunner`。 |
| 外部 Runner | External Runner | 用户自定义 exe 的 runner，经 `IntegrateHost` 嵌入外部进程窗口。 |
| 宿主 | Host | 承载一次会话的 WPF 控件（`HostBase` 子类）。终端协议统一用 `RustTerminalHost`。 |
| 默认 Runner | Default Runner | 协议首次注册时默认启用的 runner。SSH/Telnet/Serial 均默认 Rust。 |

## 编码

| 术语 | 英文 | 含义 |
|---|---|---|
| Code Page | Encoding CodePage | 终端字节流↔文本的编码数字（如 65001=UTF-8、936=GBK）。存于 `Telnet`/`Serial` 模型的 `EncodingCodePage`，C# 桥接层用 `Encoding.GetEncoding(cp)` 转换。 |

## RDP 与 mstscax

| 术语 | 英文 | 含义 |
|---|---|---|
| RDP ActiveX 控件 | mstscax | Windows 系统自带的远程桌面 ActiveX 控件（`%windir%\system32\mstscax.dll`），RDP 会话的渲染宿主。非第三方库，随系统更新升级。 |
| 互操作程序集 | Interop Assembly | `lib/MSTSCLib.dll` 与 `lib/AxMSTSCLib.dll`，是 mstscax 的 .NET COM 互操作包装（编译期 `<Reference>`，运行时用系统控件）。由 `lib/BuildAxMSTSCLib.ps1`（aximp + SendKeys 补丁）重新生成。 |
| 会话宿主 | AxMsRdpClient10Host | RDP 的 WPF host（`AxMSTSCLib.AxMsRdpClient10NotSafeForScripting` 子类，自定义 `WndProc` 修复 WM_GETOBJECT 崩溃/焦点问题），大量使用 `MSTSCLib` 的 COM 接口。 |
| 高级设置透传 | AdvancedSettings passthrough | `RDP.cs` 通过反射遍历 `IMsRdpClientAdvancedSettings8` 的可写属性，将模型里自定义的键值对透传给控件。 |

### mstscax 接口版本链（官方 Requirements）

接口连续递进（每个继承前一个）：`IMsTscAx → IMsRdpClient → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 10`。

| 接口 | 最低客户端 Windows | 最低服务器 | 关键特性 |
|---|---|---|---|
| IMsRdpClient7 | Win7 SP1 + KB2574819 | Server 2008 R2 | RDP 8.0 |
| IMsRdpClient8 | Win8 | Server 2012 | RDP 8.1 |
| IMsRdpClient9 | Win8.1 | Server 2012 R2 | `SyncSessionDisplaySettings` 等 |
| IMsRdpClient10 | **Win10（任意版本）** | Server 2016 | AVC-444、UDP 传输、DPI 缩放属性 |
| Win11 | 仍为 Client10（无 Client11） | — | 系统组件升级新增扩展接口（如 `IMsRdpCameraRedirConfig`），主接口不变 |

注意：AVC-444 属性接口（`IMsRdpClientAdvancedSettings8.H264AVC444EncodersPriority`）在 Win8 就有，但编解码真正生效需 Win10 服务端；UDP 传输在 `IMsRdpClientTransportSettings2/3`（Win8/8.1）已存在。Win10 1903 仅是 mstscax build 升级，并非接口版本。

### 项目现状

- Host 基类与高级设置透传全部用 **Client10**（`AxMsRdpClient10Host` + `AxMSTSCLib.AxMsRdpClient10NotSafeForScripting` + `AxMsRdpClient10` 反射），运行时在 Win10/11 上使用最新接口。
- 决策：`lib/` 保持现状，不刷新互操作集（aximp 已废弃，风险大于收益）；不换 ActiveX 方案（微软专有协议，FreeRDP 等实现不完整）；不引入 `CODELAB.MSBUILD.MSTSCLIB` NuGet（第三方、更新不活跃）。

## 清理语境（历史遗留，已废弃）

| 术语 | 说明 |
|---|---|
| PuTTY | 外部终端客户端，曾是 SSH/Telnet/Serial 的 runner 后端。ADR-0001/0006 后全部移除。 |
| KiTTY | PuTTY 的第三方增强分支（自动重连/背景图/ZModem 等）。已 `[Obsolete]`，随 ADR-0006 删除。 |
| IPuttyConnectable | 协议模型的 PuTTY 配置接口，上游标注 `TODO: delete after 2026-01-01`，随 ADR-0006 删除。 |
