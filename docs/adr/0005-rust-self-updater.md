# ADR-0005: Rust self-updater（应用内自更新）

更新机制原为"跳转浏览器到 GitHub Release 页手动下载"。改为**应用内自更新**：由独立 Rust 进程 `updater.exe` 完成下载、校验、解压、替换主程序、重启，WPF 主程序只负责检测版本、启动 updater 并显示进度。

**为什么 Rust 独立进程**：主程序 exe 运行时被系统锁定，无法直接覆盖，替换必须在主进程退出后进行——这要求 updater 是**独立进程**而非 ssh-rust 那样的进程内 cdylib。Rust 提供 `reqwest`（流式下载+进度）、`zip`（解压）、`sha2`（校验），且与 ADR-0001 的 Rust 技术路线一致。

**Status**: accepted

**Considered Options**:
- **保持跳浏览器**：零开发成本，但体验差。
- **AutoUpdater.NET 等第三方库**：成熟，但引入 C# 依赖，且对 GitHub Releases 资产支持需适配。
- **Rust 独立进程 updater.exe（选定）**：与项目 Rust 方向一致；独立进程天然规避文件占用；stdout JSON 协议让 C# 侧解析进度。

**协议**（updater stdout，逐行 JSON）:
- `{"type":"stage","stage":"download|verify|extract|ready-to-swap|wait-exit|swap|swapped"}`
- `{"type":"progress","pct":42.3}` 下载百分比
- `{"type":"error","message":"..."}` 致命错误，退出码 1
- `{"type":"done"}` 成功

## 更新交接与事务约束

WPF 侧从一次 GitHub Releases API 响应同时取得目标版本、self-contained 资产地址、发布页地址和可选 SHA-256 摘要，并把这组元数据作为 `SelfUpdatePackage` 传给更新器。更新器在发出 `ready-to-swap` 前必须完成下载、摘要校验（若发布元数据提供）、安全解压、`RemoteX.exe` 存在性检查和目标目录写入预检；因此 `ready-to-swap` 是唯一允许应用调用 `App.Close()` 的交接点。

更新器参数保留旧的 URL/程序路径位置参数，并新增 `--pid`、`--target-version`、`--state-file` 和 `--restart`。`--pid` 优先于按进程名查找，同时继续兼容 `REMOTEX_TARGET_PID`。不可重定向 stdout 的 UAC `runas` 路径通过状态文件等待 `ready-to-swap` 或失败状态；用户取消 UAC 时旧版本保持运行。

替换是可回滚事务：更新器先在目标目录创建带 PID 的备份目录，记录 `backupPath`，再复制发布目录内容和主程序。发布包内含新的 `updater.exe` 时，先从暂存目录启动新更新器，旧更新器退出后由暂存副本替换自身；只有没有新更新器的旧包才走兼容路径并保留旧更新器。任一替换步骤失败都尝试恢复已备份文件、记录 `rolled-back` 或 `rollback-failed`，写入 `%TEMP%/RemoteX-updater.log`，并尽力重启旧程序。

`.locality/update-state.json` 是启动校验的交接凭据，而不是成功标志。启动时将待更新目标版本与实际加载版本比较：实际版本达到目标版本才清理状态和备份；仍运行旧版本时保留状态、日志路径和重试/手动下载入口。状态文件采用临时文件加替换的写入方式，读取时兼容残留 `.tmp`，避免更新进程中断后出现“已成功但实际仍是旧版”的重复循环。

**Consequences**:
- updater.exe 随发布 zip 一起分发（CI 在 publish 后复制进产物目录）。
- 构建需要 MSVC 链接器 + Windows SDK——本机无 SDK 无法本地构建，由 CI（GitHub Actions 自带）构建。
- 下载用 HTTPS（GitHub Releases 资产）+ zip 内部完整性；sha256 校验作为后续增强（CI 发布 `*.sha256` 资产后启用）。
- WPF 侧自更新 UI（阶段进度、UAC 取消、回滚/重试和手动下载提示）由 `AboutPageViewModel.CmdUpdate` 驱动；Store 构建仍交由 Microsoft Store 更新。
