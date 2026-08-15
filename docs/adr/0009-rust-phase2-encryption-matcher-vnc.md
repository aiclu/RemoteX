# 0009: Rust 化第二期 —— 密码加密 / 关键词匹配 / VNC

- 状态：已完成
- 日期：2026-08-15

## 背景

RemoteX 已把 SSH/Telnet/Serial/FTP/SFTP 协议核心迁移到 Rust（见 ADR 0005-0007）。
本 ADR 决定处理剩余三个仍依赖第三方包/非 Rust 实现的模块，目标：
（A）移除第三方依赖；（B）协议核心全面 Rust 化。

## 决策

| 模块 | 现状 | 决策 | 方式 |
|---|---|---|---|
| 密码加密 | `1Remote.Security`（纯 BCL AES + 随机口令 + 盐） | Rust 复刻，算法/格式一致 | `ssh_rust.dll` 新增 `sr_string_encrypt/decrypt` FFI |
| 关键词匹配 | `VariableKeywordMatcher`（MIT，作者同源） | **不 Rust 化**，git subtree 进仓库 | vendoring 源码，保留 ToolGood 拼音 NuGet 依赖 |
| VNC/RFB | `1Remote.VncSharpCore`（GPL-2.0，黑盒 WinForms 控件） | Rust 重写，对齐现有行为 | `ssh_rust.dll` 新增 `sr_vnc_*` FFI，C# 侧 WPF 自绘 framebuffer |

### 详细决策

1. **密码加密 Rust 化**
   - Rust 完整复刻 `SimpleStringEncipher`（AES + 随机口令 + salt，格式一致）
   - **新盐策略**：Debug 构建中 `Assert.STRING_SALT` 是占位符，互解测试需显式传测试盐
   - FFI 设计：`sr_string_encrypt(salt, plaintext) -> ciphertext`、`sr_string_decrypt(salt, ciphertext) -> plaintext|null`（解密失败返回 null，与旧库行为一致）
   - **验证**：单元测试双向互解（C# 加密 → Rust 解密 → 回加密），CI 跑
   - **风险**：存量密文必须可解密；新密文格式保持一致 → 用户零感知迁移

2. **关键词匹配 vendoring**
   - `git subtree` 拉入 `github.com/VShawn/VariableKeywordMatcher`（MIT）源码
   - 保持命名空间 `VariableKeywordMatcher.*`、`VariableKeywordMatcherIn1.Builder` 不变
   - 拼音 provider 的 `ToolGood.Words.Pinyin` / `FirstPinyin` 保留 NuGet 引用
   - `KeywordMatchService` 零改动

3. **VNC/RFB Rust 化**
   - 编码支持：**Raw + CopyRect + Hextile + Tight**（对齐旧库主流，Tight 含 zlib）
   - 协议版本：RFB 3.8
   - 认证：None + VNC 密码（对齐旧库）
   - UI：C# 侧 `WriteableBitmap` 自绘 framebuffer，替代 WinForms 控件，移除 `WindowsFormsHost`
   - 接入：仿 `RustSshRunner` 模式新增 `RustVncRunner` + `VncHost` 改造
   - 顺带：移除 `1Remote.VncSharpCore` 的 GPL-2.0 依赖

### 实施顺序

1. **先密码加密**（纯函数无 UI 耦合，风险唯一是算法兼容，最早做最早暴露）
2. **再关键词匹配**（纯搬运无行为变化）
3. **最后 VNC**（最大的纯新增工程：协议 + 渲染）

## 后果

- 消除 `1Remote.VncSharpCore`（GPL-2.0）、`1Remote.Security`、`VariableKeywordMatcher*` 三个第三方依赖
- 密码加密成为 Rust 实现，与 SSH/Telnet/Serial/FTP/SFTP 一致
- VNC 从黑盒控件变为可控的 Rust 协议核心 + WPF 渲染
- 关键词匹配保持 C#（FFI 往返对热路径无益），但源码内联进仓库
