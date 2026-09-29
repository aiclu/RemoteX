# Fluent 管理标签顶部残留区域修复

日期：2026-09-28。版本保持 2.0.1；仅本地代码和 Debug 测试产物，不提交、推送或发布。

## 原因与调整

- 管理标签页与连接列表使用同一个 Grid 叠放。标签页保留了经典标签栏使用的 21 DIP 顶部外边距和 20 DIP 空行，底层连接列表又始终可见，因此 Fluent 列表模式会从空隙露出数据源标题和滚动条。
- `ConnectionsPanel` 与 `TagManagementPresenter` 按现有 `TagListViewModel` 互斥显示；只折叠显示区域，不重建连接列表或改变筛选、选择及数据源折叠状态。
- 仅 Fluent 列表模式移除标签管理正文的旧留白；经典模式及仍使用顶部标签栏的卡片/树形模式保留原间距。
- 管理标签期间恢复外层普通内容面板，避免沿用数据源分组的透明背景；空连接的居中添加入口不再覆盖标签管理内容。
- 保留此前本地标签高亮和数据源卡片修复。不新增配置、语言文字或公共业务接口。

## 已执行验证

```powershell
dotnet test Tests/Tests.csproj -c Debug --no-restore -v:q -clp:ErrorsOnly
dotnet build Ui/Ui.csproj -c Debug --no-restore -p:OutDir=D:/Projects/RemoteX/Ui/bin/FluentDialogsTest/ -v:q -clp:ErrorsOnly
dotnet run --project tools/FluentPreview/Render.csproj --no-restore -v:q -clp:ErrorsOnly -- --tag-management
dotnet run --project tools/FluentPreview/Render.csproj --no-restore -v:q -clp:ErrorsOnly -- --source-groups
dotnet build Ui/Ui.csproj -c Debug --no-restore -p:OutDir=D:/Projects/RemoteX/Ui/bin/TagManagementTest/ -v:q -clp:ErrorsOnly
```

- 完整 C# 测试：104 通过、0 失败、0 跳过。新增两项覆盖内容互斥、模式切换时留白恢复、分组外层面板恢复和空连接添加入口隐藏。
- 两个 Debug 输出均构建成功：120 条警告、0 错误；交付与渲染目录的 `RemoteX.dll` SHA256 一致。
- 使用真实编译的列表页与标签管理页，配合模拟数据，在浅色/深色、800/420 DIP 下各连续切换 12 次；没有旧顶部留白，数据源折叠状态保持。隔离工具直接提供视图内容，不启动 Stylet 应用或访问用户配置。
- 数据源卡片回归通过：折叠双向绑定、独立操作按钮、名称和状态变化、空组，以及 1200 条模拟连接的列表/卡片虚拟化。
- `ssh_rust.dll` 存在，并通过本地加载和 `sr_set_log_callback` 导出检查。没有调用登录接口。
- 明暗预览位于 `Ui/bin/FluentDialogsTest/screenshots/TagManagement-{Light|Dark}-{800|420}-{100|150|200}.png`。100%/150%/200% 是渲染输出比例，不代表实机显示器 DPI 验证。

## 本地交付与待验

测试程序：`Ui/bin/TagManagementTest/RemoteX.exe`。

待实机确认：通过“更多 → 管理标签”进入、返回全部连接/标签筛选、真实数据下的键盘焦点与滚动位置，以及不同显示器缩放。没有自动执行真实数据库修改、远程连接或软件更新。
