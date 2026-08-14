# ADR-0007: FTP/SFTP 迁移到 Rust 核心 + 全量 NuGet 升级

## 背景

1. **安全漏洞**：`SSH.NET 2023.0.0`（High，GHSA-q939-rpr3-3284）与 `System.Drawing.Common 4.7.0`（Critical，GHSA-rxg9-xrhp-64gj，传递依赖）存在已知漏洞。
2. **依赖过时**：14 个 NuGet 包版本偏旧，含 3 个大版本（MySql.Data 8→26、Npgsql 9→10、Sentry 4→6）。
3. **架构一致性**：SSH 终端已 Rust 化（ADR-0001），但 FTP（FluentFTP）与 SFTP（SSH.NET）仍是 .NET 依赖。

## 决策

### Q1-A: FTP 和 SFTP 一起迁到 Rust

- FTP 用 `suppaftp`（同步 API + native-tls FTPS），SFTP 用 `russh-sftp`（复用现有 SSH 认证）。
- 彻底移除 `FluentFTP` 与 `SSH.NET` 两个依赖。
- 移除后漏洞清单为 0（`dotnet list package --vulnerable` 验证）。

### Q2-A: 进程内 FFI，非外部进程

- 复用 SSH 终端的 cdylib FFI 架构（`ssh_rust.dll`）。
- `RustFtpBridge`（C#）封装 session handle + JSON 列表 + 进度回调。

### Q3-A: C 函数指针进度回调

- Rust 侧每 chunk 调 `extern "C" fn(u64)`。
- C# 侧 `Marshal.GetFunctionPointerForDelegate` + 托管委托保持存活。
- 取消用 pinned byte flag（与 Rust `AtomicBool` 布局兼容，1 字节 0/1）。

### Q6-A: Rust 直接读写本地磁盘

- FFI 只传路径 + 回调，Rust 用 `std::fs`（FTP）/ `tokio::fs`（SFTP）直接读写。
- 无跨 FFI 字节流拷贝。

### Q7-A: JSON 返回目录列表

- Rust 侧 `serde_json::to_string(Vec<RemoteItemDto>)`，C# 侧 `JsonConvert.DeserializeObject`。
- DTO：name / full_name / is_directory / is_symlink / size / last_update。

### Q9-A: 不升级 russh 主版本

- 关键事实：`russh-sftp 2.4.0` 的 "2" 是**它自己的版本号**，与 russh 主版本无关；它只依赖 `dashmap/bytes/chrono` 等，接收任何 `AsyncRead+AsyncWrite` 流。
- `russh 0.50` 的 `Channel::into_stream()` 提供 `ChannelStream`（AsyncRead+AsyncWrite），直接喂给 `SftpSession::new`。
- **SSH 终端代码零改动**。

### Q10-B: FTP 用同步 API

- suppaftp 同步 `FtpStream`/`NativeTlsFtpStream`（deprecated feature），独立于 tokio。
- FTPS：`into_secure(NativeTlsConnector, host)`，TLS 1.2 + `danger_accept_invalid_certs(true)`（镜像旧 FluentFTP 行为）。

### NuGet 升级策略（Q1-C→用户改"全部升级"）

- 除 FluentFTP/SSH.NET（被迁移替代而**删除**）外，**12 个包全部升级到最新**：
  - AvalonEdit 6.3.0.90→6.3.1.120
  - Dapper 2.1.66→2.1.79
  - JsonKnownTypes 0.5.4→0.7.0
  - MySql.Data 8.0.30→26.7.0
  - Newtonsoft.Json 13.0.1→13.0.4
  - Npgsql 9.0.2→10.0.3
  - NUlid 1.7.1→1.7.3
  - Sentry 4.13.0→6.8.0
  - Stylet 1.3.6→1.3.7
  - System.Data.SQLite.Core 1.0.117→1.0.119
  - System.IO.Ports 8.0.0→10.0.11
  - VirtualizingWrapPanel 2.0.10→2.5.4
- 大版本（MySql.Data/Npgsql/Sentry）升级后编译通过，无 breaking change。

## 架构

```
C# (TransmitterFtp/TransmitterSFtp)
  └── RustFtpBridge (session handle, JSON, progress)
        └── FtpRustNative (P/Invoke → ssh_rust.dll)
              ├── sr_ftp_*  → suppaftp (FtpStream, sync + native-tls FTPS)
              └── sr_sftp_* → russh-sftp (SftpSession over ChannelStream)
```

## Consequences

- 漏洞清单归零（SSH.NET High + System.Drawing.Common Critical 均清除）。
- 移除 FluentFTP 51.0.0 + SSH.NET 2023.0.0 两个依赖。
- 新增 Rust 依赖：suppaftp 10.0.1、russh-sftp 2.4.0、native-tls、serde/serde_json、tokio fs。
- 文件传输（FTP/SFTP）与 SSH 终端共享同一个 `ssh_rust.dll` FFI 面。
- SFTP 走独立 SSH 连接（russh 0.50，TOFU 策略），不共享终端会话。
- 进度回调与取消（pinned byte flag）跨 FFI 实时生效。

## 关联

- 前置：ADR-0001（SSH Rust 化）、ADR-0006（Telnet/Serial Rust 化）。
- 新文件：`Ui/Service/RustFtp/`（FtpRustNative.cs、RustFtpBridge.cs）、`ssh-rust/src/ftp.rs`、`ssh-rust/src/sftp.rs`。
