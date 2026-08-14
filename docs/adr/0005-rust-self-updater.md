# ADR-0005: Rust self-updater（应用内自更新）

更新机制原为"跳转浏览器到 GitHub Release 页手动下载"。改为**应用内自更新**：由独立 Rust 进程 `updater.exe` 完成下载、校验、解压、替换主程序、重启，WPF 主程序只负责检测版本、启动 updater 并显示进度。

**为什么 Rust 独立进程**：主程序 exe 运行时被系统锁定，无法直接覆盖，替换必须在主进程退出后进行——这要求 updater 是**独立进程**而非 ssh-rust 那样的进程内 cdylib。Rust 提供 `reqwest`（流式下载+进度）、`zip`（解压）、`sha2`（校验），且与 ADR-0001 的 Rust 技术路线一致。

**Status**: accepted

**Considered Options**:
- **保持跳浏览器**：零开发成本，但体验差。
- **AutoUpdater.NET 等第三方库**：成熟，但引入 C# 依赖，且对 GitHub Releases 资产支持需适配。
- **Rust 独立进程 updater.exe（选定）**：与项目 Rust 方向一致；独立进程天然规避文件占用；stdout JSON 协议让 C# 侧解析进度。

**协议**（updater stdout，逐行 JSON）:
- `{"type":"stage","stage":"download|verify|extract|wait-exit|swap|swapped"}`
- `{"type":"progress","pct":42.3}` 下载百分比
- `{"type":"error","message":"..."}` 致命错误，退出码 1
- `{"type":"done"}` 成功

**Consequences**:
- updater.exe 随发布 zip 一起分发（CI 在 publish 后复制进产物目录）。
- 构建需要 MSVC 链接器 + Windows SDK——本机无 SDK 无法本地构建，由 CI（GitHub Actions 自带）构建。
- 下载用 HTTPS（GitHub Releases 资产）+ zip 内部完整性；sha256 校验作为后续增强（CI 发布 `*.sha256` 资产后启用）。
- WPF 侧自更新 UI（进度条、重启提示）在后续迭代接入 `AboutPageViewModel.CmdUpdate`。
