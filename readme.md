# RemoteX

English | [中文](readme_zh-CN.md)

[![version](https://img.shields.io/github/v/release/aiclu/RemoteX?color=Green&include_prereleases)](https://github.com/aiclu/RemoteX/releases)
[![issues](https://img.shields.io/github/issues/aiclu/RemoteX)](https://github.com/aiclu/RemoteX/issues)
[![license](https://img.shields.io/github/license/aiclu/RemoteX?color=blue)](https://github.com/aiclu/RemoteX/blob/main/LICENSE)
[![CI](https://github.com/aiclu/RemoteX/actions/workflows/build-on-dev-push.yml/badge.svg)](https://github.com/aiclu/RemoteX/actions/workflows/build-on-dev-push.yml)

RemoteX is a modern personal remote session manager and launcher. It is a single place to manage all your remote sessions supporting number of different protocols.

## Features

- Supports RDP, SSH, VNC, Telnet, FTP/FTPS, SFTP, Serial, [RemoteApp](https://1remote.github.io/usage/protocol/especial/remoteapp/), [NoMachine and other app](https://1remote.github.io/usage/protocol/especial/app/)
- **Rust-powered protocol core** — SSH terminal, Telnet, Serial, FTP/FTPS and SFTP are all implemented in-process via a Rust FFI core (russh / suppaftp), with no external PuTTY / KiTTY dependencies
- Quick and convenient remote session launcher (Alt + M)
- Multi-screen and HiDPI RDP connection (Test on **Win10 + 4k monitor *2** RDP TO **Win2016**)
- Detailed connection configuration: tags, icons, colors, connection scripts etc.
- Multiple languages, themes and tabbed interface
- [Import connections from mRemoteNG](https://1remote.github.io/usage/overview/#importing-from-mremoteng)
- Data sources: SQLite (default), MySQL, PostgreSQL
- Portable - just unpack and run
- Built-in auto-updater (Rust self-updater, GitHub Releases as source)

## 🚩Installation

Latest Version: 1.0.7

### 🔻[Download](https://github.com/aiclu/RemoteX/releases)

Grab the `RemoteX-1.0.7-net9-x64.zip` (framework-dependent) or `RemoteX-1.0.7-net9-x64-self-contained.zip` (no .NET runtime required) asset from the [Releases page](https://github.com/aiclu/RemoteX/releases).

## 👓Overview

<img src="https://1remote.github.io/img/home_override/hero1.png" width="800" />

<p align="center">
    <img src="https://1remote.github.io/img/home_override/protocols.png" width="400" />
</p>
<p align="center">
    <img src="https://1remote.github.io/img/home_override/hero2.gif" width="400"/>
</p>

<p align="center">
    ↑ Launcher(Alt + M) open RDP connection & resizing
</p>

<p align="center">
    <img src="https://raw.githubusercontent.com/1Remote/PRemoteM/Doc/DocPic/multi-screen.jpg" width="500"/>
</p>

<p align="center">
    ↑ RDP with Multi-monitors
</p>

<p align="center">
    <img src="https://raw.githubusercontent.com/1Remote/PRemoteM/Doc/DocPic/RemoteApp/demo.jpg" width="800"/>
</p>

<p align="center">
    ↑ RemoteApp via RDP
</p>

## Special thanks

<a href="http://www.jetbrains.com/resharper/"><img src="http://www.tom-englert.de/Images/icon_ReSharper.png" alt="ReSharper" width="64" height="64" /></a>
