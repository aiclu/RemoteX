# 0008: 应用图标重设计（任务栏模糊与品牌现代化）

- 状态：已接受（待实现）
- 日期：2026-08-15

## 背景

任务栏图标模糊且带突兀白色背景。排查发现根因是资产而非代码：

1. **模糊的根因**：`Ui/LOGO.ico`（657 B）与 `LOGO_D.ico`（680 B）物理上只含一个 16×16 小帧。主窗口从不设置 `Window.Icon`，Windows 回退到 exe 内嵌图标；即便 `AppIcons.cs` 请求 256×256，也只是把 16px 帧放大。
2. **白底**：旧设计是"白色底上的青蓝渐变圆角方块"，深色任务栏上呈现为突兀白块。
3. 用户同时希望图标更简洁、更现代。

## 决策

| 维度 | 决策 |
|---|---|
| 设计概念 | **X monogram**——由两个终端 chevron（`>_`）交叉组成的字母 X，对应 RemoteX |
| 背景风格 | Windows 11 fluent 风格：实心彩色圆角方块，方块外透明（plated） |
| 视觉风格 | 纯色扁平，无渐变无阴影 |
| 品牌色 | `#00A6C4`（青） |
| Debug 变体 | `LOGO_D.ico` 右下角叠加橙色小圆标 + 字母 D |
| MSIX 变体 | unplated 用品牌色字形（透明底）；lightunplated 用白色字形（透明底） |
| 小尺寸 | 16/24px 不做简化变体（X 几何简单，缩小后仍清晰） |
| 制作方式 | 矢量绘制（非 AI 像素图），程序化渲染全尺寸帧 |

## 实现范围

- 重新生成 `Ui/LOGO.ico`、`Ui/LOGO_D.ico`（含 16/24/32/48/64/128/256 全帧）
- 替换 `Ui/Resources/Image/Logo/logo32.png`、`logo64.png`、`logo256.png`（标题栏、关于页）
- 重新生成 `Installer/Images/` 全部 35 个 MSIX PNG（PackageLogo/SmallTile/Square150x150Logo/Square44x44Logo 的各 scale 与 targetsize/altform 变体）

## 后果

- 任务栏图标在任意 DPI 下清晰（ICO 含真实 256px 帧）
- 深色/浅色任务栏上图标呈现统一的青色圆角方块，不再出现白块
- 品牌视觉从"窗口+钥匙箭头"切换为"X monogram"
