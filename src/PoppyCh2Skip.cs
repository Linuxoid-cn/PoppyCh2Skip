// =============================================================================
//  Poppy Playtime Chapter 2 - Whack-a-Wuggy Skip  (portable console tool)
//  Author : Kernix   |   https://github.com/Linuxoid-cn
//  Version: 1.1.0
//
//  功能:
//    1) 跳过 "打地鼠" (Whack-a-Wuggy) 这一关 —— 强制备份后把检查点 17 改成 18
//    2) 只备份原存档 (强制备份, 不改档)
//    3) 从备份恢复存档
//    4) 退出
//
//  特性:
//    - 游戏运行中会提示是否强制关闭
//    - 游戏路径 / 存档路径写入 PoppyCh2Skip.json 配置文件
//    - 备份统一放在 <exe目录>\PoppyCh2Skip_Backups\<时间戳>\  (可用配置修改)
//    - 操作成功后自动打开作者 GitHub
//
//  编译: csc /target:exe /platform:anycpu /optimize+ /codepage:65001
//            /out:PoppyCh2Skip.exe PoppyCh2Skip.cs
//
//  参数 (一般用不到):
//        --save <文件路径>   直接指定存档文件 (跳过定位步骤)
//        --auto              跳过菜单, 直接执行 "跳过打地鼠"
// =============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

[assembly: AssemblyTitle("Poppy Playtime Ch.2 - Whack-a-Wuggy Skip")]
[assembly: AssemblyDescription("Portable console tool for Poppy Playtime Chapter 2: skip Whack-a-Wuggy, force-backup and restore the save, with JSON config.")]
[assembly: AssemblyCompany("Kernix")]
[assembly: AssemblyProduct("PoppyCh2Skip")]
[assembly: AssemblyCopyright("Kernix - https://github.com/Linuxoid-cn")]
[assembly: AssemblyTrademark("Kernix")]
[assembly: AssemblyVersion("1.3.0.0")]
[assembly: AssemblyFileVersion("1.3.0.0")]
[assembly: AssemblyInformationalVersion("1.3.0 by Kernix")]

namespace PoppyCh2Skip
{
    internal static class Program
    {
        private const string Author = "Kernix";
        private const string DefaultGitHubUrl = "https://github.com/Linuxoid-cn";
        private const string ToolVersion = "1.3.0";

        private const int DefaultTargetCheckpoint = 18; // [19] Whack-A-Wuggy (End)

        private const string SaveProjectName = "Playtime_Prototype4";
        private const string SaveFileName = "Chap2Checkpoint.sav";
        private const string SaveClassMarker = "Chap2_Checkpoints";
        private const string ConfigFileName = "PoppyCh2Skip.json";
        private const string BackupFolderName = "PoppyCh2Skip_Backups";

        private static string _exeDir = ".";
        private static string _exePath = "";
        private static string _configPath = "";
        private static AppConfig _cfg = new AppConfig();

        private static string _saveOverride = null;
        private static bool _auto = false;
        private static bool _killOnly = false;
        private static bool _noDetect = false;
        private static bool _authorDone = false;
        private static int _ownPid = -1;

        // 检查点数值 -> 位置 (来自社区全检查点包并逐个读档核对)
        private static readonly Dictionary<int, string> Checkpoints = new Dictionary<int, string>()
        {
            { 0,  "Poppy 的房间 (章节开始)" },
            { 1,  "Swing Into Slide" },
            { 2,  "Slide (End)" },
            { 3,  "Mommy Dialogue (第一次对话)" },
            { 4,  "Train Station (第一次进站)" },
            { 5,  "Green Hand Factory (入口)" },
            { 6,  "Green Hand Factory (出口)" },
            { 7,  "Musical Memory (开始前) - 第 1 个小游戏" },
            { 8,  "Musical Memory (第 1 轮)" },
            { 9,  "Musical Memory (第 2 轮)" },
            { 10, "Musical Memory (第 3 轮)" },
            { 11, "Musical Memory (第 4 轮)" },
            { 12, "Musical Memory (最后一轮)" },
            { 13, "Storage (入口)" },
            { 14, "Storage (出口)" },
            { 15, "Train Station (过完 Musical Memory)" },
            { 16, "Whack-A-Wuggy (开始前)" },
            { 17, "Whack A Wuggy (已开始 / 打地鼠中) - 第 2 个小游戏" },
            { 18, "Whack-A-Wuggy (End) 打地鼠刚结束" },
            { 19, "Meeting Kissy Missy (已离开小游戏场地)" },
            { 20, "Barry Gates (End)" },
            { 21, "Statues (入口) - 第 3 个小游戏" },
            { 22, "Statues (开始前)" },
            { 23, "Statues (已开始)" },
            { 24, "Statues (Escape 逃脱)" },
            { 25, "Water Treatment (入口)" },
            { 26, "Water Treatment (出口)" },
            { 27, "Power Room (红胶带)" },
            { 28, "Vent Fall (Power Room 之后坠落)" },
            { 29, "Levers Puzzle (入口)" },
            { 30, "Levers Puzzle (出口)" },
            { 31, "Mommy Mini Chase (拉杆)" },
            { 32, "Mommy Chase (第二次小追逐之后)" },
            { 33, "Mommy Chase (冲向 Mommy 结局)" },
        };

        [DllImport("kernel32.dll")]
        private static extern uint GetConsoleOutputCP();

        [DllImport("kernel32.dll")]
        private static extern uint GetConsoleCP();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref int lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        // =====================================================================
        //  main
        // =====================================================================
        private static int Main(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--save" && i + 1 < args.Length) _saveOverride = args[i + 1];
                else if (args[i] == "--auto") _auto = true;
                else if (args[i] == "--kill-game") _killOnly = true;
                else if (args[i] == "--no-detect") _noDetect = true;
            }

            try { _ownPid = Process.GetCurrentProcess().Id; } catch { _ownPid = -1; }

            try
            {
                _exePath = Assembly.GetExecutingAssembly().Location;
                _exeDir = Path.GetDirectoryName(_exePath);
                if (string.IsNullOrEmpty(_exeDir)) _exeDir = ".";
            }
            catch { }

            _configPath = Path.Combine(_exeDir, ConfigFileName);
            _cfg = AppConfig.Load(_configPath);

            try
            {
                TrySetupConsole();
                try { Console.Clear(); } catch { }

                Banner();
                Info("配置文件 : " + _configPath);
                Info("备份目录 : " + BackupRoot());
                if (!string.IsNullOrEmpty(_cfg.GamePath)) Info("配置的游戏路径 : " + _cfg.GamePath);

                if (_killOnly)
                {
                    Section("只强制关闭游戏 (--kill-game)");
                    bool closed = EnsureGameClosed();
                    if (closed) Ok("游戏已关闭。");
                    return 0;
                }

                if (_auto)
                {
                    DoSkip();
                    return 0;
                }

                while (true)
                {
                    Menu();
                    string input = Console.ReadLine();
                    if (input == null) return 0;
                    input = input.Trim();

                    if (input == "1") { DoSkip(); break; }
                    if (input == "2") { DoBackup(); break; }
                    if (input == "3") { DoRestore(); break; }
                    if (input == "4") { Goodbye(); break; }

                    Err("无效输入, 请输入 1 / 2 / 3 / 4。");
                }
                return 0;
            }
            finally
            {
                // 任何情况下退出程序 (成功 / 取消 / 出错 / 选 4) 都会走这里
                AuthorExit();
                Pause();
            }
        }

        private static void TrySetupConsole()
        {
            try { Console.Title = "Poppy Playtime Ch.2 - Whack-a-Wuggy Skip  |  " + Author + "  " + DefaultGitHubUrl; } catch { }
            try { Console.OutputEncoding = Encoding.GetEncoding((int)GetConsoleOutputCP()); } catch { }
            try { Console.InputEncoding = Encoding.GetEncoding((int)GetConsoleCP()); } catch { }
        }

        // =====================================================================
        //  output helpers
        // =====================================================================
        private static void Line(ConsoleColor color, string text)
        {
            ConsoleColor old = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = color;
                Console.WriteLine(text);
            }
            finally { Console.ForegroundColor = old; }
        }

        private static void Info(string text) { Line(ConsoleColor.Gray, "  [i] " + text); }
        private static void Ok(string text) { Line(ConsoleColor.Green, "  [OK] " + text); }
        private static void Warn(string text) { Line(ConsoleColor.Yellow, "  [!] " + text); }
        private static void Err(string text) { Line(ConsoleColor.Red, "  [X] " + text); }

        private static void Section(string text)
        {
            Console.WriteLine();
            Line(ConsoleColor.Cyan, "  >> " + text);
            Line(ConsoleColor.DarkGray, "  " + new string('-', Math.Min(70, 8 + text.Length)));
        }

        private static void Banner()
        {
            Console.WriteLine();
            Line(ConsoleColor.Magenta, "  ================================================================");
            Line(ConsoleColor.White,   "     Poppy Playtime Chapter 2  ·  跳过 \"打地鼠\" (Whack-a-Wuggy)");
            Line(ConsoleColor.Magenta, "  ================================================================");
            Line(ConsoleColor.Gray,    "     作者   : " + Author);
            Line(ConsoleColor.Gray,    "     GitHub : " + DefaultGitHubUrl);
            Line(ConsoleColor.Gray,    "     版本   : v" + ToolVersion);
            Line(ConsoleColor.Magenta, "  ----------------------------------------------------------------");
            Line(ConsoleColor.DarkGray, "     1) 跳过打地鼠: 强制备份后把检查点设为 18 (之前/之后都回到 18)");
            Line(ConsoleColor.DarkGray, "     2) 强制备份  : 不改档, 只把现在的存档整份备份出去");
            Line(ConsoleColor.DarkGray, "     3) 恢复存档  : 从任意一次备份还原 (还原前也会先备份当前档)");
            Line(ConsoleColor.Magenta, "  ================================================================");
            Console.WriteLine();
        }

        private static void Menu()
        {
            Console.WriteLine();
            Line(ConsoleColor.Cyan, "  ------------------------------ 主菜单 ------------------------------");
            Line(ConsoleColor.White, "    [1] 跳过 \"打地鼠\" 这一关 (强制备份 + 设为 18)");
            Line(ConsoleColor.White, "    [2] 只备份原存档 (强制备份, 不改档)");
            Line(ConsoleColor.White, "    [3] 恢复存档 (从备份还原)");
            Line(ConsoleColor.White, "    [4] 退出程序");
            Line(ConsoleColor.Cyan, "  --------------------------------------------------------------------");
            Console.Write("     请选择 [1/2/3/4]: ");
        }

        private static void Goodbye()
        {
            Console.WriteLine();
            Line(ConsoleColor.DarkGray, "  已退出。   -- " + Author + "   " + DefaultGitHubUrl);
        }

        private static void Pause()
        {
            if (Console.IsInputRedirected) return;
            Console.WriteLine();
            Line(ConsoleColor.DarkGray, "  按任意键退出...");
            try { Console.ReadKey(true); } catch { }
        }

        // =====================================================================
        //  flow 1: skip Whack-a-Wuggy
        // =====================================================================
        private static void DoSkip()
        {
            bool withOverride = !string.IsNullOrEmpty(_saveOverride);
            int total = withOverride ? 4 : 5;
            int step = 0;

            Section(++step + "/" + total + "  检查游戏进程");
            if (!EnsureGameClosed()) return;

            string savePath;
            if (withOverride)
            {
                Section(++step + "/" + total + "  使用指定的存档文件");
                savePath = CleanPath(_saveOverride);
                Ok("已指定: " + savePath);
            }
            else
            {
                Section(++step + "/" + total + "  定位游戏文件夹");
                string gameFolder = LocateGameFolder();
                if (gameFolder == null) { Err("已取消。"); return; }

                Section(++step + "/" + total + "  定位存档");
                savePath = ResolveSavePath(gameFolder);
            }

            if (string.IsNullOrEmpty(savePath) || !File.Exists(savePath))
            {
                Err("没有找到第二章存档 " + SaveFileName + "。");
                Info("请先启动一次游戏, 进入第 2 章并玩到有自动存档的位置, 然后再运行本工具。");
                Info("预期位置: " + Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    SaveProjectName, @"Saved\SaveGames", SaveFileName));
                return;
            }
            if (!withOverride) Ok("存档: " + savePath);
            RememberSavePath(savePath);

            int oldValue, offset;
            string error;
            if (!TryReadCheckpoint(savePath, out oldValue, out offset, out error))
            {
                Err("读取存档失败: " + error);
                return;
            }

            if (oldValue == _cfg.TargetCheckpoint)
            {
                Ok("当前检查点已经是 " + oldValue + " (" + NameOf(oldValue) + ")。");
                Info("本身就在打地鼠结束处, 不需要修改。");
                return;
            }

            Section(++step + "/" + total + "  强制备份原存档");
            Info("当前检查点: " + oldValue + "  (" + NameOf(oldValue) + ")");
            Info("目标检查点: " + _cfg.TargetCheckpoint + "  (" + NameOf(_cfg.TargetCheckpoint) + ")");
            if (oldValue > _cfg.TargetCheckpoint)
                Warn("当前进度在打地鼠之后, 本次操作会把检查点回退到 " + _cfg.TargetCheckpoint + "。");
            else
                Info("当前进度在打地鼠之前, 本次操作会跳到 " + _cfg.TargetCheckpoint + "。");
            string backupDir = CreateBackup(savePath, "before-skip");
            if (backupDir == null)
            {
                Err("备份失败, 出于安全考虑已取消修改。");
                Info("请检查磁盘空间/权限, 或把 exe 放到可写目录再试。");
                return;
            }
            Ok("已强制备份: " + backupDir);
            ShowBackupContent(backupDir);

            Section(++step + "/" + total + "  修改存档");
            if (!TryWriteCheckpoint(savePath, _cfg.TargetCheckpoint, out error))
            {
                Err("写入失败: " + error);
                Info("常见原因: 杀毒软件 / Windows \"受控文件夹访问\" 阻止, 或当前账户没有写权限。");
                Info("原存档没有被动过, 可以用备份覆盖回去: " + backupDir);
                OfferElevate(savePath);
                return;
            }

            int check;
            if (!TryReadCheckpoint(savePath, out check, out offset, out error) || check != _cfg.TargetCheckpoint)
            {
                Err("写后校验失败, 请把备份文件复制回存档目录还原:");
                Info(backupDir);
                return;
            }

            Console.WriteLine();
            Line(ConsoleColor.Green, "  ================================================================");
            Line(ConsoleColor.Green, "     修改成功 !");
            Line(ConsoleColor.Green, "  ================================================================");
            Ok("检查点: " + oldValue + " -> " + _cfg.TargetCheckpoint);
            Ok("现在启动游戏 -> 第 2 章 -> 继续游戏 (Continue)");
            Info("你会直接出现在打地鼠刚结束的位置, 小游戏已跳过。");
            Console.WriteLine();
            Line(ConsoleColor.DarkGray, "  备份目录 : " + backupDir);
            Line(ConsoleColor.DarkGray, "  配置文件 : " + _configPath);
            Line(ConsoleColor.DarkGray, "  还原方法 : 本工具 [3] 恢复存档, 或手动把备份里的 .sav 覆盖回");
            Line(ConsoleColor.DarkGray, "             " + Path.GetDirectoryName(savePath));
        }

        // =====================================================================
        //  flow 2: force backup only
        // =====================================================================
        private static void DoBackup()
        {
            const int total = 3;
            int step = 0;

            Section(++step + "/" + total + "  检查游戏进程");
            if (!EnsureGameClosed()) return;

            string savePath = FindSaveForFlows();
            if (savePath == null) return;

            Section(++step + "/" + total + "  定位存档");
            Ok("存档: " + savePath);
            RememberSavePath(savePath);

            int value, offset;
            string error;
            if (TryReadCheckpoint(savePath, out value, out offset, out error))
                Info("当前检查点: " + value + "  (" + NameOf(value) + ")");
            else
                Warn("读不到检查点数值 (" + error + "), 仍然继续备份。");

            Section(++step + "/" + total + "  强制备份");
            string backupDir = CreateBackup(savePath, "manual");
            if (backupDir == null)
            {
                Err("备份失败: 请检查磁盘空间/权限。");
                return;
            }

            Console.WriteLine();
            Line(ConsoleColor.Green, "  ================================================================");
            Line(ConsoleColor.Green, "     备份完成 !");
            Line(ConsoleColor.Green, "  ================================================================");
            Ok("备份目录: " + backupDir);
            ShowBackupContent(backupDir);
            Console.WriteLine();
            Line(ConsoleColor.DarkGray, "  这个备份目录就是手动恢复用的路径, 记下来即可:");
            Line(ConsoleColor.DarkGray, "    " + backupDir);
            Line(ConsoleColor.DarkGray, "  恢复: 把里面的 .sav 覆盖回");
            Line(ConsoleColor.DarkGray, "    " + Path.GetDirectoryName(savePath));
            Line(ConsoleColor.DarkGray, "  或直接用本工具的 [3] 恢复存档。");
        }

        // =====================================================================
        //  flow 3: restore
        // =====================================================================
        private static void DoRestore()
        {
            const int total = 3;
            int step = 0;

            Section(++step + "/" + total + "  检查游戏进程");
            if (!EnsureGameClosed()) return;

            string savePath = FindSaveForFlows();
            if (savePath == null) return;
            RememberSavePath(savePath);
            string saveDir = Path.GetDirectoryName(savePath);

            Section(++step + "/" + total + "  选择要恢复的备份");
            string chosen;
            if (!ChooseBackup(out chosen)) return;

            List<string> files = new List<string>(Directory.GetFiles(chosen, "*.sav"));
            if (files.Count == 0)
            {
                Err("这个备份里没有 .sav 文件: " + chosen);
                return;
            }

            Section(++step + "/" + total + "  恢复存档");
            Info("备份来源: " + chosen);
            Info("恢复目标: " + saveDir);

            if (!Console.IsInputRedirected)
            {
                Console.Write("  确认用这个备份覆盖当前存档吗? [y/N]: ");
                string a = (Console.ReadLine() ?? string.Empty).Trim().ToLowerInvariant();
                if (a != "y" && a != "yes") { Info("已取消。"); return; }
            }

            // 覆盖前先把"当前存档"也强制备份一份, 免得恢复错了没退路
            string safety = CreateBackup(savePath, "pre-restore");
            if (safety != null) Ok("已先备份当前存档: " + safety);
            else Warn("当前存档备份失败 (仍继续恢复)。");

            int okCount = 0;
            foreach (string f in files)
            {
                try
                {
                    string dst = Path.Combine(saveDir, Path.GetFileName(f));
                    FileAttributes attrs = File.GetAttributes(dst);
                    if ((attrs & FileAttributes.ReadOnly) != 0) File.SetAttributes(dst, attrs & ~FileAttributes.ReadOnly);
                    File.Copy(f, dst, true);
                    okCount++;
                    Ok("已恢复: " + Path.GetFileName(f));
                }
                catch (Exception ex)
                {
                    Err("恢复 " + Path.GetFileName(f) + " 失败: " + ex.Message);
                }
            }

            if (okCount == 0) { Err("没有任何文件恢复成功。"); return; }

            int value, offset;
            string error;
            if (TryReadCheckpoint(savePath, out value, out offset, out error))
            {
                Console.WriteLine();
                Line(ConsoleColor.Green, "  ================================================================");
                Line(ConsoleColor.Green, "     恢复完成 !  共 " + okCount + " 个文件");
                Line(ConsoleColor.Green, "  ================================================================");
                Ok("恢复后的检查点: " + value + "  (" + NameOf(value) + ")");
                Info("启动游戏 -> 第 2 章 -> 继续游戏 即可。");
            }
            else
            {
                Warn("文件已复制, 但读不到检查点数值: " + error);
            }
            Console.WriteLine();
            Line(ConsoleColor.DarkGray, "  如果你想撤销这次恢复, 用刚才的安全备份:");
            Line(ConsoleColor.DarkGray, "    " + (safety ?? "(无)"));
        }

        // =====================================================================
        //  game process handling
        // =====================================================================
        // 名字里带这些的进程都算游戏进程 (含 UE 的 *-Win64-Shipping.exe)
        private static bool NameLooksLikeGame(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.ToLowerInvariant();
            if (n.Contains("ch2skip")) return false;                          // 本工具自己
            if (n.Contains("poppy")) return true;                            // Poppy_Playtime-Win64-Shipping 等
            if (n.Contains("playtime")) return true;                         // Playtime_Prototype4-Win64-Shipping 等
            if (n.StartsWith("ch4_pro") || n.StartsWith("ch5_pro")) return true;
            return false;
        }

        private static bool IsGameProcess(Process p, string gameRoot)
        {
            try { if (p.Id == _ownPid) return false; } catch { }

            string name;
            try { name = p.ProcessName; } catch { return false; }
            if (NameLooksLikeGame(name)) return true;

            // 兜底: 可执行文件就在游戏目录里 (EpicWebHelper 之类的附属进程)
            if (!string.IsNullOrEmpty(gameRoot))
            {
                string path = TryGetProcessPath(p);
                if (!string.IsNullOrEmpty(path) &&
                    path.StartsWith(gameRoot, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>取进程的可执行文件路径: 先试 MainModule, 失败再用 QueryFullProcessImageName。</summary>
        private static string TryGetProcessPath(Process p)
        {
            try
            {
                string path = p.MainModule.FileName;
                if (!string.IsNullOrEmpty(path)) return path;
            }
            catch { }

            IntPtr h = IntPtr.Zero;
            try
            {
                int pid;
                try { pid = p.Id; } catch { return null; }

                h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
                if (h == IntPtr.Zero) return null;

                StringBuilder sb = new StringBuilder(1024);
                int size = sb.Capacity;
                if (QueryFullProcessImageName(h, 0, sb, ref size)) return sb.ToString(0, size);
            }
            catch { }
            finally
            {
                if (h != IntPtr.Zero) { try { CloseHandle(h); } catch { } }
            }
            return null;
        }

        private static List<Process> GetGameProcesses()
        {
            List<Process> list = new List<Process>();
            HashSet<int> seen = new HashSet<int>();
            string gameRoot = null;
            if (!string.IsNullOrEmpty(_cfg.GamePath) && Directory.Exists(_cfg.GamePath))
                gameRoot = _cfg.GamePath.TrimEnd('\\');

            foreach (Process p in Process.GetProcesses())
            {
                bool match = false;
                try { match = IsGameProcess(p, gameRoot); }
                catch { }

                if (match)
                {
                    int id = -1;
                    try { id = p.Id; } catch { }
                    if (id > 0 && seen.Add(id)) list.Add(p);
                    else { try { p.Dispose(); } catch { } }
                }
                else { try { p.Dispose(); } catch { } }
            }
            return list;
        }

        private static bool GameRunning()
        {
            List<Process> procs = GetGameProcesses();
            bool running = procs.Count > 0;
            foreach (Process p in procs) { try { p.Dispose(); } catch { } }
            return running;
        }

        private static bool EnsureGameClosed()
        {
            List<Process> procs = GetGameProcesses();
            if (procs.Count == 0)
            {
                Ok("游戏未在运行。");
                return true;
            }

            Err("检测到游戏正在运行:");
            foreach (Process p in procs) Line(ConsoleColor.Yellow, "      - " + Describe(p));
            Info("游戏退出时会用内存里的存档覆盖文件, 所以必须先关闭它。");
            Info("注意: 强制关闭会丢失上次自动存档之后的进度。");
            foreach (Process p in procs) { try { p.Dispose(); } catch { } }

            bool kill;
            if (_cfg.KillGameWithoutAsking)
            {
                Warn("配置 KillGameWithoutAsking = true, 直接强制关闭。");
                kill = true;
            }
            else if (Console.IsInputRedirected)
            {
                Err("输入被重定向, 无法询问是否关闭。请先手动退出游戏再试。");
                return false;
            }
            else
            {
                Console.Write("  是否强制关闭游戏进程? [y/N]: ");
                string a = (Console.ReadLine() ?? string.Empty).Trim().ToLowerInvariant();
                kill = (a == "y" || a == "yes");
            }

            if (!kill)
            {
                Err("已取消。请手动完全退出游戏 (含章节启动器窗口) 后重试。");
                return false;
            }

            // 1) 逐个 Kill
            foreach (Process p in GetGameProcesses())
            {
                string desc = Describe(p);
                try
                {
                    p.Kill();
                    Ok("已结束: " + desc);
                }
                catch (Exception ex)
                {
                    Warn("结束失败: " + desc + " -> " + ex.Message);
                }
                finally { try { p.Dispose(); } catch { } }
            }

            // 2) 等它真正退出, 最多约 10 秒
            for (int i = 0; i < 40; i++)
            {
                if (!GameRunning()) { Ok("游戏已关闭, 继续。"); return true; }
                System.Threading.Thread.Sleep(250);
            }

            // 3) 兜底: taskkill /PID x /T /F (连子进程一起)
            List<Process> remain = GetGameProcesses();
            foreach (Process p in remain)
            {
                int pid = -1;
                try { pid = p.Id; } catch { }
                finally { try { p.Dispose(); } catch { } }
                if (pid > 0) RunTaskkill(pid);
            }
            System.Threading.Thread.Sleep(1500);

            remain = GetGameProcesses();
            if (remain.Count == 0)
            {
                Ok("游戏已关闭 (taskkill 兜底), 继续。");
                return true;
            }

            Err("还有进程没关掉: " + JoinProcessNames(remain));
            foreach (Process p in remain) { try { p.Dispose(); } catch { } }
            Info("多半是权限不足 (游戏以管理员身份启动) 或进程被保护。");
            OfferElevateKill();
            return false;
        }

        private static string Describe(Process p)
        {
            try { return p.ProcessName + " (PID " + p.Id + ")"; }
            catch { return "? (PID ?)"; }
        }

        private static void RunTaskkill(int pid)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("taskkill", "/PID " + pid + " /T /F");
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                Process tk = Process.Start(psi);
                if (tk != null) { tk.WaitForExit(5000); tk.Dispose(); }
            }
            catch { }
        }

        private static void OfferElevateKill()
        {
            if (Console.IsInputRedirected) return;
            Console.WriteLine();
            Console.Write("  要以管理员身份重新打开本程序来关闭游戏吗? [y/N]: ");
            string a = (Console.ReadLine() ?? string.Empty).Trim().ToLowerInvariant();
            if (a != "y" && a != "yes") return;

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(_exePath, "--kill-game");
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                Process.Start(psi);
                Line(ConsoleColor.DarkGray, "  已在新窗口以管理员身份启动, 关闭游戏后请重新运行本工具。");
            }
            catch (Exception ex)
            {
                Err("提权失败 (可能你点了\"否\"): " + ex.Message);
            }
        }

        private static string JoinProcessNames(List<Process> procs)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Process p in procs)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(Describe(p));
            }
            return sb.ToString();
        }

        // =====================================================================
        //  game folder / save location
        // =====================================================================
        private static string LocateGameFolder()
        {
            // 1) 配置文件里的路径
            if (!string.IsNullOrEmpty(_cfg.GamePath))
            {
                if (LooksLikeGameFolder(_cfg.GamePath))
                {
                    Ok("使用配置文件里的游戏路径: " + _cfg.GamePath);
                    return _cfg.GamePath;
                }
                Warn("配置文件里的路径失效了: " + _cfg.GamePath);
            }

            // 2) exe 附近自动拾取
            if (!_noDetect)
            {
                string detected = DetectGameFolder();
                if (detected != null)
                {
                    Ok("已自动拾取游戏文件夹: " + detected);
                    _cfg.GamePath = detected;
                    _cfg.Save();
                    return detected;
                }
            }

            // 3) 扫描 Steam 库 (steamapps\common\Poppy Playtime)
            List<string> libraries = new List<string>();
            if (!_noDetect)
            {
                string fromSteam = FindGameInSteam(libraries);
                if (fromSteam != null)
                {
                    Ok("在 Steam 库里找到游戏: " + fromSteam);
                    _cfg.GamePath = fromSteam;
                    _cfg.Save();
                    return fromSteam;
                }
            }

            // 4) 本机确实没找到 -> 明确提示, 再让用户给路径
            ReportGameNotFound(libraries);

            string input = AskGameFolder();
            if (input == null) return null;
            if (input.Length == 0)
            {
                Info("未输入路径, 将使用默认存档位置 (%LOCALAPPDATA%\\" + SaveProjectName + ")。");
                return string.Empty;
            }

            _cfg.GamePath = input;
            _cfg.Save();
            return input;
        }

        // ------------------------------------------------------- Steam libraries
        private static List<string> GetSteamLibraryRoots()
        {
            List<string> roots = new List<string>();
            List<string> steamPaths = new List<string>();

            try
            {
                using (Microsoft.Win32.RegistryKey k =
                       Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    if (k != null)
                    {
                        string sp = k.GetValue("SteamPath") as string;
                        if (!string.IsNullOrEmpty(sp)) steamPaths.Add(sp.Replace('/', '\\'));
                    }
                }
            }
            catch { }

            try
            {
                using (Microsoft.Win32.RegistryKey k =
                       Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
                {
                    if (k != null)
                    {
                        string sp = k.GetValue("InstallPath") as string;
                        if (!string.IsNullOrEmpty(sp)) steamPaths.Add(sp.Replace('/', '\\'));
                    }
                }
            }
            catch { }

            steamPaths.Add(@"C:\Program Files (x86)\Steam");
            steamPaths.Add(@"C:\Program Files\Steam");

            foreach (string steam in steamPaths)
            {
                if (string.IsNullOrEmpty(steam) || !Directory.Exists(steam)) continue;
                if (!roots.Contains(steam)) roots.Add(steam);

                // 解析 libraryfolders.vdf 里的其它 Steam 库
                try
                {
                    string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
                    if (File.Exists(vdf))
                    {
                        foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s*\"([^\"]+)\""))
                        {
                            string lib = m.Groups[1].Value.Replace("\\\\", "\\").Replace('/', '\\');
                            if (!string.IsNullOrEmpty(lib) && Directory.Exists(lib) && !roots.Contains(lib))
                                roots.Add(lib);
                        }
                    }
                }
                catch { }
            }
            return roots;
        }

        private static string FindGameInSteam(List<string> checkedLibraries)
        {
            foreach (string root in GetSteamLibraryRoots())
            {
                if (checkedLibraries != null && !checkedLibraries.Contains(root)) checkedLibraries.Add(root);

                string common = Path.Combine(root, "steamapps", "common");
                string[] candidates =
                {
                    Path.Combine(common, "Poppy Playtime"),
                    Path.Combine(common, "Poppy Playtime Chapter 2"),
                };
                foreach (string c in candidates)
                    if (LooksLikeGameFolder(c)) return c;
            }
            return null;
        }

        private static void ReportGameNotFound(List<string> libraries)
        {
            Console.WriteLine();
            Line(ConsoleColor.Red, "  ================================================================");
            Line(ConsoleColor.Red, "     [X] 这台电脑上没有找到 Poppy Playtime");
            Line(ConsoleColor.Red, "  ================================================================");
            Line(ConsoleColor.Gray, "  [i] 已经检查过这些位置:");
            Line(ConsoleColor.DarkGray, "        - 配置文件里的 GamePath");
            Line(ConsoleColor.DarkGray, "        - 本程序所在目录及其上级目录");
            if (libraries != null && libraries.Count > 0)
            {
                foreach (string lib in libraries)
                    Line(ConsoleColor.DarkGray, "        - Steam 库: " + lib);
            }
            else
            {
                Line(ConsoleColor.DarkGray, "        - Steam 库: 没找到 Steam 安装 (或注册表里没有记录)");
            }
            Console.WriteLine();
            Line(ConsoleColor.Gray, "  [i] 可能的原因:");
            Line(ConsoleColor.DarkGray, "        - 这台电脑没装这个游戏, 或没装 Steam 版");
            Line(ConsoleColor.DarkGray, "        - 游戏装在别的盘 / 别的 Steam 库, 库文件夹没登记");
            Line(ConsoleColor.DarkGray, "        - 游戏文件夹被移动或改名");
            Console.WriteLine();
            Line(ConsoleColor.Gray, "  [i] 怎么办:");
            Line(ConsoleColor.DarkGray, "        1) Steam 里右键 Poppy Playtime -> 管理 -> 浏览本地文件");
            Line(ConsoleColor.DarkGray, "        2) 复制地址栏里的路径, 粘贴到下面");
            Line(ConsoleColor.DarkGray, "        3) 直接回车 = 使用默认存档位置 (游戏卸载了但存档还在时也能改)");
            Console.WriteLine();
        }

        private static string FindSaveForFlows()
        {
            if (!string.IsNullOrEmpty(_saveOverride))
            {
                string p = CleanPath(_saveOverride);
                if (File.Exists(p)) { Ok("使用指定的存档文件: " + p); return p; }
                Err("指定的存档文件不存在: " + p);
                return null;
            }

            if (!string.IsNullOrEmpty(_cfg.SavePath) && File.Exists(_cfg.SavePath))
            {
                Ok("使用配置文件里的存档路径: " + _cfg.SavePath);
                return _cfg.SavePath;
            }

            string gameFolder = LocateGameFolder();
            if (gameFolder == null) { Err("已取消。"); return null; }

            string savePath = ResolveSavePath(gameFolder);
            if (savePath == null)
            {
                Err("没有找到第二章存档 " + SaveFileName + "。");
                Info("请先启动一次游戏, 进入第 2 章并玩到有自动存档的位置, 然后再运行本工具。");
                Info("预期位置: " + Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    SaveProjectName, @"Saved\SaveGames", SaveFileName));
                return null;
            }
            return savePath;
        }

        private static void RememberSavePath(string savePath)
        {
            if (string.IsNullOrEmpty(savePath)) return;
            string dir = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(dir) && !string.Equals(_cfg.SavePath, savePath, StringComparison.OrdinalIgnoreCase))
            {
                _cfg.SavePath = savePath;
                _cfg.Save();
            }
        }

        private static string DetectGameFolder()
        {
            string baseDir = _exeDir.TrimEnd('\\');
            List<string> seeds = new List<string>();
            string cur = baseDir;
            for (int i = 0; i < 4 && !string.IsNullOrEmpty(cur); i++)
            {
                seeds.Add(cur);
                DirectoryInfo parent = Directory.GetParent(cur);
                cur = parent == null ? null : parent.FullName;
            }

            List<string> candidates = new List<string>();
            foreach (string seed in seeds)
            {
                candidates.Add(seed);
                try
                {
                    foreach (string sub in Directory.GetDirectories(seed))
                    {
                        string name = Path.GetFileName(sub);
                        if (name.IndexOf("poppy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("playtime", StringComparison.OrdinalIgnoreCase) >= 0)
                            candidates.Add(sub);
                    }
                }
                catch { }
            }

            foreach (string c in candidates)
                if (LooksLikeGameFolder(c)) return c;

            return null;
        }

        private static bool LooksLikeGameFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return false;
            string[] markers =
            {
                @"WindowsNoEditor\Playtime_Prototype4.exe",
                @"WindowsNoEditor\Poppy_Playtime.exe",
                @"PlaytimeLauncher\PlaytimeLauncher.exe",
                @"PoppyPlaytime_Chapter3\Playtime_Chapter3.exe",
                @"Playtime_Chapter4\Playtime_Chapter4.exe",
                @"Chapter5\ch5_pro.exe",
            };
            foreach (string m in markers)
                if (File.Exists(Path.Combine(path, m))) return true;
            return false;
        }

        /// <summary>返回游戏文件夹；null = 用户取消；空字符串 = 使用默认存档位置。</summary>
        private static string AskGameFolder()
        {
            Console.WriteLine();
            Line(ConsoleColor.White, "  请输入游戏文件夹路径 (整行输入, 可直接整段粘贴):");
            Line(ConsoleColor.DarkGray, @"     例如  D:\SteamLibrary\steamapps\common\Poppy Playtime");
            Line(ConsoleColor.DarkGray, "     提示  路径带空格没关系 (Steam 默认的 \"Poppy Playtime\" 就带空格),");
            Line(ConsoleColor.DarkGray, "           引号会自动去掉; 也支持 %LOCALAPPDATA% 这类环境变量。");
            Line(ConsoleColor.DarkGray, "     直接回车 = 使用默认存档位置。");
            Console.WriteLine();

            for (int attempt = 1; attempt <= 3; attempt++)
            {
                Console.Write("     路径" + (attempt > 1 ? " (第 " + attempt + " 次)" : "") + ": ");
                string input = Console.ReadLine();
                if (input == null) return null;

                input = CleanPath(input);
                if (input.Length == 0) return string.Empty;

                if (!Directory.Exists(input))
                {
                    Err("这个路径不存在: " + input);
                    continue;
                }

                if (!LooksLikeGameFolder(input))
                {
                    Warn("这个文件夹里没找到 Poppy Playtime 的游戏文件。");
                    Warn("(正常应该有 WindowsNoEditor\\Playtime_Prototype4.exe 或 PlaytimeLauncher\\PlaytimeLauncher.exe)");
                    Console.Write("     仍然使用这个文件夹吗? [y/N]: ");
                    string a = (Console.ReadLine() ?? string.Empty).Trim().ToLowerInvariant();
                    if (a != "y" && a != "yes") continue;
                }
                Info("这个路径会写进配置文件: " + _configPath);
                return input;
            }
            return null;
        }

        private static string CleanPath(string raw)
        {
            if (raw == null) return string.Empty;
            string s = raw.Trim();
            while (s.Length >= 2 &&
                   ((s.StartsWith("\"") && s.EndsWith("\"")) || (s.StartsWith("'") && s.EndsWith("'"))))
            {
                s = s.Substring(1, s.Length - 2).Trim();
            }
            s = s.TrimEnd('\\', '/');
            try { s = Environment.ExpandEnvironmentVariables(s); } catch { }
            return s;
        }

        private static string ResolveSavePath(string gameFolder)
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            List<string> candidates = new List<string>();
            if (!string.IsNullOrEmpty(gameFolder))
            {
                candidates.Add(Path.Combine(gameFolder, "Saved", "SaveGames", SaveFileName));
                candidates.Add(Path.Combine(gameFolder, SaveFileName));
            }
            candidates.Add(Path.Combine(local, SaveProjectName, "Saved", "SaveGames", SaveFileName));

            foreach (string c in candidates)
                if (File.Exists(c)) return c;

            try
            {
                foreach (string dir in Directory.GetDirectories(local))
                {
                    string name = Path.GetFileName(dir);
                    if (name.IndexOf("playtime", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    string p = Path.Combine(dir, "Saved", "SaveGames", SaveFileName);
                    if (File.Exists(p)) return p;
                }
            }
            catch { }

            return null;
        }

        // =====================================================================
        //  backups
        // =====================================================================
        private static string BackupRoot()
        {
            if (!string.IsNullOrEmpty(_cfg.BackupDir)) return _cfg.BackupDir;
            return Path.Combine(_exeDir, BackupFolderName);
        }

        /// <summary>把存档所在目录里所有 .sav 整份复制到 <备份根>\时间戳[-标签]\，返回该目录，失败返回 null。</summary>
        private static string CreateBackup(string savePath, string tag)
        {
            string dirName = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(tag)) dirName += "-" + tag;

            List<string> roots = new List<string>();
            roots.Add(BackupRoot());
            roots.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), BackupFolderName));

            foreach (string root in roots)
            {
                string dir = Path.Combine(root, dirName);
                try
                {
                    Directory.CreateDirectory(dir);
                    string srcDir = Path.GetDirectoryName(savePath);
                    int copied = 0;
                    foreach (string f in Directory.GetFiles(srcDir, "*.sav"))
                    {
                        File.Copy(f, Path.Combine(dir, Path.GetFileName(f)), true);
                        copied++;
                    }
                    if (copied == 0) throw new IOException("源目录里没有 .sav 文件");
                    return dir;
                }
                catch (Exception ex)
                {
                    Warn("备份到 " + dir + " 失败: " + ex.Message);
                }
            }
            return null;
        }

        private static void ShowBackupContent(string backupDir)
        {
            try
            {
                foreach (string f in Directory.GetFiles(backupDir, "*.sav"))
                {
                    FileInfo fi = new FileInfo(f);
                    Info("   " + Path.GetFileName(f) + "   " + fi.Length + " 字节");
                }
            }
            catch { }
        }

        private static List<string> ListBackups()
        {
            List<string> list = new List<string>();
            string root = BackupRoot();
            if (!Directory.Exists(root)) return list;
            try
            {
                foreach (string d in Directory.GetDirectories(root))
                    if (FindCheckpointSaveIn(d) != null) list.Add(d);
            }
            catch { }
            list.Sort(StringComparer.OrdinalIgnoreCase);
            list.Reverse();
            return list;
        }

        /// <summary>在某个目录里找出第二章检查点存档：优先 Chap2Checkpoint.sav，找不到就按内容识别。</summary>
        private static string FindCheckpointSaveIn(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return null;

            string p = Path.Combine(dir, SaveFileName);
            if (File.Exists(p)) return p;

            try
            {
                foreach (string f in Directory.GetFiles(dir, "*.sav"))
                {
                    int v, off;
                    string err;
                    if (TryReadCheckpoint(f, out v, out off, out err)) return f;
                }
            }
            catch { }
            return null;
        }

        private static bool ChooseBackup(out string chosen)
        {
            chosen = null;
            List<string> backups = ListBackups();

            if (backups.Count == 0)
            {
                Err("还没有任何备份。备份根目录: " + BackupRoot());
                Info("请先运行 [2] 强制备份, 或先执行一次 [1] 跳过打地鼠 (它会自动备份)。");
                return false;
            }

            if (Console.IsInputRedirected)
            {
                // 非交互场景: 直接用最新的一份
                chosen = backups[0];
                Info("(输入被重定向) 自动选择最新备份: " + Path.GetFileName(chosen));
                return true;
            }

            Line(ConsoleColor.White, "  找到 " + backups.Count + " 份备份 (备份根目录: " + BackupRoot() + ")");
            Console.WriteLine();
            for (int i = 0; i < backups.Count; i++)
            {
                string name = Path.GetFileName(backups[i]);
                int v = -1, off; string err;
                string saveInBackup = FindCheckpointSaveIn(backups[i]);
                if (saveInBackup != null) TryReadCheckpoint(saveInBackup, out v, out off, out err);
                int fileCount = 0;
                try { fileCount = Directory.GetFiles(backups[i], "*.sav").Length; } catch { }

                string desc = v >= 0 ? ("检查点 " + v + " / " + NameOf(v)) : "检查点未知";
                Line(i == 0 ? ConsoleColor.White : ConsoleColor.Gray,
                     "    [" + (i + 1) + "] " + name + "   " + desc + "   (" + fileCount + " 个文件)");
            }
            Console.WriteLine();
            Line(ConsoleColor.DarkGray, "    直接回车 = 最新一份;   m = 手动输入备份文件路径;   q = 取消");

            while (true)
            {
                Console.Write("     请选择: ");
                string input = (Console.ReadLine() ?? string.Empty).Trim();

                if (input.Length == 0) { chosen = backups[0]; break; }
                if (input.Equals("q", StringComparison.OrdinalIgnoreCase)) return false;
                if (input.Equals("m", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Write("     备份文件 (.sav) 路径: ");
                    string manual = CleanPath(Console.ReadLine());
                    if (manual.Length == 0) continue;
                    if (!File.Exists(manual)) { Err("文件不存在: " + manual); continue; }
                    string dir = Path.GetDirectoryName(manual);
                    if (!File.Exists(Path.Combine(dir, SaveFileName)))
                    {
                        Warn("这个目录里没有 " + SaveFileName + ", 仍然使用该目录作为备份来源吗?");
                        Console.Write("     [y/N]: ");
                        string a = (Console.ReadLine() ?? string.Empty).Trim().ToLowerInvariant();
                        if (a != "y" && a != "yes") continue;
                    }
                    chosen = dir;
                    break;
                }

                int idx;
                if (int.TryParse(input, out idx) && idx >= 1 && idx <= backups.Count)
                {
                    chosen = backups[idx - 1];
                    break;
                }
                Err("输入无效, 请输入 1-" + backups.Count + "、回车、m 或 q。");
            }

            Ok("已选择: " + Path.GetFileName(chosen));
            return true;
        }

        // =====================================================================
        //  GVAS save editing
        // =====================================================================
        private static bool TryReadCheckpoint(string path, out int value, out int offset, out string error)
        {
            value = 0;
            offset = -1;
            error = null;
            try
            {
                if (!File.Exists(path)) { error = "文件不存在"; return false; }
                byte[] data = File.ReadAllBytes(path);
                if (data.Length < 64 || Encoding.ASCII.GetString(data, 0, 4) != "GVAS")
                {
                    error = "不是 UE 的 SaveGame 文件 (缺少 GVAS 文件头)";
                    return false;
                }
                if (!ContainsAscii(data, SaveClassMarker))
                {
                    error = "这不是第二章的检查点存档 (找不到 " + SaveClassMarker + ")";
                    return false;
                }
                if (!TryFindCheckpointOffset(data, out offset))
                {
                    error = "存档里没有找到 Checkpoint 属性, 游戏版本可能不兼容";
                    return false;
                }
                value = BitConverter.ToInt32(data, offset);
                if (value < 0 || value > 500)
                {
                    error = "Checkpoint 数值异常: " + value;
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryFindCheckpointOffset(byte[] data, out int offset)
        {
            offset = -1;
            byte[] needle = Encoding.ASCII.GetBytes("Checkpoint\0");

            for (int i = 4; i <= data.Length - needle.Length - 40; i++)
            {
                bool hit = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (data[i + j] != needle[j]) { hit = false; break; }
                }
                if (!hit) continue;

                if (BitConverter.ToInt32(data, i - 4) != 11) continue;            // "Checkpoint" 长度
                if (BitConverter.ToInt32(data, i + 11) != 12) continue;           // "IntProperty" 长度
                if (Encoding.ASCII.GetString(data, i + 15, 11) != "IntProperty") continue;

                // name(len+str) + type(len+str) + size + arrayIndex + hasPropertyGuid
                int valOff = i + 11 + 4 + 12 + 4 + 4 + 1;
                if (valOff + 4 > data.Length) continue;

                int candidate = BitConverter.ToInt32(data, valOff);
                if (candidate < 0 || candidate > 500) continue;

                offset = valOff;
                return true;
            }
            return false;
        }

        private static bool TryWriteCheckpoint(string path, int newValue, out string error)
        {
            error = null;
            try
            {
                byte[] data = File.ReadAllBytes(path);
                int offset;
                if (!TryFindCheckpointOffset(data, out offset))
                {
                    error = "存档里找不到 Checkpoint 属性";
                    return false;
                }

                FileAttributes attrs = File.GetAttributes(path);
                bool wasReadOnly = (attrs & FileAttributes.ReadOnly) != 0;
                if (wasReadOnly) File.SetAttributes(path, attrs & ~FileAttributes.ReadOnly);

                try
                {
                    using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
                    {
                        fs.Seek(offset, SeekOrigin.Begin);
                        fs.Write(BitConverter.GetBytes(newValue), 0, 4);
                        fs.Flush(true);
                    }
                }
                finally
                {
                    if (wasReadOnly) { try { File.SetAttributes(path, attrs); } catch { } }
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static void OfferElevate(string savePath)
        {
            if (Console.IsInputRedirected) return;
            Console.WriteLine();
            Console.Write("  要以管理员身份重新尝试一次吗? [y/N]: ");
            string a = (Console.ReadLine() ?? string.Empty).Trim().ToLowerInvariant();
            if (a != "y" && a != "yes") return;

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(_exePath, "--save \"" + savePath + "\" --auto");
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                Process.Start(psi);
                Line(ConsoleColor.DarkGray, "  已在新窗口以管理员身份启动, 请在那里完成操作。");
            }
            catch (Exception ex)
            {
                Err("提权失败 (可能你点了\"否\"): " + ex.Message);
            }
        }

        // =====================================================================
        //  author promotion (every exit goes through here)
        // =====================================================================
        private static void AuthorExit()
        {
            if (_authorDone) return;
            _authorDone = true;

            Console.WriteLine();
            Line(ConsoleColor.DarkGray, "  " + new string('=', 64));
            Line(ConsoleColor.White, "    作者: " + Author + "   " + DefaultGitHubUrl);
            Line(ConsoleColor.DarkGray, "  " + new string('=', 64));

            if (!_cfg.OpenGitHubAfterDone)
            {
                Line(ConsoleColor.DarkGray, "  (配置 OpenGitHubAfterDone = false, 未打开浏览器)");
                return;
            }
            if (Console.IsInputRedirected)
            {
                Line(ConsoleColor.DarkGray, "  (输入已重定向, 跳过自动打开浏览器)");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(_cfg.GitHubUrl) { UseShellExecute = true });
                Info("已打开作者的 GitHub: " + _cfg.GitHubUrl);
            }
            catch (Exception ex)
            {
                Warn("打开浏览器失败 (" + ex.Message + "), 请手动访问: " + _cfg.GitHubUrl);
            }
        }

        // =====================================================================
        //  misc helpers
        // =====================================================================
        private static string NameOf(int value)
        {
            string s;
            return Checkpoints.TryGetValue(value, out s) ? s : "未知检查点";
        }

        private static bool ContainsAscii(byte[] data, string text)
        {
            byte[] n = Encoding.ASCII.GetBytes(text);
            for (int i = 0; i <= data.Length - n.Length; i++)
            {
                bool hit = true;
                for (int j = 0; j < n.Length; j++)
                {
                    if (data[i + j] != n[j]) { hit = false; break; }
                }
                if (hit) return true;
            }
            return false;
        }

        // =====================================================================
        //  configuration (PoppyCh2Skip.json)
        // =====================================================================
        private sealed class AppConfig
        {
            public string GamePath = "";
            public string SavePath = "";
            public string BackupDir = "";
            public int TargetCheckpoint = DefaultTargetCheckpoint;
            public bool OpenGitHubAfterDone = true;
            public bool KillGameWithoutAsking = false;
            public string GitHubUrl = DefaultGitHubUrl;

            public static AppConfig Load(string path)
            {
                AppConfig c = new AppConfig();
                try
                {
                    if (!File.Exists(path)) { c.Save(); return c; }

                    string text = File.ReadAllText(path, Encoding.UTF8);
                    Dictionary<string, object> root = MiniJson.ParseObject(text);
                    if (root == null) return c;

                    c.GamePath = MiniJson.GetString(root, "GamePath", c.GamePath);
                    c.SavePath = MiniJson.GetString(root, "SavePath", c.SavePath);
                    c.BackupDir = MiniJson.GetString(root, "BackupDir", c.BackupDir);
                    c.TargetCheckpoint = MiniJson.GetInt(root, "TargetCheckpoint", c.TargetCheckpoint);
                    c.OpenGitHubAfterDone = MiniJson.GetBool(root, "OpenGitHubAfterDone", c.OpenGitHubAfterDone);
                    c.KillGameWithoutAsking = MiniJson.GetBool(root, "KillGameWithoutAsking", c.KillGameWithoutAsking);
                    c.GitHubUrl = MiniJson.GetString(root, "GitHubUrl", c.GitHubUrl);
                    if (string.IsNullOrEmpty(c.GitHubUrl)) c.GitHubUrl = DefaultGitHubUrl;
                    if (c.TargetCheckpoint < 0 || c.TargetCheckpoint > 500) c.TargetCheckpoint = DefaultTargetCheckpoint;
                }
                catch { }
                return c;
            }

            public void Save()
            {
                try
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("{");
                    sb.AppendLine("  \"_comment\": [");
                    sb.AppendLine("    \"Poppy Playtime 第二章 打地鼠跳过工具 / 配置文件  (Kernix - https://github.com/Linuxoid-cn)\",");
                    sb.AppendLine("    \"GamePath            : 游戏文件夹. 例如 D:\\\\SteamLibrary\\\\steamapps\\\\common\\\\Poppy Playtime; 留空则自动检测或询问\",");
                    sb.AppendLine("    \"SavePath            : 第二章存档完整路径; 留空则按默认 %LOCALAPPDATA%\\\\Playtime_Prototype4\\\\Saved\\\\SaveGames 推断\",");
                    sb.AppendLine("    \"BackupDir           : 备份根目录; 留空则用 exe 同级的 PoppyCh2Skip_Backups\",");
                    sb.AppendLine("    \"TargetCheckpoint    : 跳过打地鼠后写入的检查点值, 默认 18 (Whack-A-Wuggy End)\",");
                    sb.AppendLine("    \"OpenGitHubAfterDone : 操作成功后是否自动打开作者 GitHub, true/false\",");
                    sb.AppendLine("    \"KillGameWithoutAsking: 游戏运行时是否不询问直接强制关闭, true/false (默认 false)\",");
                    sb.AppendLine("    \"GitHubUrl           : 要打开的地址, 一般不用改\"");
                    sb.AppendLine("  ],");
                    sb.AppendLine("  \"GamePath\": " + J(GamePath) + ",");
                    sb.AppendLine("  \"SavePath\": " + J(SavePath) + ",");
                    sb.AppendLine("  \"BackupDir\": " + J(BackupDir) + ",");
                    sb.AppendLine("  \"TargetCheckpoint\": " + TargetCheckpoint + ",");
                    sb.AppendLine("  \"OpenGitHubAfterDone\": " + (OpenGitHubAfterDone ? "true" : "false") + ",");
                    sb.AppendLine("  \"KillGameWithoutAsking\": " + (KillGameWithoutAsking ? "true" : "false") + ",");
                    sb.AppendLine("  \"GitHubUrl\": " + J(GitHubUrl));
                    sb.AppendLine("}");

                    File.WriteAllText(_configPath, sb.ToString(), new UTF8Encoding(false));
                }
                catch { }
            }

            private static string J(string s)
            {
                if (s == null) return "\"\"";
                StringBuilder sb = new StringBuilder("\"");
                foreach (char c in s)
                {
                    switch (c)
                    {
                        case '\\': sb.Append("\\\\"); break;
                        case '"': sb.Append("\\\""); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        default:
                            if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                            else sb.Append(c);
                            break;
                    }
                }
                return sb.Append('"').ToString();
            }
        }

        /// <summary>极简 JSON 解析器: 只支持本工具配置需要的对象/数组/字符串/数字/布尔/null。</summary>
        private static class MiniJson
        {
            public static Dictionary<string, object> ParseObject(string text)
            {
                int i = 0;
                object v = ParseValue(text, ref i);
                return v as Dictionary<string, object>;
            }

            public static string GetString(Dictionary<string, object> o, string key, string def)
            {
                object v;
                if (o != null && o.TryGetValue(key, out v) && v is string) return (string)v;
                return def;
            }

            public static int GetInt(Dictionary<string, object> o, string key, int def)
            {
                object v;
                if (o != null && o.TryGetValue(key, out v))
                {
                    if (v is double) return (int)(double)v;
                    if (v is string) { int n; if (int.TryParse((string)v, out n)) return n; }
                }
                return def;
            }

            public static bool GetBool(Dictionary<string, object> o, string key, bool def)
            {
                object v;
                if (o != null && o.TryGetValue(key, out v))
                {
                    if (v is bool) return (bool)v;
                    if (v is string)
                    {
                        string s = ((string)v).Trim().ToLowerInvariant();
                        if (s == "true" || s == "1" || s == "yes") return true;
                        if (s == "false" || s == "0" || s == "no") return false;
                    }
                }
                return def;
            }

            private static object ParseValue(string s, ref int i)
            {
                SkipWs(s, ref i);
                if (i >= s.Length) return null;

                char c = s[i];
                if (c == '{') return ParseObj(s, ref i);
                if (c == '[') return ParseArr(s, ref i);
                if (c == '"') return ParseStr(s, ref i);
                if (matches(s, i, "true")) { i += 4; return true; }
                if (matches(s, i, "false")) { i += 5; return false; }
                if (matches(s, i, "null")) { i += 4; return null; }
                return ParseNum(s, ref i);
            }

            private static Dictionary<string, object> ParseObj(string s, ref int i)
            {
                Dictionary<string, object> o = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                i++; // {
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == '}') { i++; return o; }

                while (i < s.Length)
                {
                    SkipWs(s, ref i);
                    string key = ParseStr(s, ref i);
                    SkipWs(s, ref i);
                    if (i < s.Length && s[i] == ':') i++;
                    object val = ParseValue(s, ref i);
                    if (key != null) o[key] = val;
                    SkipWs(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == '}') { i++; break; }
                    break;
                }
                return o;
            }

            private static List<object> ParseArr(string s, ref int i)
            {
                List<object> list = new List<object>();
                i++; // [
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ']') { i++; return list; }
                while (i < s.Length)
                {
                    list.Add(ParseValue(s, ref i));
                    SkipWs(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == ']') { i++; break; }
                    break;
                }
                return list;
            }

            private static string ParseStr(string s, ref int i)
            {
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != '"') return null;
                i++;
                StringBuilder sb = new StringBuilder();
                while (i < s.Length)
                {
                    char c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }

                    if (i >= s.Length) break;
                    char e = s[i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 <= s.Length)
                            {
                                int code;
                                if (int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                                {
                                    sb.Append((char)code);
                                    i += 4;
                                }
                            }
                            break;
                        default: sb.Append(e); break;
                    }
                }
                return sb.ToString();
            }

            private static double ParseNum(string s, ref int i)
            {
                int start = i;
                while (i < s.Length && ("+-0123456789.eE".IndexOf(s[i]) >= 0)) i++;
                double d = 0;
                double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out d);
                return d;
            }

            private static void SkipWs(string s, ref int i)
            {
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            }

            private static bool matches(string s, int i, string lit)
            {
                if (i + lit.Length > s.Length) return false;
                return string.Compare(s, i, lit, 0, lit.Length, StringComparison.OrdinalIgnoreCase) == 0;
            }
        }
    }
}
