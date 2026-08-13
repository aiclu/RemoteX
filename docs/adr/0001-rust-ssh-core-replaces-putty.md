# ADR-0001: 用进程内 Rust SSH 核心替换 PuTTY

SSH 会话原本通过 `IntegrateHost` 拉起外部 putty.exe 并 `SetParent` 嵌入其原生窗口；该方案引入了外部进程依赖、无法自由缩放/全屏，且 putty 的用户体验无法由 1Remote 控制。我们决定改用进程内 Rust（russh）SSH 核心，经 cdylib + FFI 桥与 WPF 终端控件通信。

原因：彻底摆脱 putty 的外部进程与配置依赖，获得统一的进程内渲染、可自由缩放/全屏的终端；同时为未来跨平台核心（Rust 层可移植）铺路。Rust 的收益是技术验证与跨平台路径，而非 SSH 吞吐性能。

**Status**: accepted

**Considered Options**:
- **嵌入 putty.exe 窗口（现状）**：零开发但无法摆脱 putty，UX 受制于外部程序。
- **C# SSH.NET 重写**：工作量小一个数量级，但不满足"用 Rust"的核心诉求，无法为跨平台核心铺路。
- **进程内 Rust cdylib + FFI（选定）**：满足 Rust 诉求，进程内共享免 IPC 开销；代价是 Rust 崩溃会波及宿主（以 `catch_unwind` + 错误码缓解）。

**Consequences**:
- SSH 终端仅 net9.0-windows 构建可用；net48/net6 不编译相关代码。
- 新增第三种 host 形态（既非 Native 协议主机，也非 Integrate 外部 exe 窗口）。
- SFTP 本期继续用 Renci.SSH.NET，不迁移。
- 私有密钥仅支持 OpenSSH/PEM 格式，不支持 .ppk 与 passphrase 私钥。
