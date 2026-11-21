# Changelog / 更新日志

All notable versions of PoppyCh2Skip.
每个版本的变更记录（完整说明见对应的 [Releases](https://github.com/Linuxoid-cn/PoppyCh2Skip/releases) 页面）。

---

## v1.3.0

**未安装提示 + Steam 库扫描 / Not-installed message + Steam library scan**

- 定位顺序：配置 `GamePath` → exe 附近 → **自动扫描 Steam 库** → 手动输入
- 读注册表 `HKCU\Software\Valve\Steam\SteamPath` 并解析各库 `steamapps\libraryfolders.vdf`，
  在 `steamapps\common\Poppy Playtime` 查找游戏，命中后写回配置
- 全都没找到时打印：检查过的位置、可能原因、三条处理办法（可直接回车用默认存档位置）
- 新增 `--no-detect` 调试参数

## v1.2.0

**修复强制关闭 / 退出即跳转 / 双向回到 18**

- 修复强杀：识别 UE 的 `*-Win64-Shipping.exe`（如 `Playtime_Prototype4-Win64-Shipping`），
  增加按可执行文件路径兜底、`taskkill /T /F` 兜底、管理员提权重试（`--kill-game`）
- 任何情况下退出（成功 / 取消 / 出错 / 选退出）都显示作者并打开 GitHub
- 检查点双向：在 18 之前会跳过去、之后会回退到 18，本来在 18 则提示无需修改

## v1.1.0

**备份 / 恢复 / 配置 / 进程检测**

- 菜单扩为四项：跳过 / 只备份 / 恢复存档 / 退出
- 改档前强制备份整个存档目录（带时间戳，`*-before-skip`）
- 恢复：列出所有备份及其检查点值，恢复前先备份当前档（`*-pre-restore`）
- 新增 `PoppyCh2Skip.json` 配置（GamePath / SavePath / BackupDir / TargetCheckpoint /
  OpenGitHubAfterDone / KillGameWithoutAsking / GitHubUrl）
- 进程检测并提示是否强制关闭（此版按进程名精确匹配，存在关不掉的问题，v1.2.0 修复）
- 成功操作后打开作者 GitHub

## v1.0.0

**初版 / First release**

- 单文件控制台程序（C# / `csc` 编译，无需安装运行时）
- 菜单两项：`[1]` 跳过打地鼠（检查点 17 → 18）、`[2]` 退出
- 定位游戏：放在游戏目录可自动拾取，或手动输入路径（支持含空格路径）
- 只改 4 字节；校验 GVAS 头与第二章检查点类，拒绝错误文件
- 控制台与 exe 属性带作者签名 `Kernix / https://github.com/Linuxoid-cn`

---

## 关于历史版本 / Historical releases

v1.0.0 – v1.2.0 的原始二进制文件在开发过程中被后续版本覆盖，已不在本机保留，
因此那三个 Release **只保留了更新说明、不提供下载**；请使用
[最新版本](https://github.com/Linuxoid-cn/PoppyCh2Skip/releases/latest)。

The original binaries of v1.0.0 – v1.2.0 were overwritten during development and are no longer
available, so those releases keep their notes only. Please use the
[latest release](https://github.com/Linuxoid-cn/PoppyCh2Skip/releases/latest).

仓库源码始终保持最新（v1.3.0）；历史 tag 指向同一个最新提交。
The repository always holds the latest source (v1.3.0); the older tags point at that same commit.
