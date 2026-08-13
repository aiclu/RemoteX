# ADR-0004: UI 现代化路线（三轴分阶段 + Fluent2 + 样式归属）

UI 升级需求含三个独立目标轴：**性能优化、内存占用、视觉现代化**。我们决定留在 WPF 技术栈，按 **性能 → 内存 → 视觉** 分阶段推进，以基线先行（DEV-only 插桩）量化验收；视觉方向对齐 **Windows 11 Fluent 2**，深色为默认、保留浅色与现有 9 套主题机制兼容；视觉基础样式**直接改 `Shawn.Utils.WpfResources` 库内源码**（Ui 以 ProjectReference 引用，该库不发布 NuGet 包，无外部影响）。

原因：换框架（WinUI 3 / Avalonia）意味着 RDP ActiveX、VNC、终端 HwndHost、Dragablz 跨窗拖拽全部重写，风险与工作量不成比例；视觉样式源码本就在仓库内，无需 fork/覆盖层。

**Status**: accepted

**Considered Options**:
- **留在 WPF（选定）**：深度绑定 Stylet/Dragablz/HwndHost/MSIX Store 发布，换栈成本过高。
- **样式覆盖层**（Ui 内 override 外部库）：Fluent2 需动控件模板，覆盖层做不干净且性能更差。
- **换 UI 框架**（WinUI 3 / Avalonia）：全面重写，否定。

**Consequences**:
- **性能阶段**模板取舍：去掉卡片动态字号 MultiBinding、`SharedSizeGroup` 列测量、隐藏项 `Hidden`→`Collapsed`；保留旋转协议名与悬停动效（视觉特色）。
- **Mica/Acrylic 仅限非会话区**：RDP/SSH 终端区是 HwndHost/WindowsFormsHost，Airspace 限制下不可用。
- **图标策略**：服务器品牌图标（141 PNG）保留，仅 UI 操作图标换 Segoe Fluent glyph。
- 改动拆分为独立 PR（插桩基线 → 加载异步化 → 模板减重 → 视觉批次1/2），主分支渐进合并，CI 每推必验；数据/配置零破坏，可随时回退。
