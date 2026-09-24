# Fluent 搜索框与协议选择栏验证

日期：2026-09-24。版本保持 1.0.16；仅本地修改和测试构建。

## 实现

- 搜索框保留原 TextBox、筛选绑定和 Ctrl+F 路径；加入矢量图标、居中占位文案和独立快捷键提示。紧凑布局隐藏快捷键提示，完整快捷键仍在工具提示中。
- 协议栏仅在 Fluent 激活时使用动态正文颜色、36 DIP 最小高度、选中下划线、悬停和焦点边框。保留经典模板、协议顺序、帮助链接及原选择绑定。
- 编辑器头部使用 Fluent 自适应行高，经典仍预留 20 DIP；正文与头部分行，底部保存/取消仍固定。
- 搜索文案已加入全部语言 XAML 和 glossary CSV。

## 自动验证

- `dotnet build Ui/Ui.csproj -c Debug --no-restore -p:OutDir=D:/Projects/RemoteX/Ui/bin/FluentDialogsTest/ -v:q`：通过，有项目现有编译及包源警告。
- `dotnet test tools/FluentPreviewTests/Tests.csproj --no-restore -v:q`：44 项通过。新增测试覆盖 13/20 DIP 字体下占位居中、空值/输入、紧凑提示折叠，协议动态颜色替换、选中下划线和经典模板恢复。
- `dotnet run --project tools/FluentPreview/Render.csproj --no-restore`：通过。编辑器使用九种协议的模拟列表，断言正文未被头部遮挡；搜索覆盖空值、输入和禁用。
- 明暗主题、1000/600 DIP 编辑器、640/280 DIP 搜索、18/20 DIP 大字体，输出 100%/150%/200% 位图。截图位于 `Ui/bin/FluentDialogsTest/screenshots`。这些是隔离渲染，不等同于真实显示器 DPI 测试。
- 测试目录包含 `ssh_rust.dll`（5,443,072 字节）。
- 完整 `dotnet test Tests/Tests.csproj --no-restore -v:q` 未运行成功：NETSDK1005，现有资产文件缺少 `net6.0-windows10.0.17763.0` 目标。

## 启动语言资源回归修复（同日）

- 修正 de-de、it-it、pl-pl、pt-br、pt-pt 中重复的 `FluentSearchConnections` 键。重复资源会导致启动加载失败，并触发 Debug 断言；此前只渲染中文未覆盖此问题。
- 内置语言 URI 明确指向 RemoteX 程序集，不依赖测试宿主的默认资源程序集。
- 增加全部 14 种语言的源码键唯一性、编译资源加载和关键字符串检查；专项测试更新为 45 项通过。
- 隔离渲染工具在渲染前调用真实 `LanguageService` 构造函数，检查所有注册语言均已加载；不启动真实应用或访问用户连接数据库。

## 待实机验证

### 搜索光标回跳修复

- 经典和 Fluent 搜索框共用光标位置；隐藏文本框同步文本时的 SelectionChanged 曾将共享位置回写为 0。
- 光标附加属性现在只接受具有键盘焦点的文本框回写，使用 SetCurrentValue 保留绑定；模型仍可主动设置光标位置。
- 新增回归测试修复前复现 `Expected: 3, Actual: 0`，修复后通过，并验证程序定位和绑定保留。Debug 构建通过，专项测试更新为 46 项通过。真实输入法、鼠标选择及连续键入仍需实机验证。

- Ctrl+F、中文输入法组合输入、实际筛选结果、Tab/方向键与鼠标切换协议、帮助链接、保存/取消。
- 系统主题动态切换、高对比度、真实 100%/150%/200% DPI 及跨显示器移动。
- 未执行真实连接、升级、提交或发布。
