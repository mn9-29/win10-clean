using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Win10Clean
{
    public partial class MainWindow : Window
    {
        const string Version = "2.1.0";

        readonly List<TweakItem> _all = new List<TweakItem>();
        readonly ObservableCollection<TweakItem> _apps = new ObservableCollection<TweakItem>();
        readonly ObservableCollection<TweakItem> _privacy = new ObservableCollection<TweakItem>();
        readonly ObservableCollection<TweakItem> _services = new ObservableCollection<TweakItem>();
        readonly ObservableCollection<TweakItem> _gaming = new ObservableCollection<TweakItem>();
        readonly ObservableCollection<TweakItem> _perf = new ObservableCollection<TweakItem>();
        readonly ObservableCollection<TweakItem> _cleanup = new ObservableCollection<TweakItem>();
        readonly ObservableCollection<TweakItem> _system = new ObservableCollection<TweakItem>();
        readonly ObservableCollection<TweakItem> _install = new ObservableCollection<TweakItem>();

        public MainWindow()
        {
            InitializeComponent();
            lblVersion.Text = "v" + Version;
            BuildCatalog();
            icApps.ItemsSource = _apps;
            icPrivacy.ItemsSource = _privacy;
            icServices.ItemsSource = _services;
            icGaming.ItemsSource = _gaming;
            icPerf.ItemsSource = _perf;
            icCleanup.ItemsSource = _cleanup;
            icSystem.ItemsSource = _system;
            icInstall.ItemsSource = _install;
            ApplyPreset("WORK");
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
            "powershell -NoProfile -ExecutionPolicy Bypass -Command \"$pat='" + pattern + "'; " +
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
            "Checkpoint-Computer -Description 'Before win10-clean' -RestorePointType 'MODIFY_SETTINGS' -ErrorAction Stop; " +
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

            // ---- WSL / Startup (situational, all off by default) ----
            Add(_system, "Optimize WSL2 memory (.wslconfig)",
                "Caps WSL/Docker RAM at 8GB and auto-returns freed memory to Windows, then shuts WSL down.",
                false, false, false, WslConfigCmd, "wsl --shutdown");
            Add(_system, "Shut down WSL now", "Stops the WSL2 VM to release its RAM immediately (closes Docker).",
                false, false, false, "wsl --shutdown");
            Add(_system, "Disable Docker Desktop autostart", "Stops Docker launching at boot (start it manually when needed).",
                false, false, false, DisableStartup("*Docker*"));
            Add(_system, "Disable Discord autostart", "Stops Discord launching at boot.", false, false, false, DisableStartup("*Discord*"));
            Add(_system, "Disable Steam autostart", "Stops Steam launching at boot.", false, false, false, DisableStartup("*Steam*"));
            Add(_system, "Disable SteelSeries autostart", "Stops SteelSeries GG / Sonar launching at boot.", false, false, false, DisableStartup("*SteelSeries*"));
            Add(_system, "Disable GitHub Desktop autostart", "Stops GitHub Desktop launching at boot.", false, false, false, DisableStartup("*GitHub*"));
            Add(_system, "Disable Epic Games autostart", "Stops Epic Games Launcher at boot.", false, false, false, DisableStartup("*Epic*"));
            Add(_system, "Disable Spotify autostart", "Stops Spotify launching at boot.", false, false, false, DisableStartup("*Spotify*"));

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
                MessageBox.Show("Nothing selected.", "win10-clean", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                "Apply " + selected.Count + " selected item(s)?" +
                (restore ? "\nA restore point will be created first." : "\n(No restore point will be created!)"),
                "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            SetBusy(true);
            txtLog.Clear();
            Log("=== win10-clean v" + Version + " - applying ===");
            await Task.Run(() =>
            {
                if (restore)
                {
                    Log("» Creating system restore point...");
                    RunCmd(RestoreCmd);
                }
                foreach (var it in selected)
                {
                    Log("» " + it.Title);
                    foreach (var c in it.Commands) RunCmd(c);
                }
                Log("");
                Log("=== Finished. " + selected.Count + " item(s) applied. A restart is recommended. ===");
            });
            SetBusy(false);
        }

        async void btnRevert_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Revert the settings/services changed by win10-clean?\n" +
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

        // Mirrors win10-clean-undo.bat
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
            "schtasks /Change /TN \"\\Microsoft\\Windows\\Customer Experience Improvement Program\\Consolidator\" /Enable"
        };
    }
}
