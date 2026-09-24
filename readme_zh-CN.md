# RemoteX

[English](readme.md) | 中文

[![version](https://img.shields.io/github/v/release/aiclu/RemoteX?color=Green&include_prereleases)](https://github.com/aiclu/RemoteX/releases)
[![issues](https://img.shields.io/github/issues/aiclu/RemoteX)](https://github.com/aiclu/RemoteX/issues)
[![license](https://img.shields.io/github/license/aiclu/RemoteX?color=blue)](https://github.com/aiclu/RemoteX/blob/main/LICENSE)
[![CI](https://github.com/aiclu/RemoteX/actions/workflows/build-on-dev-push.yml/badge.svg)](https://github.com/aiclu/RemoteX/actions/workflows/build-on-dev-push.yml)

RemoteX 是一款现代化的个人远程会话管理与启动器。它可以在一个统一界面中管理所有远程会话，支持多种协议。

## RemoteX 2.0

- **可选 Fluent 外观**：覆盖主界面、设置、连接编辑器、启动器、常用弹窗、会话窗口边框及列表／卡片／树形视图，支持浅色、深色与跟随系统。
- **紧凑连接管理**：全部连接入口、标签导航、居中搜索框、清晰的协议选择栏，以及带选中标记和排序方向的菜单。
- **保留经典模式**：现有配置默认继续使用经典外观；经典配色和视图偏好独立保留。
- **统一到 .NET 9**：应用依赖链及相关测试移除 .NET 6、.NET Framework 构建目标。
- 修复语言资源加载、搜索光标位置、RDP 编辑器补全弹层关闭，以及敏感字段加密时 SSH 私钥被清空的问题。

启用方式：进入 **设置 → 主题**，选择 **Fluent 预览**，再选择浅色、深色或跟随系统。Fluent 模式下也可通过左侧 **外观** 菜单切换主题或返回经典模式。

详见 [2.0.0 发布说明](docs/releases/2.0.0.md) 与 [开发指南](DEVELOP.md)。

## 功能特性

- 支持 RDP、SSH、VNC、Telnet、FTP/FTPS、SFTP、串口（Serial）、[RemoteApp](https://1remote.github.io/usage/protocol/especial/remoteapp/)、[NoMachine 等应用](https://1remote.github.io/usage/protocol/especial/app/)
- **Rust 协议核心** —— SSH 终端、Telnet、串口、FTP/FTPS、SFTP 全部由进程内 Rust FFI 核心（russh / suppaftp）实现，不再依赖外部 PuTTY / KiTTY
- 快速便捷的远程会话启动器（Alt + M）
- 多屏与 HiDPI 的 RDP 连接（已在 **Win10 + 4K 双屏** 连接 **Win2016** 上测试）
- 详细的连接配置：标签、图标、颜色、连接脚本等
- 多语言、多主题与标签页界面
- [从 mRemoteNG 导入连接](https://1remote.github.io/usage/overview/#importing-from-mremoteng)
- 数据源：SQLite（默认）、MySQL、PostgreSQL
- 便携免安装，解压即用
- 内置自动更新（Rust 自更新器，以 GitHub Releases 为更新源）

## 🚩安装

最新版本：2.0.0

### 🔻[下载](https://github.com/aiclu/RemoteX/releases)

在 [Releases 页面](https://github.com/aiclu/RemoteX/releases) 下载 `RemoteX-2.0.0-net9-x64.zip`（框架依赖版）或 `RemoteX-2.0.0-net9-x64-self-contained.zip`（无需安装 .NET 运行时的自包含版）。

### 运行要求与升级

- Windows x64；应用目标为 Windows 10 build 19041 或更高版本。
- 框架依赖版需要 **.NET 9 Desktop Runtime（x64）**；自包含版已包含运行时。
- 请**完整解压 ZIP**，保留随包提供的 `ssh_rust.dll`、`updater.exe` 等文件，不要只复制 `RemoteX.exe`。
- 升级前备份配置和连接数据库。手动替换程序文件前先退出 RemoteX，保留原配置和数据。
- 部分安装环境曾出现自动更新未完成。本版**不宣称修复该问题**；更新失败时，请退出程序并使用 Releases 中的完整 ZIP 手动更新。

## 👓概览

以下图片为历史协议功能／经典界面示例，并非新版 Fluent 外观截图。

<img src="https://1remote.github.io/img/home_override/hero1.png" width="800" />

<p align="center">
    <img src="https://1remote.github.io/img/home_override/protocols.png" width="400" />
</p>
<p align="center">
    <img src="https://1remote.github.io/img/home_override/hero2.gif" width="400"/>
</p>

<p align="center">
    ↑ 启动器（Alt + M）打开 RDP 连接并自动调整大小
</p>

<p align="center">
    <img src="https://raw.githubusercontent.com/1Remote/PRemoteM/Doc/DocPic/multi-screen.jpg" width="500"/>
</p>

<p align="center">
    ↑ RDP 多显示器
</p>

<p align="center">
    <img src="https://raw.githubusercontent.com/1Remote/PRemoteM/Doc/DocPic/RemoteApp/demo.jpg" width="800"/>
</p>

<p align="center">
    ↑ 通过 RDP 使用 RemoteApp
</p>

## 特别致谢

<a href="http://www.jetbrains.com/resharper/"><img src="http://www.tom-englert.de/Images/icon_ReSharper.png" alt="ReSharper" width="64" height="64" /></a>
