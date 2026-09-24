# RemoteX

English | [中文](readme_zh-CN.md)

[![version](https://img.shields.io/github/v/release/aiclu/RemoteX?color=Green&include_prereleases)](https://github.com/aiclu/RemoteX/releases)
[![issues](https://img.shields.io/github/issues/aiclu/RemoteX)](https://github.com/aiclu/RemoteX/issues)
[![license](https://img.shields.io/github/license/aiclu/RemoteX?color=blue)](https://github.com/aiclu/RemoteX/blob/main/LICENSE)
[![CI](https://github.com/aiclu/RemoteX/actions/workflows/build-on-dev-push.yml/badge.svg)](https://github.com/aiclu/RemoteX/actions/workflows/build-on-dev-push.yml)

RemoteX is a modern personal remote session manager and launcher. It is a single place to manage all your remote sessions supporting number of different protocols.

## RemoteX 2.0

- **Optional Fluent appearance** across the main workspace, settings, connection editor, launcher, common dialogs, session chrome, and list/card/tree views. Choose light, dark, or system appearance.
- **Compact connection management** with an “All connections” entry, tag navigation, centered search, clearer protocol selection, and view/sort menus with selection indicators.
- **Classic mode remains available**. Existing installations keep their classic appearance by default; classic colors and view preferences are preserved separately.
- **.NET 9 throughout** the supported application libraries and tests; .NET 6 and .NET Framework build targets have been removed.
- Fixes for language resource loading, search caret positioning, RDP editor completion dismissal, and SSH private-key preservation during sensitive-field encryption.

To enable Fluent, open **Settings → Theme**, choose **Fluent preview**, then select light, dark, or system. In Fluent mode, the sidebar **Appearance** menu switches themes or returns to classic.

See [2.0.0 release notes](docs/releases/2.0.0.md) and the [development guide](DEVELOP.md).

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

Latest Version: 2.0.0

### 🔻[Download](https://github.com/aiclu/RemoteX/releases)

Grab the `RemoteX-2.0.0-net9-x64.zip` (framework-dependent) or `RemoteX-2.0.0-net9-x64-self-contained.zip` (no .NET runtime required) asset from the [Releases page](https://github.com/aiclu/RemoteX/releases).

### Requirements and upgrading

- Windows x64; the application targets Windows 10 build 19041 or later.
- The framework-dependent package requires the **.NET 9 Desktop Runtime (x64)**. The self-contained package includes the runtime.
- Extract the **entire ZIP**: keep `ssh_rust.dll`, `updater.exe`, and the other bundled files alongside the application. Do not distribute only `RemoteX.exe`.
- Before upgrading, back up your configuration and connection databases. Close RemoteX before manually replacing application files, and keep your data/configuration.
- Some installations have reported incomplete automatic updates. This release does **not** claim to resolve that issue; if updating fails, close the application and use the full ZIP from Releases.

## 👓Overview

The images below are historical protocol/classic-interface examples, not screenshots of the new Fluent appearance.

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
