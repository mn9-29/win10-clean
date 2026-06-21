using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;

namespace WinForge.Engine
{
    // Headless port of MainWindow.BuildCatalog() + BuildExtra() (and the dynamic
    // Run-key startup entries). Produces a flat list of EngineItem with stable Ids.
    public static class Catalog
    {
        public static List<EngineItem> Build()
        {
            var list = new List<EngineItem>();

            void Add(string category, string title, string desc,
                     bool work, bool gaming, bool basic, params string[] cmds)
            {
                list.Add(new EngineItem
                {
                    Title = title,
                    Desc = desc,
                    Category = category,
                    Commands = cmds,
                    Work = work,
                    Gaming = gaming,
                    Basic = basic
                });
            }

            // =========================== BuildCatalog() ===========================

            // ---- Apps / Games (each one its own checkbox) ----
            // Xbox: removed for Work only.
            Add("apps", "Xbox app + Game Bar", "Xbox app, overlays and Game Bar packages.", true, false, false,
                Commands.RemoveAppx("Microsoft.XboxApp"), Commands.RemoveAppx("Microsoft.XboxGameOverlay"),
                Commands.RemoveAppx("Microsoft.XboxGamingOverlay"), Commands.RemoveAppx("Microsoft.XboxSpeechToTextOverlay"),
                Commands.RemoveAppx("Microsoft.Xbox.TCUI"), Commands.RemoveAppx("Microsoft.GamingApp"));
            // Junk games: removed in all modes.
            Add("apps", "Solitaire Collection", "Microsoft Solitaire (with ads).", true, true, true, Commands.RemoveAppx("Microsoft.MicrosoftSolitaireCollection"));
            Add("apps", "Mahjong", "Microsoft Mahjong.", true, true, true, Commands.RemoveAppx("Microsoft.MicrosoftMahjong"));
            Add("apps", "Candy Crush", "Candy Crush Saga / Soda.", true, true, true,
                Commands.RemoveAppx("king.com.CandyCrushSaga"), Commands.RemoveAppx("king.com.CandyCrushSodaSaga"));
            Add("apps", "Bubble Witch", "Bubble Witch 3 Saga.", true, true, true, Commands.RemoveAppx("king.com.BubbleWitch3Saga"));
            Add("apps", "Bing News / Weather / etc.", "Bing News, Weather, Finance, Sports.", true, true, true,
                Commands.RemoveAppx("Microsoft.BingNews"), Commands.RemoveAppx("Microsoft.BingWeather"),
                Commands.RemoveAppx("Microsoft.BingFinance"), Commands.RemoveAppx("Microsoft.BingSports"));
            Add("apps", "3rd-party preinstalled stubs", "Netflix, Spotify, TikTok, Facebook, Disney, etc.", true, true, true,
                Commands.RemoveAppx("*Netflix*"), Commands.RemoveAppx("*Spotify*"), Commands.RemoveAppx("*Facebook*"), Commands.RemoveAppx("*Twitter*"),
                Commands.RemoveAppx("*Disney*"), Commands.RemoveAppx("*TikTok*"), Commands.RemoveAppx("*Hulu*"), Commands.RemoveAppx("*Amazon*"),
                Commands.RemoveAppx("*Instagram*"), Commands.RemoveAppx("*Plex*"), Commands.RemoveAppx("*Dropbox*"), Commands.RemoveAppx("*Duolingo*"),
                Commands.RemoveAppx("*Wunderlist*"), Commands.RemoveAppx("*Asphalt*"), Commands.RemoveAppx("*Royal*"));
            // Microsoft bloat: Work + Gaming (Basic keeps these).
            Add("apps", "Skype", "Skype app.", true, true, false, Commands.RemoveAppx("Microsoft.SkypeApp"));
            Add("apps", "Maps", "Windows Maps.", true, true, false, Commands.RemoveAppx("Microsoft.WindowsMaps"));
            Add("apps", "People", "People app.", true, true, false, Commands.RemoveAppx("Microsoft.People"));
            Add("apps", "Your Phone", "Phone Link / Your Phone.", true, true, false, Commands.RemoveAppx("Microsoft.YourPhone"));
            Add("apps", "Get Help + Tips", "Get Help and Get Started (Tips).", true, true, false,
                Commands.RemoveAppx("Microsoft.GetHelp"), Commands.RemoveAppx("Microsoft.Getstarted"));
            Add("apps", "3D Builder / Viewer", "3D Builder, 3D Viewer, Print 3D.", true, true, false,
                Commands.RemoveAppx("Microsoft.3DBuilder"), Commands.RemoveAppx("Microsoft.Microsoft3DViewer"), Commands.RemoveAppx("Microsoft.Print3D"));
            Add("apps", "Mixed Reality Portal", "Mixed Reality Portal.", true, true, false, Commands.RemoveAppx("Microsoft.MixedReality.Portal"));
            Add("apps", "Groove Music + Movies & TV", "Zune Music / Video.", true, true, false,
                Commands.RemoveAppx("Microsoft.ZuneMusic"), Commands.RemoveAppx("Microsoft.ZuneVideo"));
            Add("apps", "Feedback Hub", "Windows Feedback Hub.", true, true, false, Commands.RemoveAppx("Microsoft.WindowsFeedbackHub"));
            Add("apps", "Office Hub", "My Office / Office hub app.", true, true, false, Commands.RemoveAppx("Microsoft.MicrosoftOfficeHub"));
            // Extra (off by default in all presets).
            Add("apps", "Alarms & Clock", "Windows Alarms.", false, false, false, Commands.RemoveAppx("Microsoft.WindowsAlarms"));
            Add("apps", "To Do", "Microsoft To Do.", false, false, false, Commands.RemoveAppx("Microsoft.Todos"));
            Add("apps", "Clipchamp", "Clipchamp video editor.", false, false, false, Commands.RemoveAppx("Clipchamp.Clipchamp"));

            // ---- Privacy / Ads (safe for all modes) ----
            Add("privacy", "Disable telemetry", "Set diagnostic data to the lowest level.", true, true, true,
                Commands.RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0),
                Commands.RegDword(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "AllowTelemetry", 0));
            Add("privacy", "Disable suggested apps / consumer features", "Stops Windows auto-installing suggested apps.", true, true, true,
                Commands.RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", 1),
                Commands.RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableSoftLanding", 1));
            Add("privacy", "Disable Start menu suggestions/ads", "Turns off tips and promoted apps in Start.", true, true, true,
                Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SilentInstalledAppsEnabled", 0),
                Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled", 0),
                Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "PreInstalledAppsEnabled", 0),
                Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled", 0),
                Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338389Enabled", 0),
                Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-310093Enabled", 0));
            Add("privacy", "Disable advertising ID", "Stops apps using your advertising ID.", true, true, true,
                Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0),
                Commands.RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", "DisabledByGroupPolicy", 1));
            Add("privacy", "Disable activity history / timeline", "Stops collecting and syncing your activity.", true, true, true,
                Commands.RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed", 0),
                Commands.RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0));
            Add("privacy", "Disable Cortana", "Turns off Cortana via policy.", true, true, true,
                Commands.RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", 0));
            Add("privacy", "Block telemetry domains (hosts file)", "Adds Microsoft telemetry servers to the hosts file so they resolve to nothing. Advanced - off by default.", false, false, false,
                "powershell -NoProfile -ExecutionPolicy Bypass -Command \"$h=$env:windir+'\\System32\\drivers\\etc\\hosts'; $d='vortex.data.microsoft.com','telemetry.microsoft.com','watson.telemetry.microsoft.com','settings-win.data.microsoft.com','v10.events.data.microsoft.com'; $c=@(Get-Content $h -ErrorAction SilentlyContinue); foreach($x in $d){ if($c -notcontains ('0.0.0.0 '+$x)){ Add-Content $h ('0.0.0.0 '+$x) } }\"");

            // ---- Services (Work + Gaming; Basic leaves them alone) ----
            Add("services", "Xbox services", "Xbox Live auth/save/networking services.", true, false, false,
                Commands.DisableSvc("XblAuthManager"), Commands.DisableSvc("XblGameSave"), Commands.DisableSvc("XboxNetApiSvc"), Commands.DisableSvc("XboxGipSvc"));
            Add("services", "Connected User Experiences (DiagTrack)", "Main telemetry service.", true, true, false, Commands.DisableSvc("DiagTrack"));
            Add("services", "WAP Push (dmwappushservice)", "Device-management telemetry transport.", true, true, false, Commands.DisableSvc("dmwappushservice"));
            Add("services", "Downloaded Maps Manager", "Background maps updates.", true, true, false, Commands.DisableSvc("MapsBroker"));
            Add("services", "Retail Demo", "Store retail demo service.", true, true, false, Commands.DisableSvc("RetailDemo"));
            Add("services", "Windows Media Player Network Sharing", "Media streaming service.", true, true, false, Commands.DisableSvc("WMPNetworkSvc"));
            Add("services", "Fax", "Fax service.", true, true, false, Commands.DisableSvc("Fax"));
            Add("services", "Remote Registry", "Remote registry access (security).", true, true, false, Commands.DisableSvc("RemoteRegistry"));

            // ---- Gaming tweaks (Gaming preset only) ----
            Add("gaming", "Enable Game Mode", "Prioritizes games for better performance.", false, true, false,
                Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\GameBar", "AllowAutoGameMode", 1),
                Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\GameBar", "AutoGameModeEnabled", 1),
                Commands.RegDword(@"HKCU\System\GameConfigStore", "GameDVR_Enabled", 1));
            Add("gaming", "Hardware-accelerated GPU scheduling", "Enables HAGS (needs supported GPU + reboot).", false, true, false,
                Commands.RegDword(@"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2));
            Add("gaming", "Disable mouse acceleration", "Raw 1:1 mouse movement for precise aim.", false, true, false,
                Commands.RegSz(@"HKCU\Control Panel\Mouse", "MouseSpeed", "0"),
                Commands.RegSz(@"HKCU\Control Panel\Mouse", "MouseThreshold1", "0"),
                Commands.RegSz(@"HKCU\Control Panel\Mouse", "MouseThreshold2", "0"));
            Add("gaming", "Visual effects -> performance", "Disables animations for higher FPS.", false, true, false,
                Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 2));
            Add("gaming", "Lower network latency (disable Nagle)", "Disables Nagle on all NICs (lower ping).", false, true, false,
                "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Get-ChildItem 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\\Interfaces' | ForEach-Object { New-ItemProperty -Path $_.PSPath -Name TcpAckFrequency -Value 1 -PropertyType DWord -Force -ErrorAction SilentlyContinue | Out-Null; New-ItemProperty -Path $_.PSPath -Name TCPNoDelay -Value 1 -PropertyType DWord -Force -ErrorAction SilentlyContinue | Out-Null }\"");
            Add("gaming", "Ultimate Performance power plan", "Highest-performance power plan.", false, true, false,
                "powercfg -duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61",
                "powercfg -setactive e9a42b02-d5df-448d-aa00-03f14749eb61");

            // ---- Performance (Work preset) ----
            Add("performance", "High Performance power plan", "Switches to the High Performance plan.", true, false, false,
                "powercfg -setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
            Add("performance", "Disable Game DVR (background recording)", "Stops Game Bar recording in the background.", true, false, false,
                Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0),
                Commands.RegDword(@"HKCU\System\GameConfigStore", "GameDVR_Enabled", 0),
                Commands.RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 0));
            Add("performance", "Reduce startup app delay", "Removes the artificial startup delay.", true, false, true,
                Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", 0));
            Add("performance", "Show file extensions", "Always show file extensions (safer).", true, true, true,
                Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", 0));
            Add("performance", "Disable lock screen ads (Spotlight)", "Uses a plain lock screen picture.", true, false, true,
                Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "RotatingLockScreenEnabled", 0),
                Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "RotatingLockScreenOverlayEnabled", 0));

            // ---- Cleanup ----
            Add("cleanup", "Clean temp files", "Deletes user and Windows temp folders.", true, true, true,
                "del /q /f /s \"%TEMP%\\*\"", "del /q /f /s \"%SystemRoot%\\Temp\\*\"");
            Add("cleanup", "Empty Recycle Bin", "Clears the Recycle Bin.", true, true, true,
                "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Clear-RecycleBin -Force -ErrorAction SilentlyContinue\"");
            Add("cleanup", "Disable telemetry scheduled tasks", "Disables CEIP / appraiser feedback tasks.", true, true, false,
                "schtasks /Change /TN \"\\Microsoft\\Windows\\Application Experience\\Microsoft Compatibility Appraiser\" /Disable",
                "schtasks /Change /TN \"\\Microsoft\\Windows\\Application Experience\\ProgramDataUpdater\" /Disable",
                "schtasks /Change /TN \"\\Microsoft\\Windows\\Customer Experience Improvement Program\\Consolidator\" /Disable",
                "schtasks /Change /TN \"\\Microsoft\\Windows\\Customer Experience Improvement Program\\UsbCeip\" /Disable",
                "schtasks /Change /TN \"\\Microsoft\\Windows\\Feedback\\Siuf\\DmClient\" /Disable",
                "schtasks /Change /TN \"\\Microsoft\\Windows\\Feedback\\Siuf\\DmClientOnScenarioDownload\" /Disable");
            Add("cleanup", "Remove OneDrive", "Uninstalls OneDrive (off by default).", false, false, false,
                "taskkill /f /im OneDrive.exe",
                "\"%SystemRoot%\\System32\\OneDriveSetup.exe\" /uninstall",
                "\"%SystemRoot%\\SysWOW64\\OneDriveSetup.exe\" /uninstall");
            Add("cleanup", "Docker: prune unused data", "Removes stopped containers, dangling images and unused networks (docker system prune -f).",
                false, false, false, "docker system prune -f");

            // ---- WSL / Startup ----
            Add("system", "Optimize WSL2 memory (.wslconfig)",
                "Caps WSL/Docker RAM at 8GB and auto-returns freed memory to Windows, then shuts WSL down.",
                false, false, false, Commands.WslConfigCmd, "wsl --shutdown");
            Add("system", "Shut down WSL now", "Stops the WSL2 VM to release its RAM immediately (closes Docker).",
                false, false, false, "wsl --shutdown");
            // The actual startup programs on THIS machine are added dynamically below.
            AddStartupEntries(Add);

            // ---- Install (winget) - all off by default, user opts in ----
            Add("install", "Google Chrome", "Web browser.", false, false, false, Commands.Winget("Google.Chrome"));
            Add("install", "7-Zip", "Archive tool.", false, false, false, Commands.Winget("7zip.7zip"));
            Add("install", "VLC", "Media player.", false, false, false, Commands.Winget("VideoLAN.VLC"));
            Add("install", "Notepad++", "Text editor.", false, false, false, Commands.Winget("Notepad++.Notepad++"));
            Add("install", "Adobe Acrobat Reader", "PDF reader.", false, false, false, Commands.Winget("Adobe.Acrobat.Reader.64-bit"));
            Add("install", "Steam", "Game store/launcher.", false, false, false, Commands.Winget("Valve.Steam"));
            Add("install", "Discord", "Voice/chat.", false, false, false, Commands.Winget("Discord.Discord"));
            Add("install", "OBS Studio", "Recording/streaming.", false, false, false, Commands.Winget("OBSProject.OBSStudio"));
            Add("install", "MSI Afterburner", "GPU overclock/monitor.", false, false, false, Commands.Winget("Guru3D.Afterburner"));

            // =========================== BuildExtra() =============================

            // ---- Network ----
            Add("network", "DNS -> Cloudflare (1.1.1.1)", "Sets fast, private DNS on every active adapter.", false, false, false,
                "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Get-NetAdapter | Where-Object Status -eq 'Up' | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.ifIndex -ServerAddresses ('1.1.1.1','1.0.0.1') }\"");
            Add("network", "DNS -> Google (8.8.8.8)", "Sets Google DNS on every active adapter.", false, false, false,
                "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Get-NetAdapter | Where-Object Status -eq 'Up' | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.ifIndex -ServerAddresses ('8.8.8.8','8.8.4.4') }\"");
            Add("network", "DNS -> automatic (DHCP)", "Restores DNS to whatever your router/ISP provides.", false, false, false,
                "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Get-NetAdapter | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.ifIndex -ResetServerAddresses }\"");
            Add("network", "Flush DNS cache", "Clears the resolver cache (fixes stale lookups).", false, false, false,
                "ipconfig /flushdns");
            Add("network", "Release & renew IP", "Drops and re-requests your DHCP lease.", false, false, false,
                "ipconfig /release", "ipconfig /renew");
            Add("network", "Reset Winsock (reboot needed)", "Resets the Winsock catalog - fixes broken networking.", false, false, false,
                "netsh winsock reset");
            Add("network", "Reset TCP/IP stack (reboot needed)", "Reinstalls TCP/IP - last-resort connectivity fix.", false, false, false,
                "netsh int ip reset");

            // ---- Windows Update ----
            Add("updates", "Updates: notify before download/install", "Stops automatic downloads - Windows asks you first.", false, false, false,
                Commands.RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "NoAutoUpdate", 0),
                Commands.RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "AUOptions", 2));
            Add("updates", "No auto-restart while signed in", "Windows won't reboot for updates while you are logged on.", false, false, false,
                Commands.RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "NoAutoRebootWithLoggedOnUsers", 1));
            Add("updates", "Exclude driver updates from Windows Update", "Keeps Windows Update from replacing your drivers.", false, false, false,
                Commands.RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "ExcludeWUDriversInQualityUpdate", 1));
            Add("updates", "Defer feature updates 365 days", "Holds back big Windows version upgrades for a year.", false, false, false,
                Commands.RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "DeferFeatureUpdates", 1),
                Commands.RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "DeferFeatureUpdatesPeriodInDays", 365));

            // ---- Win 11 / UI (most need an Explorer restart to show) ----
            Add("ui", "Classic right-click menu (Win11)", "Brings back the full Windows 10 context menu. Restart Explorer after.", false, false, false,
                "reg add \"HKCU\\Software\\Classes\\CLSID\\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\\InprocServer32\" /ve /d \"\" /f");
            Add("ui", "Taskbar: align to the left", "Moves the Start button and icons to the left (Win11).", false, false, false,
                Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAl", 0));
            Add("ui", "Hide taskbar Widgets button", "Removes the weather/widgets button (Win11).", false, false, false,
                Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa", 0));
            Add("ui", "Hide taskbar Chat/Copilot button", "Removes the Chat (Teams) button (Win11).", false, false, false,
                Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarMn", 0));
            Add("ui", "Hide taskbar search box", "Collapses the search box to nothing.", false, false, false,
                Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode", 0));
            Add("ui", "Show seconds in the clock", "Adds seconds to the system tray clock.", false, false, false,
                Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowSecondsInSystemClock", 1));

            // ---- Maintenance (health & repair; all opt-in, can be slow) ----
            Add("maintenance", "System File Check (SFC)", "Scans and repairs corrupted Windows system files. Takes a while.", false, false, false,
                "sfc /scannow");
            Add("maintenance", "Repair Windows image (DISM)", "Fixes the component store DISM /Online /Cleanup-Image /RestoreHealth. 10-20 min.", false, false, false,
                "DISM /Online /Cleanup-Image /RestoreHealth");
            Add("maintenance", "Scan disk for errors (online)", "Read-only chkdsk scan of C: - no reboot needed.", false, false, false,
                "chkdsk C: /scan");
            Add("maintenance", "Disk Cleanup (automatic)", "Runs the built-in cleanup to free space on C:.", false, false, false,
                "cleanmgr /verylowdisk");
            Add("maintenance", "Optimize / TRIM drive C:", "Defragments HDDs or sends TRIM to SSDs.", false, false, false,
                "defrag C: /O");
            Add("maintenance", "Clear Windows Update cache", "Stops the update services, clears the download cache, restarts them.", false, false, false,
                "net stop wuauserv", "net stop bits",
                "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Remove-Item -Path ($env:windir+'\\SoftwareDistribution\\Download\\*') -Recurse -Force -ErrorAction SilentlyContinue\"",
                "net start bits", "net start wuauserv");
            Add("maintenance", "Flush DNS + reset network stack", "ipconfig /flushdns then Winsock reset (reboot to finish).", false, false, false,
                "ipconfig /flushdns", "netsh winsock reset");
            Add("maintenance", "Reinstall built-in Windows apps", "Re-registers the default Microsoft apps (undo accidental removals). Won't touch your own programs.", false, false, false,
                "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Get-AppxPackage -AllUsers | ForEach-Object { Add-AppxPackage -DisableDevelopmentMode -Register ($_.InstallLocation + '\\AppxManifest.xml') -ErrorAction SilentlyContinue }\"");

            AssignIds(list);
            return list;
        }

        // Reads the actual Run-key startup programs on this machine and adds a
        // disable item for each (so the list reflects reality, not a fixed set).
        static void AddStartupEntries(Action<string, string, string, bool, bool, bool, string[]> add)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddRunKey(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", seen, add);
            AddRunKey(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", seen, add);
            AddRunKey(Registry.LocalMachine, @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Run", seen, add);
        }

        static void AddRunKey(RegistryKey root, string path, HashSet<string> seen,
                              Action<string, string, string, bool, bool, bool, string[]> add)
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
                        add("system", "Startup: " + name, "Currently launches at boot: " + target,
                            false, false, false, new[] { Commands.DisableStartup(name) });
                    }
                }
            }
            catch { /* ignore unreadable keys */ }
        }

        // Assigns a stable unique Id of the form category + "-" + slug(title);
        // on collision an incrementing number is appended.
        static void AssignIds(List<EngineItem> items)
        {
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (var it in items)
            {
                string baseId = (it.Category ?? "") + "-" + Slug(it.Title);
                string id = baseId;
                int n = 2;
                while (!used.Add(id))
                {
                    id = baseId + "-" + n;
                    n++;
                }
                it.Id = id;
            }
        }

        // lowercase, non-alphanumeric runs replaced by '-', trimmed.
        static string Slug(string title)
        {
            if (string.IsNullOrEmpty(title)) return "";
            var sb = new StringBuilder(title.Length);
            bool lastDash = false;
            foreach (char ch in title)
            {
                char c = char.ToLowerInvariant(ch);
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    sb.Append(c);
                    lastDash = false;
                }
                else
                {
                    if (!lastDash) { sb.Append('-'); lastDash = true; }
                }
            }
            return sb.ToString().Trim('-');
        }
    }
}
