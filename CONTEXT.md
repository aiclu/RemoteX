# 1Remote — SSH Terminal Context

1Remote 的 SSH 会话由"进程内 Rust SSH 核心（russh）通过 FFI 与 WPF 终端控件通信"提供，取代了原先嵌入外部 putty.exe 窗口的方案。本表收录该 SSH 终端改造涉及的领域术语，供后续阶段与协作者对齐。

## Language

**SSH Session**:
一次到远程服务器的完整 SSH 连接，由一个进程内 Rust 核心会话承载，C# 侧通过 64 位整数句柄（SessionHandle）引用。
_Avoid_: 连接实例、会话对象

**SessionHandle**:
C# 与 Rust 之间共享的 SSH 会话标识符。C# 只持有 i64 句柄而非裸指针，避免跨语言生命周期错配。
_Avoid_: 指针、引用

**RustSshRunner**:
1Remote 中 SSH 协议的默认 runner（一种 `InternalDefaultRunner`），通过 FFI 桥接 Rust SSH 核心而非启动外部可执行文件。
_Avoid_: 内置 SSH、原生 SSH

**RustSshHost**:
承载 Microsoft.Terminal.WPF 终端控件的 WPF 宿主（继承 `HostBase`），负责把终端字节流与 Rust SSH 核心双向对接。
_Avoid_: 终端页面、SSH 视图

**Terminal Control**:
WPF 终端渲染控件（Microsoft.Terminal.WPF），在进程内渲染 ANSI 输出、光标与滚动，是 SSH 会话的显示层。
_Avoid_: 终端窗口、putty 窗口

**FFI Bridge**:
C# 侧调用 Rust cdylib 导出函数的封装层，负责句柄管理、轮询读取线程与错误码到文本的映射。
_Avoid_: 互操作层、动态库封装

**Trust-on-first-use (TOFU)**:
主机密钥信任策略：首次连接静默接受并记录主机密钥，后续连接校验一致性。
_Avoid_: 主机密钥弹窗、证书校验

**Startup Auto Command**:
SSH 连接建立后自动注入远程 shell 的命令（`source /etc/profile; source ~/.bashrc; <cmd>; exec $SHELL`），语义对齐原 putty 的 `-m` 行为。
_Avoid_: 启动脚本、自动命令文件

**Code Page (编码页)**:
远程字节流到 Unicode 的字符编码。本期仅支持 UTF-8。
_Avoid_: 字符集、文本编码（仅限本语境歧义时使用）

## UI 现代化

**UI 现代化 (UI Modernization)**:
一次跨多个版本的 UI 升级计划，含三个独立目标轴：性能优化、内存占用、视觉现代化。三轴各自独立验收，按 性能→内存→视觉 顺序推进。
_Avoid_: UI 改版、界面美化（单指视觉轴时除外）

**性能基线 (Performance Baseline)**:
在改动前用 DEV-only 插桩记录的各场景耗时（DB 读取、UI 提交、过滤计算、视图切换），作为每次改动后回归对比的量化锚点。
_Avoid_: 基准测试、benchmark（本语境仅指应用内插桩数字）

**会话区 / 非会话区 (Session Area / Non-Session Area)**:
会话区 = 承载 RDP/SSH/VNC 等协议渲染的 HwndHost/WindowsFormsHost 区域；非会话区 = 主窗口背景、菜单、面板等普通 WPF 区域。视觉特效（Mica/Acrylic）仅限非会话区，因 Airspace 限制会话区无法被 WPF 元素覆盖。
_Avoid_: 内容区、主区域（不区分渲染载体时）
