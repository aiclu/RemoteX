# Fluent 首页紧凑优化

日期：2026-09-29。以下记录基于 2.0.2 的发布前本地验证；该实现阶段仅保留本地代码和 Debug 测试产物，并保留此前的启动器设置滚动修复。后续经用户授权，两项改动一并纳入 2.0.3 小版本，详见 [发布说明](../releases/2.0.3.md)。

## 实现

- 首页主内容左右统一 16 DIP，侧栏首项与搜索顶部对齐。工具栏按按钮的实际测量宽度、240 DIP 搜索空间及 8 DIP 间隔决定单行或换行；不移动、重建搜索框，不改搜索绑定、输入法或筛选逻辑。
- 局部轻量菜单按钮带下拉箭头，“添加”为强调色按钮。极窄窗口允许操作继续换行；很长的按钮文本显示省略号及完整提示。
- 内部 `FluentHomeRowGrid` 统一标题与连接行的六列：24 / 28 / 32 / * / 60 / 64 DIP。标题拖动手柄与连接图标共列，连接行保留空的箭头列。长文案/大字体可扩大操作列，极窄时限制在卡片内，保留名称空间；源标题的状态、添加换至第二行。
- 保留独立圆角数据源卡片，统一内边距；标题、连接行最小 44 DIP。仅 Fluent 列表移除旧的底部 30 DIP 留白。轻量“连接”始终可见。
- “全部连接”仅在没有标签筛选且未管理标签时激活；标签使用淡底色和 3 DIP 指示条。鼠标悬停与键盘焦点分别表现，不使用 ListBox 的陈旧选中状态绘制第二层背景。
- 首页列表显式使用没有额外内边距的局部模板。真实整页检测发现系统 ListBox 模板的内部留白在最小窗口造成约 1.6 DIP 的右边缘偏差；修正模板后对齐断言通过。仍然只有一个 ScrollViewer 和虚拟化 ItemsPresenter。
- 新增选择背景/前景资源支持浅色、深色及高对比度系统颜色；不覆盖全局旧按钮样式。经典模式、卡片和树形视图内部、业务模型及各命令保持原实现。

## 自动验证

```powershell
dotnet test Tests/Tests.csproj -c Debug --no-restore -v:q -clp:ErrorsOnly
dotnet build Ui/Ui.csproj -c Debug --no-restore -p:OutDir=D:/Projects/RemoteX/Ui/bin/FluentDialogsTest/ -v:q -clp:ErrorsOnly
dotnet run --project tools/FluentPreview/Render.csproj -c Debug --no-restore -- --home
dotnet run --project tools/FluentPreview/Render.csproj -c Debug --no-build --no-restore -- --source-groups
dotnet run --project tools/FluentPreview/Render.csproj -c Debug --no-build --no-restore -- --tag-management
dotnet run --project tools/FluentPreview/Render.csproj -c Debug --no-build --no-restore -- --launcher-settings
dotnet build Ui/Ui.csproj -c Debug --no-restore -p:OutDir=D:/Projects/RemoteX/Ui/bin/FluentHomePolishTest/ -v:q -clp:ErrorsOnly
```

- 完整 C# 测试：**115 通过，0 失败，0 跳过**。新增六项首页布局测试，覆盖测量换行、搜索实例/光标保留、长文案/大字体、列对齐和紧凑布局恢复、全部连接选中条件、高对比度颜色。
- `--home` 使用真实编译的 `FluentWorkspace`、分组模板、`FluentConnectionRow` 和实际侧栏/工具栏模板，数据和命令均为内存模拟，不以简化文本行替代连接行。
- 明暗主题的 300 / 600 / 800 / 1120 DIP 窗口检查通过：工具栏与卡片左右边缘、侧栏首项与搜索顶部、分组与行的复选框/名称起点/操作右边缘，误差均不超过 **1 DIP**。
- 完整首页在上述宽度间重复缩放，每种主题 13 次：无布局循环，搜索实例、中文字符串及光标位置保持。单独工具栏还有 36 次自动换行回归。
- 覆盖单组移除/重新分组、未分组、双源交替展开、全部折叠、空组、只读、断开、长名称、筛选后分组变化、13 / 20 / 24 DIP 字体，以及长本地化按钮。宽窗口的长连接按钮不因小数 DPI 文本测量提前省略；极窄窗口保持按钮在卡片内。
- 真实模板的折叠绑定、刷新/添加命令参数、拖动手柄事件和只读权限通过。完整测试包含原有全选/部分选中、折叠持久化、标签固定/筛选、经典模板恢复及菜单标记测试。渲染工具调用连接按钮时仅执行计数用模拟命令。
- **1200 条真实连接行模板**的模拟列表，在不同滚动偏移处实际生成 **22 / 56 / 35 / 22** 个容器；没有一次生成全部行。原卡片分组回归仍为 15–21 个容器。
- 管理标签/返回反复切换、启动器设置复选框/说明/空白处滚轮、快速连接区域可达等既有回归通过。
- Debug 构建通过（120 条现有警告，0 错误）。渲染工具成功加载全部 14 种语言资源；渲染工具构建仍有既有的 9.0/10.0 引用版本冲突警告，未在 UI 调整中升级依赖。
- 独立目录 `ssh_rust.dll` 为 5,443,072 字节，通过 `NativeLibrary.Load` 加载及 `sr_set_log_callback` 导出检查，随后立即释放；未启动 SSH 或真实连接。

## 产物与人工验收

- 本地测试程序：`Ui/bin/FluentHomePolishTest/RemoteX.exe`（框架依赖的 Debug 构建；实现阶段为 2.0.2，后续 2.0.3 提交前复验已刷新为 2.0.3）。
- 完整首页模拟截图：`Ui/bin/FluentDialogsTest/screenshots/Home-{Light|Dark}-{1120|800|600|300}-{100|150|200}.png`。
- 其他场景：同目录 `Home-Collapsed-*`、`Home-Ungrouped-*`、`Home-States-*`、`Home-LongLabels-*`；本轮首页共 54 张输出。图片使用模拟名称与 `example.invalid` 地址，不包含用户连接或凭据。
- 100% / 150% / 200% 是位图渲染输出比例，不代表已经完成对应显示器 DPI 的实机验证。
- 待实机验收：跨显示器 DPI 切换、鼠标拖放排序、真实窗口下的 Tab 焦点/Ctrl+F/中文输入法、系统主题动态切换及系统高对比度。自动检查已覆盖资源更新和搜索实例/光标，但不替代这些交互验收。
- 本地实现与验证阶段未执行真实远程连接、数据库操作、配置修改或自动升级，也未改版本号、提交、推送或发布；后续提交和发布由单独的用户授权触发。
