# PoppyCh2Skip

便携式 Windows 命令行小工具：把《Poppy Playtime 第二章》自动存档的检查点直接设为
**18（Whack-A-Wuggy End，打地鼠刚结束）**，从而跳过第二章第二个小游戏（打地鼠）。
带强制备份、恢复存档、JSON 配置和进程检测。

> 作者：**Kernix** · <https://github.com/Linuxoid-cn> · 版本 v1.3.0
> [English](README.md)

[![Build & Release](https://github.com/Linuxoid-cn/PoppyCh2Skip/actions/workflows/build-release.yml/badge.svg)](https://github.com/Linuxoid-cn/PoppyCh2Skip/actions/workflows/build-release.yml)
[![Latest release](https://img.shields.io/github/v/release/Linuxoid-cn/PoppyCh2Skip)](https://github.com/Linuxoid-cn/PoppyCh2Skip/releases/latest)

---

## 它做了什么

第二章的进度就存在一个整数里：
`%LOCALAPPDATA%\Playtime_Prototype4\Saved\SaveGames\Chap2Checkpoint.sav`

| 值 | 检查点 |
|---|---|
| 16 | Whack-A-Wuggy（开始前） |
| 17 | Whack-A-Wuggy（打地鼠中） |
| **18** | **Whack-A-Wuggy (End) 打地鼠刚结束** ← 本工具写入 |
| 19 | Meeting Kissy Missy（已离开场地） |

工具会把这个值设成 **18**：不管当前进度在小游戏**之前还是之后**，都会回到 18；
本来就在 18 则提示"无需修改"。只改 4 个字节，奖杯、设置等其它数据完全不动。

改动前一定会**强制备份**，并带恢复功能，备份路径会打印出来，方便手动兜底。

## 功能

- **[1] 跳过打地鼠** —— 强制备份 → 检查点设为 18 → 读回校验
- **[2] 只备份** —— 整份备份存档，不改档
- **[3] 恢复存档** —— 列出所有备份（含其中检查点值），选一份还原；
  还原前会先把当前档备份成 `*-pre-restore`
- **进程检测** —— 识别真实运行的 UE `*-Win64-Shipping.exe`，提示"是否强制关闭"并强杀，
  失败时用 `taskkill` 兜底，权限不足可提权重试
- **定位游戏** —— 配置 → exe 附近 → **自动扫描 Steam 库**
  （`steamapps/common/Poppy Playtime`）→ 手动输入
- **没装游戏时** —— 明确打印：检查过哪些位置、可能原因、怎么把路径给它
  （或直接回车用默认存档位置）
- **JSON 配置** —— exe 同目录 `PoppyCh2Skip.json`，路径/行为都能改
- **推广 GitHub** —— 任何情况下退出（成功/取消/出错/选退出）都会打开作者主页

## 下载

到 **[Releases](https://github.com/Linuxoid-cn/PoppyCh2Skip/releases/latest)** 下载便携包：

```
PoppyCh2Skip-vX.Y.Z.zip
├─ PoppyCh2Skip.exe
├─ PoppyCh2Skip.json
├─ README.md / README.zh-CN.md
└─ 快速使用.txt
```

解压到任意位置，双击 `PoppyCh2Skip.exe`。Windows 10/11，系统自带 .NET Framework 4.x 即可，
不需要安装任何运行时。

## 使用

1. **完全退出游戏**（如果还开着，工具会提示是否强制关闭）；
2. 运行 `PoppyCh2Skip.exe`；
3. 选 `[1]`，按提示走；
4. 启动游戏 → 第 2 章 → **继续游戏 (Continue)**。

```
------------------------------ 主菜单 ------------------------------
  [1] 跳过 "打地鼠" 这一关 (强制备份 + 设为 18)
  [2] 只备份原存档 (强制备份, 不改档)
  [3] 恢复存档 (从备份还原)
  [4] 退出程序
--------------------------------------------------------------------
   请选择 [1/2/3/4]:
```

把 exe 放进游戏文件夹（`...\steamapps\common\Poppy Playtime\`）会自动识别；
否则先扫 Steam 库，再让你输入路径（**带空格没关系**，引号会自动去掉，
也支持 `%LOCALAPPDATA%` 这类环境变量）。

## 配置文件 `PoppyCh2Skip.json`

首次运行自动生成在 exe 同目录，可以手动改；改坏了删掉即可重新生成。

| 参数 | 作用 | 默认 |
|---|---|---|
| `GamePath` | 游戏文件夹，如 `D:\\SteamLibrary\\steamapps\\common\\Poppy Playtime` | `""`（自动检测/询问） |
| `SavePath` | 存档完整路径 | `""`（按 `%LOCALAPPDATA%` 推断） |
| `BackupDir` | 备份根目录 | `""`（exe 同级） |
| `TargetCheckpoint` | 跳过时写入的检查点 | `18` |
| `OpenGitHubAfterDone` | 退出时是否打开作者 GitHub | `true` |
| `KillGameWithoutAsking` | 游戏运行时是否直接强杀（不询问） | `false` |
| `GitHubUrl` | 要打开的地址 | `https://github.com/Linuxoid-cn` |

## 备份与手动恢复

```
<exe 目录>\PoppyCh2Skip_Backups\
   20261122-011615-before-skip\    ← 跳过前的自动备份
   20261122-011615-manual\         ← 菜单 [2]
   20261122-011615-pre-restore\    ← 菜单 [3] 执行前的安全备份
```

每个备份目录里是整套存档（`Chap2Checkpoint.sav`、`SG_Audio2.sav`、`SG_Graphics2.sav`）。

**手动恢复**：退出游戏 → 把这些 `.sav` 覆盖回
`C:\Users\<你>\AppData\Local\Playtime_Prototype4\Saved\SaveGames\` → 进游戏选"继续游戏"。

## 命令行参数

```bat
PoppyCh2Skip.exe                        :: 正常菜单
PoppyCh2Skip.exe --auto                 :: 跳过菜单, 直接执行 [1]
PoppyCh2Skip.exe --save "X:\path\Chap2Checkpoint.sav" --auto
PoppyCh2Skip.exe --kill-game            :: 只检测并关闭游戏
PoppyCh2Skip.exe --no-detect            :: 跳过自动检测 (调试用)
```

## 从源码编译

```bat
:: 在仓库根目录
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist dist mkdir dist
"%CSC%" /nologo /target:exe /platform:anycpu /optimize+ /codepage:65001 /out:dist\PoppyCh2Skip.exe src\PoppyCh2Skip.cs
```

或直接运行 `src\build.bat`（产物在 `dist\PoppyCh2Skip.exe`）。纯 C#，无第三方依赖。

GitHub Actions 工作流 [`.github/workflows/build-release.yml`](.github/workflows/build-release.yml)
会在 `windows-latest` 上编译、打便携 zip、上传为 artifact；推 `v*` 标签时自动发布到 **Releases**。

## 说明

- 游戏**没有 Steam 云同步**（`Steam\userdata\<id>\1721470\` 不存在），本地改档不会被云端覆盖；
- 首次运行可能弹 SmartScreen"未知发布者" → **更多信息 → 仍要运行**；
- 如果游戏是以管理员身份启动的，强杀需要管理员权限：
  用"以管理员身份运行"执行 `PoppyCh2Skip.exe --kill-game`；
- 检查点对照来自社区全检查点存档包：<https://www.speedrun.com/ppt_c2/resources/kil4i>。

## 许可

本项目未声明开源许可证，作者保留所有权利。
