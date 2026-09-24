# Fluent 树形视图卡死：本地修复验证

## 现场与定位

2.0.0 实机连续三次只读线程栈采样：UI 线程位于 TreeViewItem、VirtualizingStackPanel 的 Arrange、ContextLayoutManager.UpdateLayout 及 AutomationPeer.UpdateSubtree。CPU 持续增长，没有观察到数据库等待或拖拽阻塞。不能仅由这些采样认定无障碍功能有缺陷，因此不禁用无障碍服务。

模板还存在独立的尺寸问题：实际行高 40 DIP，而 PART_Header 只包含文字，12/24 字号对应约 15.24/30.48 DIP。两条新增测试在修复前失败、修复后通过。WPF 使用标题元素的 DesiredSize 计算层级虚拟化尺寸，参见 [TreeViewItem 源码](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/TreeViewItem.cs)。

仅修正标题尺寸不足以解除实际窗口滚动超时。隔离窗口中，原生模板、像素滚动、Standard 容器模式也曾触发同一滚动步骤超时；因此当前修复是范围受限的稳定性兜底，不宣称已修复 WPF 底层问题。

## 本地变更

- PART_Header 改为包含最小高度和内边距的完整标题边框。
- FluentTreeLayout 显式样式仅在 Fluent 模式关闭树形层级虚拟化；经典模式原有设置保持。列表虚拟化不变。
- 不修改用户数据、协议宿主、自动更新或无障碍功能。用户实机确认后仅提交本地代码，不改版本、不推送、不发布。
- 取舍：展开大量节点会创建更多控件，增加内存与首次布局成本。极大数据集建议先使用列表；后续可单独评估扁平化虚拟树。

## 验证

```powershell
dotnet test Tests/Tests.csproj -c Debug --no-restore
dotnet run --project tools/TreeLayoutProbe/TreeLayoutProbe.csproj -c Debug --no-restore
# 故障对照：显式重新启用原有虚拟化，独立进程 15 秒看门狗防止无限等待
dotnet run --project tools/TreeLayoutProbe/TreeLayoutProbe.csproj -c Debug --no-restore -- --virtualized
```

- 完整 C# 测试：88 项通过，无跳过。
- 隔离真实 WPF 窗口使用 4 个数据源、1200 条模拟连接；不初始化应用服务，不访问配置、凭据或数据库。
- 使用产品模板和布局样式验证加载、滚动、滚到底、回顶、大字体、窄窗口、展开折叠、视图卸载重新挂载。UI 低优先级心跳恢复；初始布局 2 次，后续每阶段 1 次。
- 用户使用测试版实机确认树形视图“恢复正常”。尚未完成不同实机 DPI、屏幕阅读器和极大数据集验收。真实鼠标拖放等业务交互不在本探针覆盖范围。
- 独立 Debug 测试版：`Ui/bin/TreeFreezeTest/RemoteX.exe`。请完整使用该目录，勿仅复制 EXE。
