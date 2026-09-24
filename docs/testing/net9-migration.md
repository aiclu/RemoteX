# .NET 9 统一迁移

2026-09-24，RemoteX 版本仍为 1.0.16。本轮仅本地修改，不提交、推送或发布。

## 支持范围

- 主程序、完整测试和 SDK 风格 Dragablz 示例/测试：`net9.0-windows10.0.19041.0`。
- WPF 依赖库：`net9.0-windows`；通用工具库：`net9.0`。
- 删除主解决方案的 ReleaseNet6/ReleaseNet48 配置、依赖库旧目标、旧框架专用引用和不可达实现分支；保留 RUST_SSH、Store 和 Debug 功能开关。
- CI 只清理已注释的旧发布步骤，未执行发布。历史 ADR 和测试记录中关于旧框架的叙述保留，当前构建状态以本记录为准。
- `DragablzModernUIDemo` 是未纳入主构建的旧上游示例，未迁移、不承诺可构建。SDK 风格 `DragablzDemo` 增加仅用于示例的视图模型，未向组件增加公共 API。

## 测试迁移与隔离

- 完整测试保留项目引用。必要的测试工具兼容调整：Microsoft.NET.Test.Sdk 17.12.0、MSTest 框架/适配器 3.6.4；没有升级应用的全部依赖。
- GDI 用例从缺失引用的 xUnit 语法转换为已有 MSTest，保留断言。
- 经用户确认，过时的 DataService 实例与设置页 RSA 用例迁移为当前 DAO CRUD、敏感字段加密/解密、认证模式切换、配置保存/加载测试。
- 覆盖差异：旧数据库 RSA 密钥生成/清除及名称/地址加密已无对应产品接口，不声称继续覆盖。现有明文名称/地址、密码/私钥保护、RDP 网关密码、加密幂等性、单项与批量 CRUD、数据库重新打开均有对应验证。
- 测试配置和 SQLite 数据仅写入系统临时目录 `RemoteX.Tests/<随机ID>`。不删除现有用户配置、数据库；临时结果保留便于诊断。全套禁止并行，避免全局 IoC、WPF 和路径状态相互干扰。
- 版本检查测试使用回环地址提供固定元数据，不访问 GitHub、不下载或执行更新。夹具带行末换行；已有 HttpHelper.GetAsync 截掉末字节的行为未在本轮改动，该行为不属于框架迁移。

## 经用户确认的额外修复

测试暴露：空密码加密后变成非空值，触发 SSH 密码 setter 清空私钥。现在仅对非空密码赋加密值，私钥认证往返及切换到密码认证均验证通过。不修改序列化格式，不读取或迁移用户数据，也无法据此恢复此前已被清空的数据。

## 验证命令

```powershell
dotnet restore Tests/Tests.csproj --source https://api.nuget.org/v3/index.json -p:NuGetAudit=false
dotnet build Tests/Tests.csproj -t:Rebuild -c Debug --no-restore
dotnet test Tests/Tests.csproj -c Debug --no-restore
dotnet test Dragablz/Dragablz.Test/Dragablz.Test.csproj -c Debug --no-restore
dotnet build Dragablz/DragablzDemo/DragablzDemo.csproj -c Debug --no-restore
dotnet build Ui/Ui.csproj -c Debug --no-restore -p:OutDir=D:/Projects/RemoteX/Ui/bin/Net9MigrationTest/
```

迁移框架及适配器后，旧输出曾引起测试发现器混用；完整 Rebuild 后恢复正常。不应将“未发现测试”或仅 exit code 0 视为通过。

- 完整测试：83 项通过，0 跳过，覆盖全部语言加载、搜索光标、连接信息及 Fluent 回归。
- 独立 Fluent 专项测试：47 项通过（这些用例也包含在完整测试中，不额外累加）。
- Dragablz：21 项通过；SDK 风格示例构建通过。
- Debug 构建通过，仍有既有 nullable/过时 API 警告。
- 独立测试版：`Ui/bin/Net9MigrationTest/RemoteX.exe`；`ssh_rust.dll` 存在并通过 NativeLibrary.Load 检查。
- 未执行非 Debug 构建、发布、真实远程连接或自动更新；未宣称实机 UI/DPI 回归全部通过。
