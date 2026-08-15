# RemoteX

[English](readme.md) | 中文

[![version](https://img.shields.io/github/v/release/aiclu/RemoteX?color=Green&include_prereleases)](https://github.com/aiclu/RemoteX/releases)
[![issues](https://img.shields.io/github/issues/aiclu/RemoteX)](https://github.com/aiclu/RemoteX/issues)
[![license](https://img.shields.io/github/license/aiclu/RemoteX?color=blue)](https://github.com/aiclu/RemoteX/blob/main/LICENSE)
[![CI](https://github.com/aiclu/RemoteX/actions/workflows/build-on-dev-push.yml/badge.svg)](https://github.com/aiclu/RemoteX/actions/workflows/build-on-dev-push.yml)

RemoteX 是一款现代化的个人远程会话管理与启动器。它可以在一个统一界面中管理所有远程会话，支持多种协议。

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

最新版本：1.0.9

### 🔻[下载](https://github.com/aiclu/RemoteX/releases)

在 [Releases 页面](https://github.com/aiclu/RemoteX/releases) 下载 `RemoteX-1.0.9-net9-x64.zip`（框架依赖版）或 `RemoteX-1.0.9-net9-x64-self-contained.zip`（无需安装 .NET 运行时的自包含版）。

## 👓概览

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
