using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace WinForge
{
    public partial class MainWindow : Window
    {
        const string Version = "2.8.0";
        // Where WinForge checks for newer releases (change if the repo is renamed).
        const string RepoOwner = "mn9-29";
        const string RepoName = "WinForge";

        string _filter = "";
        bool _dark = true;
        string _logFile;
        string _latestUrl;
        readonly List<ICollectionView> _views = new List<ICollectionView>();

        // ---- live RAM meter (GlobalMemoryStatusEx) ----
        [StructLayout(LayoutKind.Sequential)]
        class MemoryStatusEx
        {
            public uint dwLength = (uint)Marshal.SizeOf(typeof(MemoryStatusEx));
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx lpBuffer);

        [DllImport("kernel32.dll")]
        static extern ulong GetTickCount64();

        DispatcherTimer _ramTimer;
        PerformanceCounter _cpuCounter;

        readonly List<TweakItem> _all = new List<TweakItem>();
        readonly ObservableCollection<TweakItem> _apps = new ObservableCollection<TweakItem>();
        readonly ObservableCollection<TweakItem> _scan = new ObservableCollection<TweakItem>();
        readonly ObservableCollection<TweakItem> _privacy = new ObservableCollection<TweakItem>();
        readonly ObservableCollection<TweakItem> _services = new ObservableCollection<TweakItem>();
        readonly ObservableCollection<TweakItem> _gaming = new ObservableCollection<TweakItem>();
        readonly ObservableCollection<TweakItem> _perf = new ObservableCollection<TweakItem>();
        readonly ObservableCollection<TweakItem> _network = new ObservableCollection<TweakItem>();
        readonly ObservableCollection<TweakItem> _updates = new ObservableCollection<TweakItem>();
        readonly ObservableCollection<TweakItem> _ui = new ObservableCollection<TweakItem>();
        readonly ObservableCollection<TweakItem> _cleanup = new ObservableCollection<TweakItem>();
        readonly ObservableCollection<TweakItem> _system = new ObservableCollection<TweakItem>();
        readonly ObservableCollection<TweakItem> _install = new ObservableCollection<TweakItem>();

        public MainWindow()
        {
            InitializeComponent();
            lblVersion.Text = "v" + Version;
            try
            {
                _logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "winforge_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log");
            }
            catch { _logFile = null; }
            BuildCatalog();
            icApps.ItemsSource = _apps;
            icScan.ItemsSource = _scan;
            icPrivacy.ItemsSource = _privacy;
            icServices.ItemsSource = _services;
            icGaming.ItemsSource = _gaming;
            icPerf.ItemsSource = _perf;
            icNetwork.ItemsSource = _network;
            icUpdates.ItemsSource = _updates;
            icUi.ItemsSource = _ui;
            icCleanup.ItemsSource = _cleanup;
            icSystem.ItemsSource = _system;
            icInstall.ItemsSource = _install;
            SetupFilters();
            ApplyPreset("WORK");

            try { _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"); _cpuCounter.NextValue(); }
            catch { _cpuCounter = null; }

            InitDashboardStatic();
            UpdateDashboard();

            _ramTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _ramTimer.Tick += (s, e) => { UpdateRam(); UpdateDashboard(); };
            _ramTimer.Start();
            UpdateRam();

            // Quietly check GitHub for a newer release (never blocks the UI).
            _ = CheckForUpdatesAsync(false);
        }

        // ----------------------------------------------------------- search filter
        void SetupFilters()
        {
            foreach (var c in new[] { _apps, _scan, _privacy, _services, _gaming, _perf, _network, _updates, _ui, _cleanup, _system, _install })
            {
                var v = CollectionViewSource.GetDefaultView(c);
                v.Filter = o =>
                {
                    if (string.IsNullOrEmpty(_filter)) return true;
                    var it = (TweakItem)o;
                    return (it.Title != null && it.Title.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0)
                        || (it.Description != null && it.Description.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0);
                };
                _views.Add(v);
            }
        }

        void txtSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            _filter = txtSearch.Text.Trim();
            foreach (var v in _views) v.Refresh();
        }

        // ------------------------------------------------- select all / none (tab)
        // Finds the checkbox list shown on the active tab by reading the
        // ItemsSource of the ItemsControl inside it (no brittle tab indices).
        ObservableCollection<TweakItem> ActiveCollection()
        {
            var content = tabs.SelectedContent as DependencyObject;
            var ic = FindItemsControl(content);
            return ic?.ItemsSource as ObservableCollection<TweakItem>;
        }

        static ItemsControl FindItemsControl(DependencyObject root)
        {
            if (root == null) return null;
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                // A TabControl is itself an ItemsControl, so only accept the
                // plain list controls that carry our TweakItem collections.
                if (child is ItemsControl ic && ic.ItemsSource is ObservableCollection<TweakItem>)
                    return ic;
                var deeper = FindItemsControl(child);
                if (deeper != null) return deeper;
            }
            return null;
        }

        void SetVisibleSelection(bool sel)
        {
            var coll = ActiveCollection();
            if (coll == null) return;
            var view = CollectionViewSource.GetDefaultView(coll);
            foreach (var o in view) ((TweakItem)o).IsSelected = sel; // iterates filtered items only
        }

        // ------------------------------------------------------------ device modes
        async void btnModeGaming_Click(object sender, RoutedEventArgs e) => await RunMode("Gaming mode", new[]
        {
            ("Power plan: High Performance", "powercfg -setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"),
            ("Enable Game Mode", "reg add \"HKCU\\SOFTWARE\\Microsoft\\GameBar\" /v AllowAutoGameMode /t REG_DWORD /d 1 /f & reg add \"HKCU\\SOFTWARE\\Microsoft\\GameBar\" /v AutoGameModeEnabled /t REG_DWORD /d 1 /f"),
            ("Stop Docker Desktop", "taskkill /f /im \"Docker Desktop.exe\" /im com.docker.backend.exe /im com.docker.service 2>nul"),
            ("Shut down WSL (frees RAM)", "wsl --shutdown"),
            ("Close dev/comms apps", "taskkill /f /im Code.exe /im devenv.exe /im GitHubDesktop.exe 2>nul"),
        });

        async void btnModeDev_Click(object sender, RoutedEventArgs e) => await RunMode("Programming mode", new[]
        {
            ("Power plan: High Performance", "powercfg -setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"),
            ("Close gaming apps", "taskkill /f /im steam.exe /im Discord.exe /im EpicGamesLauncher.exe /im EADesktop.exe /im RiotClientServices.exe 2>nul"),
            ("Start Docker Desktop (starts WSL)", "start \"\" \"%ProgramFiles%\\Docker\\Docker\\Docker Desktop.exe\""),
        });

        async void btnModeOffice_Click(object sender, RoutedEventArgs e) => await RunMode("Office mode", new[]
        {
            ("Power plan: Balanced", "powercfg -setactive 381b4222-f694-41f0-9685-ff5bb260df2e"),
            ("Close gaming apps", "taskkill /f /im steam.exe /im Discord.exe /im EpicGamesLauncher.exe /im EADesktop.exe 2>nul"),
            ("Stop Docker Desktop", "taskkill /f /im \"Docker Desktop.exe\" /im com.docker.backend.exe 2>nul"),
            ("Shut down WSL (frees RAM)", "wsl --shutdown"),
        });

        async Task RunMode(string title, (string label, string cmd)[] actions)
        {
            string list = string.Join(Environment.NewLine, actions.Select(a => "  - " + a.label));
            var confirm = MessageBox.Show(
                "Activate " + title + "?" + Environment.NewLine + Environment.NewLine + list +
                Environment.NewLine + Environment.NewLine +
                "This starts/stops apps & services (nothing is deleted). Save open work first.",
                title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            SetBusy(true);
            txtLog.Clear();
            Log("=== Activating " + title + " ===");
            if (_logFile != null) Log("Log file: " + _logFile);
            int total = actions.Length, done = 0;
            await Task.Run(() =>
            {
                foreach (var a in actions)
                {
                    Log("» " + a.label);
                    RunCmd(a.cmd);
                    SetProgress(++done, total, title + " " + done + "/" + total);
                }
                Log("");
                Log("=== " + title + " is now active. ===");
            });
            SetProgress(total, total, title + " active");
            SetBusy(false);
        }

        void btnSelAll_Click(object sender, RoutedEventArgs e) => SetVisibleSelection(true);
        void btnSelNone_Click(object sender, RoutedEventArgs e) => SetVisibleSelection(false);

        // ------------------------------------------------------------- theme toggle
        void btnTheme_Click(object sender, RoutedEventArgs e)
        {
            _dark = !_dark;
            ApplyTheme();
        }

        void ApplyTheme()
        {
            if (_dark)
            {
                SetBrush("BgBrush", "#1E1E1E"); SetBrush("PanelBrush", "#252526");
                SetBrush("FgBrush", "#EEEEEE"); SetBrush("FgDimBrush", "#999999");
                SetBrush("SepBrush", "#33FFFFFF");
                btnTheme.Content = "Light theme";
            }
            else
            {
                SetBrush("BgBrush", "#F3F3F3"); SetBrush("PanelBrush", "#FFFFFF");
                SetBrush("FgBrush", "#1A1A1A"); SetBrush("FgDimBrush", "#666666");
                SetBrush("SepBrush", "#22000000");
                btnTheme.Content = "Dark theme";
            }
        }

        void SetBrush(string key, string hex)
        {
            Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }

        // ---------------------------------------------------------- app sizes (log)
        async void btnSizes_Click(object sender, RoutedEventArgs e)
        {
            SetBusy(true);
            txtLog.Clear();
            Log("» Calculating installed app sizes (this can take a minute)...");
            await Task.Run(() => RunCmd(
                "powershell -NoProfile -Command \"Get-AppxPackage | Select-Object Name, " +
                "@{N='SizeMB';E={[math]::Round((Get-ChildItem $_.InstallLocation -Recurse -ErrorAction SilentlyContinue | " +
                "Measure-Object Length -Sum).Sum/1MB,1)}} | Sort-Object SizeMB -Descending | " +
                "Format-Table -AutoSize | Out-String -Width 200\""));
            Log("=== Done. Bigger apps are listed first. ===");
            SetBusy(false);
        }

        void UpdateRam()
        {
            var m = new MemoryStatusEx();
            if (!GlobalMemoryStatusEx(m)) return;
            double totalGB = m.ullTotalPhys / 1073741824.0;
            double usedGB = (m.ullTotalPhys - m.ullAvailPhys) / 1073741824.0;
            lblRam.Text = string.Format("RAM: {0:0.0} / {1:0.0} GB  ({2}%)", usedGB, totalGB, m.dwMemoryLoad);
            lblRam.Foreground = m.dwMemoryLoad >= 85 ? Brushes.OrangeRed
                              : m.dwMemoryLoad >= 70 ? Brushes.Orange
                              : Brushes.LightGreen;
        }

        async void btnExplorer_Click(object sender, RoutedEventArgs e)
        {
            SetBusy(true);
            txtLog.Clear();
            Log("» Restarting Windows Explorer...");
            await Task.Run(() => RunCmd("taskkill /f /im explorer.exe & start explorer.exe"));
            Log("=== Explorer restarted. ===");
            SetBusy(false);
        }

        // =============================================================== Dashboard
        // Reads things that don't change while the app is open (OS/CPU/GPU/host).
        void InitDashboardStatic()
        {
            dbHost.Text = Environment.MachineName + "  \\  " + Environment.UserName;

            string product = RegRead(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName", "Windows");
            string disp = RegRead(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DisplayVersion", "");
            string build = RegRead(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "CurrentBuild", "");
            string ubr = RegRead(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "UBR", "");
            // Windows 11 still reports "Windows 10" in ProductName; correct it by build.
            int buildNum; if (int.TryParse(build, out buildNum) && buildNum >= 22000)
                product = product.Replace("Windows 10", "Windows 11");
            string ver = product;
            if (!string.IsNullOrEmpty(disp)) ver += "  " + disp;
            if (!string.IsNullOrEmpty(build)) ver += "  (build " + build + (string.IsNullOrEmpty(ubr) ? "" : "." + ubr) + ")";
            dbOs.Text = ver;

            dbCpu.Text = RegRead(Registry.LocalMachine, @"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString", "-").Trim();
            dbGpu.Text = RegRead(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000", "DriverDesc", "-");
        }

        // Refreshes the live numbers (called on the 2s timer).
        void UpdateDashboard()
        {
            if (dbRam == null) return; // not built yet
            try
            {
                if (_cpuCounter != null)
                {
                    int load = (int)Math.Round(_cpuCounter.NextValue());
                    dbCpuUse.Text = load + " %";
                }

                var m = new MemoryStatusEx();
                if (GlobalMemoryStatusEx(m))
                {
                    double totalGB = m.ullTotalPhys / 1073741824.0;
                    double usedGB = (m.ullTotalPhys - m.ullAvailPhys) / 1073741824.0;
                    dbRam.Text = string.Format("{0:0.0} / {1:0.0} GB used ({2}%)", usedGB, totalGB, m.dwMemoryLoad);
                }

                try
                {
                    var c = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
                    double totGB = c.TotalSize / 1073741824.0;
                    double freeGB = c.AvailableFreeSpace / 1073741824.0;
                    double usedGBd = totGB - freeGB;
                    dbDisk.Text = string.Format("{0:0} / {1:0} GB used  ({2:0} GB free)", usedGBd, totGB, freeGB);
                }
                catch { }

                ulong ms = GetTickCount64();
                var up = TimeSpan.FromMilliseconds(ms);
                dbUptime.Text = string.Format("{0}d {1}h {2}m", up.Days, up.Hours, up.Minutes);

                dbFlags.Text = string.Join("\n", new[]
                {
                    Flag("Telemetry disabled",      RegReadInt(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry") == 0),
                    Flag("Consumer features off",   RegReadInt(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures") == 1),
                    Flag("Start suggestions off",   RegReadInt(Registry.CurrentUser,  @"SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled") == 0),
                    Flag("Cortana disabled",        RegReadInt(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana") == 0),
                    Flag("Game DVR off",            RegReadInt(Registry.CurrentUser,  @"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled") == 0),
                    Flag("Advertising ID off",      RegReadInt(Registry.CurrentUser,  @"SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled") == 0),
                });
            }
            catch { }
        }

        static string Flag(string label, bool on) => (on ? "[x] " : "[ ] ") + label;

        void btnDbRefresh_Click(object sender, RoutedEventArgs e) { InitDashboardStatic(); UpdateDashboard(); }

        static string RegRead(RegistryKey root, string path, string name, string fallback)
        {
            try { using (var k = root.OpenSubKey(path)) { var v = k?.GetValue(name); return v != null ? v.ToString() : fallback; } }
            catch { return fallback; }
        }

        // Returns the DWORD value, or -1 if the value is missing.
        static int RegReadInt(RegistryKey root, string path, string name)
        {
            try { using (var k = root.OpenSubKey(path)) { var v = k?.GetValue(name); return v is int i ? i : -1; } }
            catch { return -1; }
        }

        // =========================================================== installed scan
        async void btnScan_Click(object sender, RoutedEventArgs e)
        {
            SetBusy(true);
            lblScan.Text = "Scanning installed packages (sizes can take a minute)...";
            // Drop previously scanned items from the master list before rescanning.
            foreach (var old in _scan) _all.Remove(old);
            _scan.Clear();

            string output = null;
            await Task.Run(() => output = RunCmdCapture(
                "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Get-AppxPackage | " +
                "Where-Object { -not $_.IsFramework -and $_.NonRemovable -ne $true } | ForEach-Object { " +
                "$s=0; try { $s=[math]::Round((Get-ChildItem $_.InstallLocation -Recurse -ErrorAction SilentlyContinue | " +
                "Measure-Object Length -Sum).Sum/1MB,1) } catch {}; ('{0}|{1}' -f $_.Name,$s) }\""));

            int count = 0;
            if (!string.IsNullOrEmpty(output))
            {
                var rows = new List<KeyValuePair<string, double>>();
                foreach (var line in output.Split('\n'))
                {
                    var t = line.Trim();
                    int bar = t.IndexOf('|');
                    if (bar <= 0) continue;
                    string name = t.Substring(0, bar);
                    double mb; double.TryParse(t.Substring(bar + 1).Trim(),
                        System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out mb);
                    rows.Add(new KeyValuePair<string, double>(name, mb));
                }
                foreach (var r in rows.OrderByDescending(x => x.Value))
                {
                    string shortName = r.Key.Contains(".") ? r.Key.Substring(r.Key.LastIndexOf('.') + 1) : r.Key;
                    var it = new TweakItem
                    {
                        Title = shortName + (r.Value > 0 ? string.Format("  -  {0:0.#} MB", r.Value) : ""),
                        Description = r.Key,
                        Commands = new[] { RemoveAppx(r.Key) },
                        Work = false, Gaming = false, Basic = false
                    };
                    _scan.Add(it);
                    _all.Add(it);
                    count++;
                }
            }
            lblScan.Text = count > 0
                ? count + " removable packages found. Tick what you want gone, then APPLY SELECTED."
                : "No packages returned (try running as administrator).";
            SetBusy(false);
        }

        // Runs a command and returns its combined stdout/stderr (no logging).
        string RunCmdCapture(string command)
        {
            try
            {
                var psi = new ProcessStartInfo("cmd.exe", "/c " + command)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (var p = Process.Start(psi))
                {
                    string outp = p.StandardOutput.ReadToEnd();
                    string err = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    return (outp + err).Replace("\r\n", "\n");
                }
            }
            catch (Exception ex) { return "ERROR: " + ex.Message; }
        }

        // ============================================================ self-update
        void btnUpdates_Click(object sender, RoutedEventArgs e) => _ = CheckForUpdatesAsync(true);
        void btnUpdateLater_Click(object sender, RoutedEventArgs e) => barUpdate.Visibility = Visibility.Collapsed;
        void btnUpdateGet_Click(object sender, RoutedEventArgs e) =>
            OpenUrl(_latestUrl ?? "https://github.com/" + RepoOwner + "/" + RepoName + "/releases/latest");

        // Asks GitHub for the latest release tag and compares it to this build.
        // 'interactive' = show a popup with the result (button), else stay silent.
        async Task CheckForUpdatesAsync(bool interactive)
        {
            if (interactive) lblStatus.Text = "Checking for updates...";
            string tag = null, url = null;
            try
            {
                await Task.Run(() =>
                {
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                    using (var wc = new WebClient())
                    {
                        wc.Headers.Add("User-Agent", "WinForge");
                        string json = wc.DownloadString(
                            "https://api.github.com/repos/" + RepoOwner + "/" + RepoName + "/releases/latest");
                        var mt = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
                        if (mt.Success) tag = mt.Groups[1].Value;
                        var mu = Regex.Match(json, "\"html_url\"\\s*:\\s*\"([^\"]+)\"");
                        if (mu.Success) url = mu.Groups[1].Value;
                    }
                });
            }
            catch (Exception ex)
            {
                if (interactive) MessageBox.Show("Could not check for updates:\n" + ex.Message,
                    "WinForge", MessageBoxButton.OK, MessageBoxImage.Warning);
                lblStatus.Text = "Ready";
                return;
            }

            System.Version remote = ParseVer(tag), local = ParseVer(Version);
            if (remote != null && local != null && remote > local)
            {
                _latestUrl = url;
                lblUpdate.Text = "WinForge " + tag + " is available (you have v" + Version + ").";
                barUpdate.Visibility = Visibility.Visible;
                lblStatus.Text = "Update available: " + tag;
            }
            else
            {
                lblStatus.Text = "Ready";
                if (interactive) MessageBox.Show("You're on the latest version (v" + Version + ").",
                    "WinForge", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        static System.Version ParseVer(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            var m = Regex.Match(s, "[0-9]+(\\.[0-9]+){1,3}");
            System.Version v; return m.Success && System.Version.TryParse(m.Value, out v) ? v : null;
        }

        static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }

        // ---------------------------------------------------------- command helpers
        static string RemoveAppx(string name) =>
            "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Get-AppxPackage -AllUsers '" + name +
            "' | Remove-AppxPackage -ErrorAction SilentlyContinue; Get-AppxProvisionedPackage -Online | " +
            "Where-Object { $_.DisplayName -like '" + name + "' } | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue\"";

        static string RegDword(string path, string val, int data) =>
            "reg add \"" + path + "\" /v " + val + " /t REG_DWORD /d " + data + " /f";

        static string RegSz(string path, string val, string data) =>
            "reg add \"" + path + "\" /v " + val + " /t REG_SZ /d " + data + " /f";

        static string DisableSvc(string svc) =>
            "sc stop " + svc + " & sc config " + svc + " start= disabled";

        static string Winget(string id) =>
            "winget install -e --id " + id + " --silent --accept-package-agreements --accept-source-agreements";

        // Disable a startup entry by matching name pattern (uses the same
        // StartupApproved flag Task Manager uses, so it is reversible there).
        static string DisableStartup(string pattern) =>
            "powershell -NoProfile -ExecutionPolicy Bypass -Command \"$pat='" + pattern.Replace("'", "''") + "'; " +
            "$run='HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run'; " +
            "$appr='HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\StartupApproved\\Run'; " +
            "if(!(Test-Path $appr)){New-Item $appr -Force | Out-Null}; " +
            "if(Test-Path $run){ (Get-Item $run).Property | Where-Object { $_ -like $pat } | ForEach-Object { " +
            "New-ItemProperty -Path $appr -Name $_ -Value ([byte[]](3,0,0,0,0,0,0,0,0,0,0,0)) -PropertyType Binary -Force | Out-Null } }\"";

        // Writes %USERPROFILE%\.wslconfig (memory cap + auto memory reclaim).
        const string WslConfigCmd =
            "(echo [wsl2]& echo memory=8GB& echo processors=4& echo swap=2GB& echo.& " +
            "echo [experimental]& echo autoMemoryReclaim=gradual& echo sparseVhd=true) > \"%USERPROFILE%\\.wslconfig\"";

        const string RestoreCmd =
            "powershell -NoProfile -ExecutionPolicy Bypass -Command \"try { Enable-ComputerRestore -Drive 'C:\\' -ErrorAction Stop; " +
            "Checkpoint-Computer -Description 'Before WinForge' -RestorePointType 'MODIFY_SETTINGS' -ErrorAction Stop; " +
            "'Restore point created.' } catch { 'WARN: restore point skipped - ' + $_.Exception.Message }\"";

        // ----------------------------------------------------------------- catalog
        void Add(ObservableCollection<TweakItem> cat, string title, string desc,
                 bool work, bool gaming, bool basic, params string[] cmds)
        {
            var it = new TweakItem
            {
                Title = title,
                Description = desc,
                Commands = cmds,
                Work = work,
                Gaming = gaming,
                Basic = basic
            };
            cat.Add(it);
            _all.Add(it);
        }

        void BuildCatalog()
        {
            // ---- Apps / Games (each one its own checkbox) ----
            // Xbox: removed for Work only.
            Add(_apps, "Xbox app + Game Bar", "Xbox app, overlays and Game Bar packages.", true, false, false,
                RemoveAppx("Microsoft.XboxApp"), RemoveAppx("Microsoft.XboxGameOverlay"),
                RemoveAppx("Microsoft.XboxGamingOverlay"), RemoveAppx("Microsoft.XboxSpeechToTextOverlay"),
                RemoveAppx("Microsoft.Xbox.TCUI"), RemoveAppx("Microsoft.GamingApp"));
            // Junk games: removed in all modes.
            Add(_apps, "Solitaire Collection", "Microsoft Solitaire (with ads).", true, true, true, RemoveAppx("Microsoft.MicrosoftSolitaireCollection"));
            Add(_apps, "Mahjong", "Microsoft Mahjong.", true, true, true, RemoveAppx("Microsoft.MicrosoftMahjong"));
            Add(_apps, "Candy Crush", "Candy Crush Saga / Soda.", true, true, true,
                RemoveAppx("king.com.CandyCrushSaga"), RemoveAppx("king.com.CandyCrushSodaSaga"));
            Add(_apps, "Bubble Witch", "Bubble Witch 3 Saga.", true, true, true, RemoveAppx("king.com.BubbleWitch3Saga"));
            Add(_apps, "Bing News / Weather / etc.", "Bing News, Weather, Finance, Sports.", true, true, true,
                RemoveAppx("Microsoft.BingNews"), RemoveAppx("Microsoft.BingWeather"),
                RemoveAppx("Microsoft.BingFinance"), RemoveAppx("Microsoft.BingSports"));
            Add(_apps, "3rd-party preinstalled stubs", "Netflix, Spotify, TikTok, Facebook, Disney, etc.", true, true, true,
                RemoveAppx("*Netflix*"), RemoveAppx("*Spotify*"), RemoveAppx("*Facebook*"), RemoveAppx("*Twitter*"),
                RemoveAppx("*Disney*"), RemoveAppx("*TikTok*"), RemoveAppx("*Hulu*"), RemoveAppx("*Amazon*"),
                RemoveAppx("*Instagram*"), RemoveAppx("*Plex*"), RemoveAppx("*Dropbox*"), RemoveAppx("*Duolingo*"),
                RemoveAppx("*Wunderlist*"), RemoveAppx("*Asphalt*"), RemoveAppx("*Royal*"));
            // Microsoft bloat: Work + Gaming (Basic keeps these).
            Add(_apps, "Skype", "Skype app.", true, true, false, RemoveAppx("Microsoft.SkypeApp"));
            Add(_apps, "Maps", "Windows Maps.", true, true, false, RemoveAppx("Microsoft.WindowsMaps"));
            Add(_apps, "People", "People app.", true, true, false, RemoveAppx("Microsoft.People"));
            Add(_apps, "Your Phone", "Phone Link / Your Phone.", true, true, false, RemoveAppx("Microsoft.YourPhone"));
            Add(_apps, "Get Help + Tips", "Get Help and Get Started (Tips).", true, true, false,
                RemoveAppx("Microsoft.GetHelp"), RemoveAppx("Microsoft.Getstarted"));
            Add(_apps, "3D Builder / Viewer", "3D Builder, 3D Viewer, Print 3D.", true, true, false,
                RemoveAppx("Microsoft.3DBuilder"), RemoveAppx("Microsoft.Microsoft3DViewer"), RemoveAppx("Microsoft.Print3D"));
            Add(_apps, "Mixed Reality Portal", "Mixed Reality Portal.", true, true, false, RemoveAppx("Microsoft.MixedReality.Portal"));
            Add(_apps, "Groove Music + Movies & TV", "Zune Music / Video.", true, true, false,
                RemoveAppx("Microsoft.ZuneMusic"), RemoveAppx("Microsoft.ZuneVideo"));
            Add(_apps, "Feedback Hub", "Windows Feedback Hub.", true, true, false, RemoveAppx("Microsoft.WindowsFeedbackHub"));
            Add(_apps, "Office Hub", "My Office / Office hub app.", true, true, false, RemoveAppx("Microsoft.MicrosoftOfficeHub"));
            // Extra (off by default in all presets).
            Add(_apps, "Alarms & Clock", "Windows Alarms.", false, false, false, RemoveAppx("Microsoft.WindowsAlarms"));
            Add(_apps, "To Do", "Microsoft To Do.", false, false, false, RemoveAppx("Microsoft.Todos"));
            Add(_apps, "Clipchamp", "Clipchamp video editor.", false, false, false, RemoveAppx("Clipchamp.Clipchamp"));

            // ---- Privacy / Ads (safe for all modes) ----
            Add(_privacy, "Disable telemetry", "Set diagnostic data to the lowest level.", true, true, true,
                RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0),
                RegDword(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "AllowTelemetry", 0));
            Add(_privacy, "Disable suggested apps / consumer features", "Stops Windows auto-installing suggested apps.", true, true, true,
                RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", 1),
                RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableSoftLanding", 1));
            Add(_privacy, "Disable Start menu suggestions/ads", "Turns off tips and promoted apps in Start.", true, true, true,
                RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SilentInstalledAppsEnabled", 0),
                RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled", 0),
                RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "PreInstalledAppsEnabled", 0),
                RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled", 0),
                RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338389Enabled", 0),
                RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-310093Enabled", 0));
            Add(_privacy, "Disable advertising ID", "Stops apps using your advertising ID.", true, true, true,
                RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0),
                RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", "DisabledByGroupPolicy", 1));
            Add(_privacy, "Disable activity history / timeline", "Stops collecting and syncing your activity.", true, true, true,
                RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed", 0),
                RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0));
            Add(_privacy, "Disable Cortana", "Turns off Cortana via policy.", true, true, true,
                RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", 0));
            Add(_privacy, "Block telemetry domains (hosts file)", "Adds Microsoft telemetry servers to the hosts file so they resolve to nothing. Advanced - off by default.", false, false, false,
                "powershell -NoProfile -ExecutionPolicy Bypass -Command \"$h=$env:windir+'\\System32\\drivers\\etc\\hosts'; $d='vortex.data.microsoft.com','telemetry.microsoft.com','watson.telemetry.microsoft.com','settings-win.data.microsoft.com','v10.events.data.microsoft.com'; $c=@(Get-Content $h -ErrorAction SilentlyContinue); foreach($x in $d){ if($c -notcontains ('0.0.0.0 '+$x)){ Add-Content $h ('0.0.0.0 '+$x) } }\"");

            // ---- Services (Work + Gaming; Basic leaves them alone) ----
            Add(_services, "Xbox services", "Xbox Live auth/save/networking services.", true, false, false,
                DisableSvc("XblAuthManager"), DisableSvc("XblGameSave"), DisableSvc("XboxNetApiSvc"), DisableSvc("XboxGipSvc"));
            Add(_services, "Connected User Experiences (DiagTrack)", "Main telemetry service.", true, true, false, DisableSvc("DiagTrack"));
            Add(_services, "WAP Push (dmwappushservice)", "Device-management telemetry transport.", true, true, false, DisableSvc("dmwappushservice"));
            Add(_services, "Downloaded Maps Manager", "Background maps updates.", true, true, false, DisableSvc("MapsBroker"));
            Add(_services, "Retail Demo", "Store retail demo service.", true, true, false, DisableSvc("RetailDemo"));
            Add(_services, "Windows Media Player Network Sharing", "Media streaming service.", true, true, false, DisableSvc("WMPNetworkSvc"));
            Add(_services, "Fax", "Fax service.", true, true, false, DisableSvc("Fax"));
            Add(_services, "Remote Registry", "Remote registry access (security).", true, true, false, DisableSvc("RemoteRegistry"));

            // ---- Gaming tweaks (Gaming preset only) ----
            Add(_gaming, "Enable Game Mode", "Prioritizes games for better performance.", false, true, false,
                RegDword(@"HKCU\SOFTWARE\Microsoft\GameBar", "AllowAutoGameMode", 1),
                RegDword(@"HKCU\SOFTWARE\Microsoft\GameBar", "AutoGameModeEnabled", 1),
                RegDword(@"HKCU\System\GameConfigStore", "GameDVR_Enabled", 1));
            Add(_gaming, "Hardware-accelerated GPU scheduling", "Enables HAGS (needs supported GPU + reboot).", false, true, false,
                RegDword(@"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2));
            Add(_gaming, "Disable mouse acceleration", "Raw 1:1 mouse movement for precise aim.", false, true, false,
                RegSz(@"HKCU\Control Panel\Mouse", "MouseSpeed", "0"),
                RegSz(@"HKCU\Control Panel\Mouse", "MouseThreshold1", "0"),
                RegSz(@"HKCU\Control Panel\Mouse", "MouseThreshold2", "0"));
            Add(_gaming, "Visual effects -> performance", "Disables animations for higher FPS.", false, true, false,
                RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 2));
            Add(_gaming, "Lower network latency (disable Nagle)", "Disables Nagle on all NICs (lower ping).", false, true, false,
                "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Get-ChildItem 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\\Interfaces' | ForEach-Object { New-ItemProperty -Path $_.PSPath -Name TcpAckFrequency -Value 1 -PropertyType DWord -Force -ErrorAction SilentlyContinue | Out-Null; New-ItemProperty -Path $_.PSPath -Name TCPNoDelay -Value 1 -PropertyType DWord -Force -ErrorAction SilentlyContinue | Out-Null }\"");
            Add(_gaming, "Ultimate Performance power plan", "Highest-performance power plan.", false, true, false,
                "powercfg -duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61",
                "powercfg -setactive e9a42b02-d5df-448d-aa00-03f14749eb61");

            // ---- Performance (Work preset) ----
            Add(_perf, "High Performance power plan", "Switches to the High Performance plan.", true, false, false,
                "powercfg -setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
            Add(_perf, "Disable Game DVR (background recording)", "Stops Game Bar recording in the background.", true, false, false,
                RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0),
                RegDword(@"HKCU\System\GameConfigStore", "GameDVR_Enabled", 0),
                RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 0));
            Add(_perf, "Reduce startup app delay", "Removes the artificial startup delay.", true, false, true,
                RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", 0));
            Add(_perf, "Show file extensions", "Always show file extensions (safer).", true, true, true,
                RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", 0));
            Add(_perf, "Disable lock screen ads (Spotlight)", "Uses a plain lock screen picture.", true, false, true,
                RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "RotatingLockScreenEnabled", 0),
                RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "RotatingLockScreenOverlayEnabled", 0));

            // ---- Cleanup ----
            Add(_cleanup, "Clean temp files", "Deletes user and Windows temp folders.", true, true, true,
                "del /q /f /s \"%TEMP%\\*\"", "del /q /f /s \"%SystemRoot%\\Temp\\*\"");
            Add(_cleanup, "Empty Recycle Bin", "Clears the Recycle Bin.", true, true, true,
                "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Clear-RecycleBin -Force -ErrorAction SilentlyContinue\"");
            Add(_cleanup, "Disable telemetry scheduled tasks", "Disables CEIP / appraiser feedback tasks.", true, true, false,
                "schtasks /Change /TN \"\\Microsoft\\Windows\\Application Experience\\Microsoft Compatibility Appraiser\" /Disable",
                "schtasks /Change /TN \"\\Microsoft\\Windows\\Application Experience\\ProgramDataUpdater\" /Disable",
                "schtasks /Change /TN \"\\Microsoft\\Windows\\Customer Experience Improvement Program\\Consolidator\" /Disable",
                "schtasks /Change /TN \"\\Microsoft\\Windows\\Customer Experience Improvement Program\\UsbCeip\" /Disable",
                "schtasks /Change /TN \"\\Microsoft\\Windows\\Feedback\\Siuf\\DmClient\" /Disable",
                "schtasks /Change /TN \"\\Microsoft\\Windows\\Feedback\\Siuf\\DmClientOnScenarioDownload\" /Disable");
            Add(_cleanup, "Remove OneDrive", "Uninstalls OneDrive (off by default).", false, false, false,
                "taskkill /f /im OneDrive.exe",
                "\"%SystemRoot%\\System32\\OneDriveSetup.exe\" /uninstall",
                "\"%SystemRoot%\\SysWOW64\\OneDriveSetup.exe\" /uninstall");
            Add(_cleanup, "Docker: prune unused data", "Removes stopped containers, dangling images and unused networks (docker system prune -f).",
                false, false, false, "docker system prune -f");

            // ---- WSL / Startup ----
            Add(_system, "Optimize WSL2 memory (.wslconfig)",
                "Caps WSL/Docker RAM at 8GB and auto-returns freed memory to Windows, then shuts WSL down.",
                false, false, false, WslConfigCmd, "wsl --shutdown");
            Add(_system, "Shut down WSL now", "Stops the WSL2 VM to release its RAM immediately (closes Docker).",
                false, false, false, "wsl --shutdown");
            // The actual startup programs on THIS machine are added dynamically below.
            AddStartupEntries();

            // ---- Install (winget) - all off by default, user opts in ----
            Add(_install, "Google Chrome", "Web browser.", false, false, false, Winget("Google.Chrome"));
            Add(_install, "7-Zip", "Archive tool.", false, false, false, Winget("7zip.7zip"));
            Add(_install, "VLC", "Media player.", false, false, false, Winget("VideoLAN.VLC"));
            Add(_install, "Notepad++", "Text editor.", false, false, false, Winget("Notepad++.Notepad++"));
            Add(_install, "Adobe Acrobat Reader", "PDF reader.", false, false, false, Winget("Adobe.Acrobat.Reader.64-bit"));
            Add(_install, "Steam", "Game store/launcher.", false, false, false, Winget("Valve.Steam"));
            Add(_install, "Discord", "Voice/chat.", false, false, false, Winget("Discord.Discord"));
            Add(_install, "OBS Studio", "Recording/streaming.", false, false, false, Winget("OBSProject.OBSStudio"));
            Add(_install, "MSI Afterburner", "GPU overclock/monitor.", false, false, false, Winget("Guru3D.Afterburner"));

            BuildExtra();
        }

        // ---- Network / Windows Update / Win11-UI tweaks (all opt-in) ----
        void BuildExtra()
        {
            // ---- Network ----
            Add(_network, "DNS -> Cloudflare (1.1.1.1)", "Sets fast, private DNS on every active adapter.", false, false, false,
                "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Get-NetAdapter | Where-Object Status -eq 'Up' | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.ifIndex -ServerAddresses ('1.1.1.1','1.0.0.1') }\"");
            Add(_network, "DNS -> Google (8.8.8.8)", "Sets Google DNS on every active adapter.", false, false, false,
                "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Get-NetAdapter | Where-Object Status -eq 'Up' | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.ifIndex -ServerAddresses ('8.8.8.8','8.8.4.4') }\"");
            Add(_network, "DNS -> automatic (DHCP)", "Restores DNS to whatever your router/ISP provides.", false, false, false,
                "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Get-NetAdapter | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.ifIndex -ResetServerAddresses }\"");
            Add(_network, "Flush DNS cache", "Clears the resolver cache (fixes stale lookups).", false, false, false,
                "ipconfig /flushdns");
            Add(_network, "Release & renew IP", "Drops and re-requests your DHCP lease.", false, false, false,
                "ipconfig /release", "ipconfig /renew");
            Add(_network, "Reset Winsock (reboot needed)", "Resets the Winsock catalog - fixes broken networking.", false, false, false,
                "netsh winsock reset");
            Add(_network, "Reset TCP/IP stack (reboot needed)", "Reinstalls TCP/IP - last-resort connectivity fix.", false, false, false,
                "netsh int ip reset");

            // ---- Windows Update ----
            Add(_updates, "Updates: notify before download/install", "Stops automatic downloads - Windows asks you first.", false, false, false,
                RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "NoAutoUpdate", 0),
                RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "AUOptions", 2));
            Add(_updates, "No auto-restart while signed in", "Windows won't reboot for updates while you are logged on.", false, false, false,
                RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "NoAutoRebootWithLoggedOnUsers", 1));
            Add(_updates, "Exclude driver updates from Windows Update", "Keeps Windows Update from replacing your drivers.", false, false, false,
                RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "ExcludeWUDriversInQualityUpdate", 1));
            Add(_updates, "Defer feature updates 365 days", "Holds back big Windows version upgrades for a year.", false, false, false,
                RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "DeferFeatureUpdates", 1),
                RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "DeferFeatureUpdatesPeriodInDays", 365));

            // ---- Win 11 / UI (most need an Explorer restart to show) ----
            Add(_ui, "Classic right-click menu (Win11)", "Brings back the full Windows 10 context menu. Restart Explorer after.", false, false, false,
                "reg add \"HKCU\\Software\\Classes\\CLSID\\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\\InprocServer32\" /ve /d \"\" /f");
            Add(_ui, "Taskbar: align to the left", "Moves the Start button and icons to the left (Win11).", false, false, false,
                RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAl", 0));
            Add(_ui, "Hide taskbar Widgets button", "Removes the weather/widgets button (Win11).", false, false, false,
                RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa", 0));
            Add(_ui, "Hide taskbar Chat/Copilot button", "Removes the Chat (Teams) button (Win11).", false, false, false,
                RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarMn", 0));
            Add(_ui, "Hide taskbar search box", "Collapses the search box to nothing.", false, false, false,
                RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode", 0));
            Add(_ui, "Show seconds in the clock", "Adds seconds to the system tray clock.", false, false, false,
                RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowSecondsInSystemClock", 1));
        }

        // Reads the actual Run-key startup programs on this machine and adds a
        // disable item for each (so the list reflects reality, not a fixed set).
        void AddStartupEntries()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddRunKey(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", seen);
            AddRunKey(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", seen);
            AddRunKey(Registry.LocalMachine, @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Run", seen);
        }

        void AddRunKey(RegistryKey root, string path, HashSet<string> seen)
        {
            try
            {
                using (var k = root.OpenSubKey(path))
                {
                    if (k == null) return;
                    foreach (var name in k.GetValueNames())
                    {
                        if (string.IsNullOrEmpty(name) || !seen.Add(name)) continue;
                        string target = "";
                        try { target = k.GetValue(name)?.ToString() ?? ""; } catch { }
                        var it = new TweakItem
                        {
                            Title = "Startup: " + name,
                            Description = "Currently launches at boot: " + target,
                            Commands = new[] { DisableStartup(name) },
                            Work = false, Gaming = false, Basic = false
                        };
                        _system.Add(it);
                        _all.Add(it);
                    }
                }
            }
            catch { /* ignore unreadable keys */ }
        }

        // ------------------------------------------------------ save / load profile
        void btnSave_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "WinForge profile (*.wforge)|*.wforge",
                FileName = "my-profile.wforge"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var lines = new List<string>
                {
                    "# WinForge profile v" + Version,
                    "restorepoint=" + (cbRestore.IsChecked == true)
                };
                lines.AddRange(_all.Where(i => i.IsSelected).Select(i => "item=" + i.Title));
                File.WriteAllLines(dlg.FileName, lines);
                lblStatus.Text = "Profile saved";
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Save failed"); }
        }

        void btnLoad_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "WinForge profile (*.wforge)|*.wforge" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var lines = File.ReadAllLines(dlg.FileName);
                var titles = new HashSet<string>(
                    lines.Where(l => l.StartsWith("item=")).Select(l => l.Substring(5)),
                    StringComparer.Ordinal);
                foreach (var it in _all) it.IsSelected = titles.Contains(it.Title);
                var rp = lines.FirstOrDefault(l => l.StartsWith("restorepoint="));
                if (rp != null) cbRestore.IsChecked = rp.EndsWith("True");
                lblStatus.Text = "Profile loaded (" + titles.Count + " items)";
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Load failed"); }
        }

        // --------------------------------------------------------------- docker prune
        async void btnDocker_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Run 'docker system prune -f'?\nRemoves stopped containers, dangling images and unused networks.",
                "Docker cleanup", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;
            SetBusy(true);
            txtLog.Clear();
            Log("» Cleaning Docker (system prune)...");
            await Task.Run(() =>
            {
                RunCmd("docker system df");
                RunCmd("docker system prune -f");
            });
            Log("=== Docker cleanup done. ===");
            SetBusy(false);
        }

        // ----------------------------------------------------------------- presets
        void ApplyPreset(string p)
        {
            foreach (var it in _all)
            {
                switch (p)
                {
                    case "WORK": it.IsSelected = it.Work; break;
                    case "GAMING": it.IsSelected = it.Gaming; break;
                    case "BASIC": it.IsSelected = it.Basic; break;
                    default: it.IsSelected = false; break;
                }
            }
            lblStatus.Text = p == "CLEAR" ? "Cleared" : p + " preset selected";
        }

        void btnWork_Click(object s, RoutedEventArgs e) => ApplyPreset("WORK");
        void btnGaming_Click(object s, RoutedEventArgs e) => ApplyPreset("GAMING");
        void btnBasic_Click(object s, RoutedEventArgs e) => ApplyPreset("BASIC");
        void btnClear_Click(object s, RoutedEventArgs e) => ApplyPreset("CLEAR");

        // ------------------------------------------------------------------- runner
        void Log(string text)
        {
            Dispatcher.Invoke(() =>
            {
                txtLog.AppendText(text + Environment.NewLine);
                txtLog.ScrollToEnd();
            });
            if (_logFile == null) return;
            try { File.AppendAllText(_logFile, DateTime.Now.ToString("HH:mm:ss ") + text + Environment.NewLine); }
            catch { _logFile = null; } // stop trying if the folder is read-only
        }

        void SetProgress(int value, int max, string status)
        {
            Dispatcher.Invoke(() =>
            {
                prog.IsIndeterminate = false;
                prog.Maximum = max < 1 ? 1 : max;
                prog.Value = value;
                lblStatus.Text = status;
            });
        }

        void RunCmd(string command)
        {
            try
            {
                var psi = new ProcessStartInfo("cmd.exe", "/c " + command)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (var p = Process.Start(psi))
                {
                    string outp = p.StandardOutput.ReadToEnd();
                    string err = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    string combined = (outp + err).Trim();
                    if (combined.Length > 0)
                        Log("    " + combined.Replace("\r\n", "\n").Replace("\n", "\n    "));
                }
            }
            catch (Exception ex)
            {
                Log("    ERROR: " + ex.Message);
            }
        }

        void SetBusy(bool busy)
        {
            prog.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            prog.IsIndeterminate = busy;
            btnApply.IsEnabled = !busy;
            btnRevert.IsEnabled = !busy;
            btnExplorer.IsEnabled = !busy;
            btnDocker.IsEnabled = !busy;
            btnSizes.IsEnabled = !busy;
            btnModeGaming.IsEnabled = !busy;
            btnModeDev.IsEnabled = !busy;
            btnModeOffice.IsEnabled = !busy;
            btnWork.IsEnabled = !busy;
            btnGaming.IsEnabled = !busy;
            btnBasic.IsEnabled = !busy;
            btnClear.IsEnabled = !busy;
            lblStatus.Text = busy ? "Working..." : "Ready";
        }

        async void btnApply_Click(object sender, RoutedEventArgs e)
        {
            var selected = _all.Where(i => i.IsSelected).ToList();
            bool restore = cbRestore.IsChecked == true;
            if (selected.Count == 0 && !restore)
            {
                MessageBox.Show("Nothing selected.", "WinForge", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Confirmation dialog that lists exactly what will run.
            string list = string.Join(Environment.NewLine, selected.Take(40).Select(i => "  - " + i.Title));
            if (selected.Count > 40) list += Environment.NewLine + "  ...and " + (selected.Count - 40) + " more";
            var confirm = MessageBox.Show(
                "Apply these " + selected.Count + " item(s)?" + Environment.NewLine + Environment.NewLine + list +
                Environment.NewLine + Environment.NewLine +
                (restore ? "A system restore point will be created first." : "WARNING: No restore point will be created!"),
                "Confirm - review the list", MessageBoxButton.YesNo,
                restore ? MessageBoxImage.Question : MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            SetBusy(true);
            txtLog.Clear();
            Log("=== WinForge v" + Version + " - applying ===");
            if (_logFile != null) Log("Log file: " + _logFile);
            int total = (restore ? 1 : 0) + selected.Sum(i => i.Commands.Length);
            int done = 0;
            await Task.Run(() =>
            {
                if (restore)
                {
                    SetProgress(done, total, "Creating restore point...");
                    Log("» Creating system restore point...");
                    RunCmd(RestoreCmd);
                    SetProgress(++done, total, "Restore point done");
                }
                int idx = 0;
                foreach (var it in selected)
                {
                    idx++;
                    Log("» " + it.Title);
                    foreach (var c in it.Commands)
                    {
                        RunCmd(c);
                        SetProgress(++done, total, "Applying " + idx + "/" + selected.Count + ": " + it.Title);
                    }
                }
                Log("");
                Log("=== Finished. " + selected.Count + " item(s) applied. A restart is recommended. ===");
            });
            SetProgress(total, total, "Finished - " + selected.Count + " item(s) applied");
            SetBusy(false);
        }

        async void btnRevert_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Revert the settings/services changed by WinForge?\n" +
                "(Removed apps are NOT reinstalled - get them from the Microsoft Store.)",
                "Revert settings", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            SetBusy(true);
            txtLog.Clear();
            Log("=== Reverting settings ===");
            await Task.Run(() =>
            {
                foreach (var c in RevertCommands) RunCmd(c);
                Log("");
                Log("=== Revert finished. A restart is recommended. ===");
            });
            SetBusy(false);
        }

        // Mirrors winforge-undo.bat
        static readonly string[] RevertCommands =
        {
            // Re-enable services
            "sc config XblAuthManager start= demand", "sc config XblGameSave start= demand",
            "sc config XboxNetApiSvc start= demand", "sc config XboxGipSvc start= demand",
            "sc config dmwappushservice start= demand", "sc config WMPNetworkSvc start= demand",
            "sc config Fax start= demand", "sc config DiagTrack start= auto",
            "sc config MapsBroker start= delayed-auto",
            // Remove policy values
            "reg delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection\" /v AllowTelemetry /f",
            "reg delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent\" /v DisableWindowsConsumerFeatures /f",
            "reg delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent\" /v DisableSoftLanding /f",
            "reg delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\System\" /v EnableActivityFeed /f",
            "reg delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\System\" /v PublishUserActivities /f",
            "reg delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search\" /v AllowCortana /f",
            "reg delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\GameDVR\" /v AllowGameDVR /f",
            // Re-enable Start suggestions / advertising / Game DVR
            "reg add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager\" /v SystemPaneSuggestionsEnabled /t REG_DWORD /d 1 /f",
            "reg add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo\" /v Enabled /t REG_DWORD /d 1 /f",
            "reg add \"HKCU\\System\\GameConfigStore\" /v GameDVR_Enabled /t REG_DWORD /d 1 /f",
            // Revert gaming tweaks
            "reg add \"HKCU\\Control Panel\\Mouse\" /v MouseSpeed /t REG_SZ /d 1 /f",
            "reg add \"HKCU\\Control Panel\\Mouse\" /v MouseThreshold1 /t REG_SZ /d 6 /f",
            "reg add \"HKCU\\Control Panel\\Mouse\" /v MouseThreshold2 /t REG_SZ /d 10 /f",
            "reg add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VisualEffects\" /v VisualFXSetting /t REG_DWORD /d 0 /f",
            "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Get-ChildItem 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\\Interfaces' | ForEach-Object { Remove-ItemProperty -Path $_.PSPath -Name TcpAckFrequency -ErrorAction SilentlyContinue; Remove-ItemProperty -Path $_.PSPath -Name TCPNoDelay -ErrorAction SilentlyContinue }\"",
            // Balanced power plan
            "powercfg -setactive 381b4222-f694-41f0-9685-ff5bb260df2e",
            // Re-enable scheduled tasks
            "schtasks /Change /TN \"\\Microsoft\\Windows\\Application Experience\\Microsoft Compatibility Appraiser\" /Enable",
            "schtasks /Change /TN \"\\Microsoft\\Windows\\Customer Experience Improvement Program\\Consolidator\" /Enable",
            // Undo Windows Update policy tweaks
            "reg delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate\\AU\" /v NoAutoUpdate /f",
            "reg delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate\\AU\" /v AUOptions /f",
            "reg delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate\\AU\" /v NoAutoRebootWithLoggedOnUsers /f",
            "reg delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate\" /v ExcludeWUDriversInQualityUpdate /f",
            "reg delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate\" /v DeferFeatureUpdates /f",
            // Restore the Windows 11 context menu (remove the classic-menu override)
            "reg delete \"HKCU\\Software\\Classes\\CLSID\\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\" /f"
        };
    }
}
