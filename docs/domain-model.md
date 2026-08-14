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

## 清理语境（历史遗留，已废弃）

| 术语 | 说明 |
|---|---|
| PuTTY | 外部终端客户端，曾是 SSH/Telnet/Serial 的 runner 后端。ADR-0001/0006 后全部移除。 |
| KiTTY | PuTTY 的第三方增强分支（自动重连/背景图/ZModem 等）。已 `[Obsolete]`，随 ADR-0006 删除。 |
| IPuttyConnectable | 协议模型的 PuTTY 配置接口，上游标注 `TODO: delete after 2026-01-01`，随 ADR-0006 删除。 |
