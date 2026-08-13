# ADR-0002: 终端渲染层选型 Microsoft.Terminal.WPF（经 EasyWindowsTerminalControl）

SSH 终端需要一个进程内 WPF 终端控件来渲染远程字节流。我们选用微软 Windows Terminal 团队的渲染控件（`Microsoft.Terminal.Wpf.TerminalControl`）作为渲染层，通过 `EasyWindowsTerminalControl`（NuGet `1.0.38`，MIT）获得，而非自绘或其它第三方控件。

原因：它与 Windows Terminal 共用渲染技术栈（DirectX/Atlas 引擎），ANSI/VT、24 位真彩、光标、滚动、字体配色能力最完整，避免自研渲染层（工作量极大）。选型以阶段0 冒烟验证为 gate，已通过。

**Status**: accepted

**选型验证发现（阶段0 gate）**：
- 微软官方 `Microsoft.Terminal.Wpf` **无标准 NuGet 包**，仅通过 beta 预发布包（`CI.Microsoft.Terminal.Wpf`）分发，底层 API 未来可能变化。
- 通过 `EasyWindowsTerminalControl` 封装获得，依赖 `CI.Microsoft.Terminal.Wpf` + `Microsoft.Windows.Console.ConPTY`。
- **字节流驱动架构可行**：`TerminalControl.Connection`（`ITerminalConnection`）可注入自定义连接实现。接口契约：`WriteInput(string)` 接收输出字节、`TerminalOutput` 事件输出用户输入、`Start()/Resize()/Close()`。与"Rust SSH 字节流 → 渲染"完全契合。
- **UTF-8 文本接口约束**：`WriteInput(string)` 是 UTF-16 文本接口，第一期仅支持 UTF-8（非二进制内容），二进制输出（如 `cat` 二进制文件）第一期不支持。
- **Airspace 约束**：控件用 HwndHost 承载，普通 WPF 元素无法覆盖在终端上方（与现有 VncHost/IntegrateHost 的 WindowsFormsHost 类似，1Remote 已有处理经验）。

**Considered Options**:
- **自绘 WriteableBitmap 终端**：性能好但 ANSI/滚动/光标需全部自研，工作量高一个数量级，放弃。
- **微软官方 `Microsoft.Terminal.Wpf` 直接引用**：无标准 NuGet 包，需源码构建，不现实。
- **`EasyWindowsTerminalControl`（选定）**：封装微软官方渲染控件，现成 NuGet，支持 net9（回退 net8.0-windows7.0）。

**Consequences**:
- 引入 `EasyWindowsTerminalControl` 及 `CI.Microsoft.Terminal.Wpf`、`Microsoft.Windows.Console.ConPTY` 依赖，需在 Ui.csproj 条件 ItemGroup 显式引用。
- 因 net48/net6 不支持该控件（及 LibraryImport），Rust-SSH 终端仅在 net9 构建可用。
- 实现一个 `ITerminalConnection` 自定义连接，作为 Rust-SSH 与 `TerminalControl` 之间的字节流管道。
- 字体/配色第一期做基础映射（默认 Consolas/Cascadia + 深色主题），编码页仅 UTF-8。
