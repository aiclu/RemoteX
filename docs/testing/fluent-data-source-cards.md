# Fluent 数据源卡片与折叠红线修复

日期：2026-09-28。版本保持 2.0.1；仅本地代码和 Debug 产物，无提交、推送或发布。

## 实现

- Fluent 列表和卡片视图采用独立数据源卡片：8 DIP 圆角、1 DIP 边框、8 DIP 组间距；标题最小高度 44 DIP（外边框后正常实测 46 DIP）。树形视图及经典外观模板保留。
- 标题使用方向箭头、全选框、拖动手柄、可省略名称，以及右侧状态/刷新和添加按钮。状态失败时增加换行说明，箭头仍与首行对齐；只读和失败状态保留原命令限制。
- 实际列表进入分组时，通过 UI 附加属性通知外层容器撤掉重复面板；退出分组或切换视图后恢复。没有新增业务接口、配置或手动事件订阅。
- 分组仅禁用横向滚动以约束名称宽度；卡片排列使用分组正文可用宽度，避免边框、内边距和滚动条导致末列裁切。原虚拟化面板、ItemsPresenter、命令、拖动处理和存储格式保持。
- 提示复用现有 `Select`、`Sort by drag`、`Add` 本地化资源。没有新增 XAML 文件。

## 红线根因与证据

初始就折叠的旧模板未显示异常；通过真实编译的分组模板先展开、再折叠后复现：

```text
Toggle 0: expanded=True, source=True, binding=Active
Toggle 1: expanded=False, source=True, binding=UpdateSourceError
VALIDATION: 未能转换值“”。
```

`IsExpanded` 的双向绑定将 `TargetNullValue=False` 用作空值标记。折叠时 WPF 把目标 `false` 反向映射为 `null`，不能写入非空布尔属性 `GroupedIsExpanded`，产生校验装饰层，源值也未保存。

删除这个不适用的空值标记，保留 `FallbackValue=False` 并明确双向绑定。Fluent 和经典分组共用的折叠语义均修复；不改经典外观，不禁用 `Validation.ErrorTemplate`。修复后连续十次切换都保持 `binding=Active`，源布尔值随之更新，校验错误为零。

## 已执行的自动验证

```powershell
dotnet test Tests/Tests.csproj -c Debug --no-restore -v:q -clp:ErrorsOnly
dotnet build Ui/Ui.csproj -c Debug --no-restore -p:OutDir=D:/Projects/RemoteX/Ui/bin/FluentDialogsTest/ -v:q -clp:ErrorsOnly
dotnet run --project tools/FluentPreview/Render.csproj --no-restore -v:q -clp:ErrorsOnly -- --source-groups
dotnet build Ui/Ui.csproj -c Debug --no-restore -p:OutDir=D:/Projects/RemoteX/Ui/bin/DataSourceCardsTest/ -v:q -clp:ErrorsOnly
```

- 完整 C# 测试：102 通过，0 失败，0 跳过，包括此前本地标签高亮修复。
- 新增 8 项测试结果覆盖展开/折叠双向同步、大字体标题、操作按钮、明暗及高对比度资源、外层面板恢复、全选/部分选中，以及临时目录内的折叠状态保存/重新加载。
- Debug 构建通过，独立构建有 120 条警告、0 错误。
- 隔离渲染使用真实 `ServerListPageView` 分组模板及模拟行/数据源。按钮通过 WPF Automation Provider 激活，验证刷新/添加命令各调用一次、参数为对应数据源且不触发展开。验证真实模板中的选择框名称作用域、拖动手柄事件、改名、只读/失败状态、集合清空、移除及空数据源占位。
- 1200 条模拟连接：列表滚动时实现 24–60 个行容器；同规模卡片排列实现 15–21 个容器。窄窗口的卡片边界检查通过，没有取消虚拟化或添加嵌套 ScrollViewer。
- 14 种语言资源加载通过。明暗、800/420 DIP 宽度、普通/20–24 DIP 字体、折叠/展开/空组/只读/错误场景生成 100%/150%/200% 渲染文件。
- `DataSourceCardsTest/ssh_rust.dll` 存在且通过本地 `NativeLibrary.TryLoad` 及 `sr_set_log_callback` 导出检查。不启动真实协议会话或调用 Rust 登录接口。

## 本地交付与实机待验

测试程序：`Ui/bin/DataSourceCardsTest/RemoteX.exe`。

渲染样例位于 `Ui/bin/FluentDialogsTest/screenshots/`：

- `Sources-Collapsed-Light-800-100.png` / `Sources-Collapsed-Dark-800-100.png`
- `Sources-Expanded-Light-800-100.png`
- `Sources-States-Dark-420-100.png`
- `Sources-CardView-Light-420-100.png`
- 同名前缀的 `-150.png`、`-200.png` 是输出比例，不代表真实显示器 DPI 切换。

尚需实机检查：鼠标空白区域点击、实际拖放排序、Tab/Space 焦点导航、系统主题动态切换及系统高对比度、100%/150%/200% 显示器缩放，以及用户真实数据源状态下的重启恢复。自动验证没有执行真实数据库操作、连接或升级；只在独立临时目录写入测试折叠状态。
