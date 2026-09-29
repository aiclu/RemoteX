# 启动器设置页滚动修复

日期：2026-09-29。以下为基于 2.0.2 的发布前本地验证记录，当时不改版本号、不提交、不推送、不发布。后续经用户授权，本修复与首页优化一并纳入 [2.0.3](../releases/2.0.3.md)。

## 根因和修复

“设置 → 启动器”外层已经有页面 ScrollViewer，但关键词匹配器使用带独立 ScrollViewer 的 ListView。匹配器在纵向 StackPanel 内按全部内容展开，内层没有可滚动距离，却仍处理 MouseWheel，阻止事件继续到外层。因此鼠标位于匹配器复选框或说明上时，页面看似无法滚动。

- 将没有选择业务的匹配器 ListView 改为非滚动 ItemsControl，整页只由外层容器负责滚动；无需捕获并重新派发鼠标事件。
- 保留原 ItemsSource、选项模板、复选框 Enabled/IsEditable 绑定、快捷键控件和设置保存逻辑。经典与 Fluent 共用同一修复。
- 外层显式使用纵向自动滚动条，横向保持禁用。不修改真正的启动器搜索窗口或结果列表。

## 自动验证

```powershell
dotnet test Tests/Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~LauncherSettingsScrollTests
dotnet test Tests/Tests.csproj -c Debug --no-restore -v:q -clp:ErrorsOnly
dotnet build Ui/Ui.csproj -c Debug --no-restore -p:OutDir=D:/Projects/RemoteX/Ui/bin/FluentDialogsTest/ -v:q -clp:ErrorsOnly
dotnet run --project tools/FluentPreview/Render.csproj --no-restore -- --launcher-settings
dotnet build Ui/Ui.csproj -c Debug --no-restore -p:OutDir=D:/Projects/RemoteX/Ui/bin/LauncherSettingsScrollTest/ -v:q -clp:ErrorsOnly
```

- 修复前：基于实际匹配器 XAML 的 5 项回归全部失败；复选框和说明文字上的滚轮被处理后，外层 VerticalOffset 仍为 0。
- 修复后：5 项通过；验证滚轮向上/向下、底部快速连接可达、13/24 DIP 字体、无内层滚动容器、默认不可编辑选项及双向绑定。测试只修改模拟选项。
- 完整 C# 测试：109 通过、0 失败、0 跳过。
- 真实编译的设置外壳和启动器设置页，以内存模型渲染：明暗主题、1000/600 DIP 宽度、13/20 DIP 字体。滚轮分别从复选框、说明和选项空白处发起，均能到达底部并返回顶部，快捷键与选项值不变。
- 正常宽度下滚动视口约 382 DIP、内容约 606 DIP；窄窗口大字体下视口约 313 DIP、内容约 679 DIP。底部快速连接选项在视口内。
- Debug 构建通过；已有 120 条警告、0 错误。测试目录保留 ssh_rust.dll 等原生依赖。

## 交付和范围

- 测试程序：`Ui/bin/LauncherSettingsScrollTest/RemoteX.exe`。
- 预览：`Ui/bin/FluentDialogsTest/screenshots/Settings-Launcher-{Top|Bottom}-{Light|Dark}-{1000|600}-{100|150|200}.png`。
- 100%/150%/200% 是渲染输出比例。实际鼠标、触摸板、滚动条拖动及显示器 DPI 切换仍需实机确认；未自动操作真实配置、热键、远程连接、数据库或更新。
- 本次只修复纵向滚动；大字体窄窗口下部分很长的匹配示例原有横向截断不在本次调整范围。
