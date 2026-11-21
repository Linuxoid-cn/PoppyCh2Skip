# PoppyCh2Skip

Portable Windows console tool that **skips the Whack-a-Wuggy minigame in Poppy Playtime Chapter 2**
by moving the save's checkpoint to its end — with forced backups, restore, and a JSON config.

> Author: **Kernix** · <https://github.com/Linuxoid-cn> · Version v1.3.0
> [中文说明](README.zh-CN.md)

[![Build & Release](https://github.com/Linuxoid-cn/PoppyCh2Skip/actions/workflows/build-release.yml/badge.svg)](https://github.com/Linuxoid-cn/PoppyCh2Skip/actions/workflows/build-release.yml)
[![Latest release](https://img.shields.io/github/v/release/Linuxoid-cn/PoppyCh2Skip)](https://github.com/Linuxoid-cn/PoppyCh2Skip/releases/latest)

---

## What it does

Poppy Playtime Chapter 2 stores its progress in one `Int32` inside
`%LOCALAPPDATA%\Playtime_Prototype4\Saved\SaveGames\Chap2Checkpoint.sav`.

| Value | Checkpoint |
|---|---|
| 16 | Whack-A-Wuggy (before start) |
| 17 | Whack-A-Wuggy (in progress) |
| **18** | **Whack-A-Wuggy (End)** ← what this tool writes |
| 19 | Meeting Kissy Missy |

The tool sets that value to **18**, no matter whether the current save is **before or after** it
(if it already is 18, it tells you nothing needs to change). Only 4 bytes are touched —
trophies, settings and everything else stay untouched.

It always writes a **forced backup** before changing anything, keeps a restore function,
and shows the backup path so you can recover manually.

## Features

- **[1] Skip Whack-a-Wuggy** — forced backup → set checkpoint to 18 → read-back verification
- **[2] Backup only** — copy the whole save folder to a timestamped backup, without editing
- **[3] Restore** — list every backup with its checkpoint value, restore any of them
  (the current save is backed up first as `*-pre-restore`)
- **Game process handling** — detects the real UE `*-Win64-Shipping.exe` processes; asks
  “force close?” and kills them, with `taskkill` fallback and an elevation retry
- **Location detection** — config → next to the exe → **Steam library scan**
  (`steamapps/common/Poppy Playtime`) → manual input
- **Game not installed?** — prints exactly what was checked, why it may be missing, and how to
  point the tool at the right folder (or continue with the default save path)
- **JSON config** — `PoppyCh2Skip.json` next to the exe, every path/behaviour configurable
- **GitHub promotion** — every exit (success, cancel, error, quit) opens the author's GitHub

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for the version history (v1.0.0 -> v1.3.0).

## Download

Grab the latest portable zip from **[Releases](https://github.com/Linuxoid-cn/PoppyCh2Skip/releases/latest)**:

```
PoppyCh2Skip-vX.Y.Z.zip
├─ PoppyCh2Skip.exe
├─ PoppyCh2Skip.json
├─ README.md / README.zh-CN.md
└─ 快速使用.txt
```

Extract anywhere and run `PoppyCh2Skip.exe`. Windows 10/11, .NET Framework 4.x
(built into Windows — no runtime to install).

## Usage

1. **Fully close the game** (the tool will offer to force-close it if it's still running).
2. Run `PoppyCh2Skip.exe`.
3. Choose `[1]`, follow the prompts.
4. Start the game → Chapter 2 → **Continue**.

```
------------------------------ Menu ------------------------------
  [1] Skip "Whack-a-Wuggy" (forced backup + set checkpoint to 18)
  [2] Backup the save only (no changes)
  [3] Restore from a backup
  [4] Exit
------------------------------------------------------------------
   Choose [1/2/3/4]:
```

Put the exe inside the game folder (`...\steamapps\common\Poppy Playtime\`) and it is
detected automatically. Otherwise the tool scans Steam libraries, then asks for a path
(spaces are fine, quotes are stripped, `%LOCALAPPDATA%` variables are expanded).

## Configuration — `PoppyCh2Skip.json`

Generated next to the exe on first run. Safe to edit by hand; delete it to regenerate.

| Key | Meaning | Default |
|---|---|---|
| `GamePath` | Game folder, e.g. `D:\\SteamLibrary\\steamapps\\common\\Poppy Playtime` | `""` (auto-detect/ask) |
| `SavePath` | Full path of `Chap2Checkpoint.sav` | `""` (infer from `%LOCALAPPDATA%`) |
| `BackupDir` | Backup root | `""` (next to the exe) |
| `TargetCheckpoint` | Checkpoint to write on skip | `18` |
| `OpenGitHubAfterDone` | Open the author's GitHub on exit | `true` |
| `KillGameWithoutAsking` | Kill the game without asking | `false` |
| `GitHubUrl` | URL to open | `https://github.com/Linuxoid-cn` |

## Backups and manual recovery

```
<exe folder>\PoppyCh2Skip_Backups\
   20261122-011615-before-skip\    ← automatic backup before a skip
   20261122-011615-manual\         ← menu [2]
   20261122-011615-pre-restore\    ← safety backup before menu [3]
```

Each folder contains the whole save folder (`Chap2Checkpoint.sav`, `SG_Audio2.sav`,
`SG_Graphics2.sav`).

**Manual restore**: close the game, copy those `.sav` files back to
`C:\Users\<you>\AppData\Local\Playtime_Prototype4\Saved\SaveGames\`, then Continue in game.

## Command line

```bat
PoppyCh2Skip.exe                        :: normal menu
PoppyCh2Skip.exe --auto                 :: run [1] without the menu
PoppyCh2Skip.exe --save "X:\path\Chap2Checkpoint.sav" --auto
PoppyCh2Skip.exe --kill-game            :: only detect/close the game
PoppyCh2Skip.exe --no-detect            :: skip auto detection (debug)
```

## Build from source

```bat
:: from the repo root
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist dist mkdir dist
"%CSC%" /nologo /target:exe /platform:anycpu /optimize+ /codepage:65001 /out:dist\PoppyCh2Skip.exe src\PoppyCh2Skip.cs
```

or run `src\build.bat` (outputs `dist\PoppyCh2Skip.exe`). Pure C#, no third-party dependencies.

The GitHub Actions workflow [`.github/workflows/build-release.yml`](.github/workflows/build-release.yml)
compiles the exe on `windows-latest`, packages the portable zip, uploads it as a workflow
artifact, and — when a `v*` tag is pushed — publishes it to **Releases** automatically.

## Notes

- The game does **not** use Steam Cloud (`Steam\userdata\<id>\1721470\` does not exist),
  so local saves are not overwritten by the cloud.
- First run may trigger SmartScreen (“unknown publisher”) — click **More info → Run anyway**.
- If the game was started as administrator, killing it needs an elevated run:
  `PoppyCh2Skip.exe --kill-game` via “Run as administrator”.
- Checkpoint mapping cross-checked against the community all-checkpoints save pack:
  <https://www.speedrun.com/ppt_c2/resources/kil4i>

## License

No license has been declared for this project. All rights reserved by the author.
