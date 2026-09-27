# 梦之形 - 自动施法与自动普攻 Mod / Shape of Dreams - AutoUseSkill Mod

[English](#english) | [中文](#chinese)

---

<a name="chinese"></a>
## 简体中文

适用于 Unity 肉鸽动作游戏《梦之形》（*Shape of Dreams*）的原生辅助 Mod。基于官方原生 Mod 系统（DewMod）构建，**无需安装 BepInEx**，解压即用。

### ✨ 主要特性

- **智能自动施法**：自动检测冷却（CD）、技能有效射程与合法目标（支持单体目标、锥形范围、弹道指向等不同施法类型）。
- **智能自动普攻**：目标在攻击距离内时自动衔接普通攻击，支持通过快捷键一键启闭。
- **独立技能快捷键**：局内随时按 F1 ~ F4 独立切换 Q/W/E/R 技能自动释放，按 F5 切换自动普攻，按 F6 切换自动击碎矿石/金币罐。
- **技能图标常驻状态显示**：在游戏技能栏各个图标内常驻显示醒目的状态标签：
  - 开启时显示绿色 **ON** 标签
  - 关闭时显示红色 **OFF** 标签
  - 自动普攻与自动敲矿在技能栏左侧常驻显示 **普攻 ON / OFF** 与 **敲矿 ON / OFF**
- **局内图形化设置**：按 I 键随时呼出/关闭浮动设置菜单，支持调整技能开关、脱战施法以及检测频率滑条。
- **轻量零依赖**：完全依赖游戏底层组件，性能开销极低。

### ⌨️ 快捷键一览

| 按键 | 功能说明 | 图标常驻显示 |
| :---: | :--- | :---: |
| **F1** | 切换 **Q 技能** 自动释放（开 / 关） | 绿色 ON / 红色 OFF |
| **F2** | 切换 **W 技能** 自动释放（开 / 关） | 绿色 ON / 红色 OFF |
| **F3** | 切换 **E 技能** 自动释放（开 / 关） | 绿色 ON / 红色 OFF |
| **F4** | 切换 **R 技能** 自动释放（开 / 关） | 绿色 ON / 红色 OFF |
| **F5** | 切换 **自动普通攻击**（开 / 关） | 绿色 普攻 ON / 红色 普攻 OFF |
| **F6** | 切换 **自动击碎矿石/金币罐**（开 / 关） | 绿色 敲矿 ON / 红色 敲矿 OFF |
| **I** | 打开 / 关闭图形化设置窗口 | — |

> **提示**：若开启了游戏设置中的【开发者模式】，在控制台（~ 键）输入 utoskill 也可调出设置窗口。

### 📦 安装方法

1. 前往本仓库的 [Releases](../../releases) 页面下载最新的发布包压缩文件，或直接克隆本仓库。
2. 将本模组文件夹放置于游戏安装目录的 Mods 文件夹下，例如：
   `	ext
   <游戏根目录>/Mods/AutoUseSkillMod/
   ├── about/
   │   ├── metadata.json
   │   ├── description.txt
   │   ├── icon.png
   │   └── preview.png
   ├── AutoUseSkill.dll
   └── ...
   `
3. 启动游戏，在标题界面点击 **Mods**，确认列表中已识别并勾选启用 **AutoUseSkill** 即可。

### 🛠️ 源码编译

本项目兼容 .NET Framework 4.7.2 / C# 5+ 编译器，依赖游戏本体目录下的核心 DLL：
- 引用路径：<游戏根目录>/Shape of Dreams_Data/Managed/
- 关键依赖项：Dew.Core.dll、Dew.UI.dll、UnityEngine.CoreModule.dll、UnityEngine.IMGUIModule.dll、UnityEngine.InputLegacyModule.dll

---

<a name="english"></a>
## English

A native Quality-of-Life (QoL) mod for the action roguelike *Shape of Dreams*. Built on the official native modding architecture (DewMod), **no BepInEx installation required**.

### ✨ Features

- **Smart Auto-Cast**: Automatically verifies cooldowns, effective skill ranges, and target validators (supports Cone, Arrow, Target, and Point cast methods).
- **Smart Auto-Attack**: Automatically executes basic attacks when an enemy is within attack range, easily toggled on/off.
- **Individual Hotkeys**: Toggle auto-casting for Q/W/E/R independently in real-time (F1–F4), and auto-attack via F5, and auto-break resource props via F6.
- **Persistent In-Icon Badges**: Permanent, high-contrast badges displayed directly inside each skill icon:
  - Green **ON** when enabled
  - Red **OFF** when disabled
  - Auto-attack & Auto-break badges (普攻 ON / OFF, 敲矿 ON / OFF) displayed beside the skill bar.
- **In-Game GUI**: Press I to toggle a draggable configuration window to adjust toggles, out-of-combat casting, and detection interval.
- **Zero Overhead**: Clean, pure implementation directly interfacing with Dew.Core without external mod loaders.

### ⌨️ Controls & Hotkeys

| Key | Description | In-Icon Badge |
| :---: | :--- | :---: |
| **F1** | Toggle auto-cast for **Skill 1 (Q)** | Green ON / Red OFF |
| **F2** | Toggle auto-cast for **Skill 2 (W)** | Green ON / Red OFF |
| **F3** | Toggle auto-cast for **Skill 3 (E)** | Green ON / Red OFF |
| **F4** | Toggle auto-cast for **Skill 4 (R)** | Green ON / Red OFF |
| **F5** | Toggle **Auto-Attack** | Green 普攻 ON / Red 普攻 OFF |
| **F6** | Toggle **Auto-Break Props (Dust/Gold)** | Green 敲矿 ON / Red 敲矿 OFF |
| **I** | Toggle settings window on / off | — |

> **Note**: If Developer Mode is enabled in settings, you can also type utoskill in the debug console (~) to toggle the GUI.

### 📦 Installation

1. Download the latest release from the [Releases](../../releases) tab or clone this repository.
2. Place the mod folder inside the Mods directory of your game installation:
   `	ext
   <GameRoot>/Mods/AutoUseSkillMod/
   ├── about/
   │   ├── metadata.json
   │   ├── description.txt
   │   ├── icon.png
   │   └── preview.png
   ├── AutoUseSkill.dll
   └── ...
   `
3. Launch the game, click **Mods** on the main menu, and make sure **AutoUseSkill** is enabled.

### 🛠️ Building from Source

Compatible with standard .NET / Roslyn compilers referencing the managed assemblies in your game directory:
- Reference directory: <GameRoot>/Shape of Dreams_Data/Managed/
- Key assemblies: Dew.Core.dll, Dew.UI.dll, UnityEngine.CoreModule.dll, UnityEngine.IMGUIModule.dll, UnityEngine.InputLegacyModule.dll

---

## 📜 Credits / 致谢

- **Original Author**: Death (me.Death.Plugin.ShapeOfDream.AutoUseSkill)
- **Adaptation & Native DewMod Port**: Adapted for Shape of Dreams official native ModBehaviour framework.