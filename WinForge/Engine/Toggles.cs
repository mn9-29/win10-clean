using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace WinForge.Engine
{
    /// <summary>
    /// One stateful, two-way setting the user can flip on/off, and whose
    /// CURRENT state we can read back from the machine. Unlike the action
    /// catalog (remove app, clean temp, …) these reflect live system state.
    /// </summary>
    public class ToggleItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Desc { get; set; }
        public string Category { get; set; }
        public bool Applied { get; set; }   // computed live

        // not serialised to the UI
        public string[] On;
        public string[] Off;
        public string StateKind;   // regdword | regsz | svc | regkey
        public string Hive;        // HKLM | HKCU (for reg* kinds)
        public string Path;        // reg subpath, or service name, or key path
        public string Name;        // value name (reg* kinds)
        public string Expect;      // expected value (applied) for regdword/regsz
    }

    public static class Toggles
    {
        static readonly List<ToggleItem> _items = Build();
        static readonly Dictionary<string, ToggleItem> _byId = Index();

        static Dictionary<string, ToggleItem> Index()
        {
            var d = new Dictionary<string, ToggleItem>();
            foreach (var t in _items) d[t.Id] = t;
            return d;
        }

        // Returns every toggle with its live Applied state filled in.
        public static List<ToggleItem> List()
        {
            foreach (var t in _items)
            {
                try { t.Applied = Read(t); } catch { t.Applied = false; }
            }
            return _items;
        }

        // Applies (on=true) or reverts (on=false) a toggle, then returns the
        // freshly-read state.
        public static bool SetApplied(string id, bool on)
        {
            if (!_byId.TryGetValue(id, out var t)) return false;
            try
            {
                foreach (var cmd in (on ? t.On : t.Off)) Commands.Run(cmd, _ => { });
                t.Applied = Read(t);
                return true;
            }
            catch { return false; }
        }

        // Live state of a single toggle by id.
        public static bool IsApplied(string id)
        {
            if (!_byId.TryGetValue(id, out var t)) return false;
            try { return Read(t); } catch { return false; }
        }

        // ---------------------------------------------------------- state read
        static bool Read(ToggleItem t)
        {
            switch (t.StateKind)
            {
                case "regdword":
                {
                    int v = RegInt(t.Hive, t.Path, t.Name);
                    return v == int.Parse(t.Expect);
                }
                case "regsz":
                {
                    string v = RegStr(t.Hive, t.Path, t.Name);
                    return string.Equals(v, t.Expect, StringComparison.OrdinalIgnoreCase);
                }
                case "svc":
                {
                    // disabled == applied
                    int start = RegInt("HKLM", @"SYSTEM\CurrentControlSet\Services\" + t.Path, "Start");
                    return start == 4;
                }
                case "regkey":
                    using (var k = Root(t.Hive).OpenSubKey(t.Path)) return k != null;
                default:
                    return false;
            }
        }

        static RegistryKey Root(string hive) =>
            hive == "HKLM" ? Registry.LocalMachine : Registry.CurrentUser;

        static int RegInt(string hive, string path, string name)
        {
            try { using (var k = Root(hive).OpenSubKey(path)) { var v = k?.GetValue(name); return v is int i ? i : -1; } }
            catch { return -1; }
        }
        static string RegStr(string hive, string path, string name)
        {
            try { using (var k = Root(hive).OpenSubKey(path)) { return k?.GetValue(name)?.ToString(); } }
            catch { return null; }
        }

        // ---------------------------------------------------------- definitions
        static void Add(List<ToggleItem> list, string id, string title, string desc, string cat,
                        string[] on, string[] off,
                        string kind, string hive, string path, string name, string expect)
        {
            list.Add(new ToggleItem
            {
                Id = id, Title = title, Desc = desc, Category = cat,
                On = on, Off = off,
                StateKind = kind, Hive = hive, Path = path, Name = name, Expect = expect
            });
        }

        static string RegDel(string hive, string path, string name) =>
            "reg delete \"" + hive + "\\" + path + "\" /v " + name + " /f";

        static List<ToggleItem> Build()
        {
            var l = new List<ToggleItem>();

            // ---- Privacy ----
            Add(l, "telemetry", "Telemetry (diagnostic data)", "Send diagnostic data to Microsoft.", "privacy",
                new[] {
                    Commands.RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0),
                    Commands.RegDword(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "AllowTelemetry", 0)
                },
                new[] {
                    RegDel("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry"),
                    RegDel("HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "AllowTelemetry")
                },
                "regdword", "HKLM", @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", "0");

            Add(l, "cortana", "Cortana", "The Cortana assistant.", "privacy",
                new[] { Commands.RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", 0) },
                new[] { RegDel("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana") },
                "regdword", "HKLM", @"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", "0");

            Add(l, "advertisingId", "Advertising ID", "Let apps use your advertising ID.", "privacy",
                new[] { Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0) },
                new[] { Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 1) },
                "regdword", "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", "0");

            Add(l, "startSuggestions", "Start menu suggestions / ads", "Tips and promoted apps in Start.", "privacy",
                new[] {
                    Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled", 0),
                    Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SilentInstalledAppsEnabled", 0)
                },
                new[] {
                    Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled", 1),
                    Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SilentInstalledAppsEnabled", 1)
                },
                "regdword", "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled", "0");

            Add(l, "activityHistory", "Activity history / Timeline", "Collect and sync your activity.", "privacy",
                new[] {
                    Commands.RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed", 0),
                    Commands.RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0)
                },
                new[] {
                    RegDel("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed"),
                    RegDel("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities")
                },
                "regdword", "HKLM", @"SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed", "0");

            Add(l, "consumerFeatures", "Suggested apps (consumer features)", "Auto-installed suggested apps.", "privacy",
                new[] { Commands.RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", 1) },
                new[] { RegDel("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures") },
                "regdword", "HKLM", @"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", "1");

            // ---- Performance ----
            Add(l, "gameDvr", "Game DVR (background recording)", "Game Bar background recording.", "performance",
                new[] {
                    Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0),
                    Commands.RegDword(@"HKCU\System\GameConfigStore", "GameDVR_Enabled", 0)
                },
                new[] {
                    Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 1),
                    Commands.RegDword(@"HKCU\System\GameConfigStore", "GameDVR_Enabled", 1)
                },
                "regdword", "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", "0");

            Add(l, "showFileExt", "Show file extensions", "Always show file extensions in Explorer.", "performance",
                new[] { Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", 0) },
                new[] { Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", 1) },
                "regdword", "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", "0");

            Add(l, "mouseAccel", "Mouse acceleration", "Pointer precision / acceleration.", "performance",
                new[] {
                    Commands.RegSz(@"HKCU\Control Panel\Mouse", "MouseSpeed", "0"),
                    Commands.RegSz(@"HKCU\Control Panel\Mouse", "MouseThreshold1", "0"),
                    Commands.RegSz(@"HKCU\Control Panel\Mouse", "MouseThreshold2", "0")
                },
                new[] {
                    Commands.RegSz(@"HKCU\Control Panel\Mouse", "MouseSpeed", "1"),
                    Commands.RegSz(@"HKCU\Control Panel\Mouse", "MouseThreshold1", "6"),
                    Commands.RegSz(@"HKCU\Control Panel\Mouse", "MouseThreshold2", "10")
                },
                "regsz", "HKCU", @"Control Panel\Mouse", "MouseSpeed", "0");

            // ---- Win 11 / UI ----
            Add(l, "classicMenu", "Classic right-click menu (Win11)", "Full Windows 10 context menu.", "ui",
                new[] { "reg add \"HKCU\\Software\\Classes\\CLSID\\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\\InprocServer32\" /ve /d \"\" /f" },
                new[] { "reg delete \"HKCU\\Software\\Classes\\CLSID\\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\" /f" },
                "regkey", "HKCU", @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32", null, null);

            Add(l, "taskbarLeft", "Taskbar aligned left", "Move Start + icons to the left.", "ui",
                new[] { Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAl", 0) },
                new[] { Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAl", 1) },
                "regdword", "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAl", "0");

            Add(l, "hideWidgets", "Hide taskbar Widgets", "Remove the weather/widgets button.", "ui",
                new[] { Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa", 0) },
                new[] { Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa", 1) },
                "regdword", "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa", "0");

            Add(l, "hideChat", "Hide taskbar Chat/Copilot", "Remove the Chat (Teams) button.", "ui",
                new[] { Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarMn", 0) },
                new[] { Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarMn", 1) },
                "regdword", "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarMn", "0");

            Add(l, "clockSeconds", "Show seconds in clock", "Add seconds to the tray clock.", "ui",
                new[] { Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowSecondsInSystemClock", 1) },
                new[] { RegDel("HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowSecondsInSystemClock") },
                "regdword", "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowSecondsInSystemClock", "1");

            // ---- Gaming ----
            Add(l, "gameMode", "Game Mode", "Prioritise games for performance.", "gaming",
                new[] {
                    Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\GameBar", "AllowAutoGameMode", 1),
                    Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\GameBar", "AutoGameModeEnabled", 1)
                },
                new[] {
                    Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\GameBar", "AllowAutoGameMode", 0),
                    Commands.RegDword(@"HKCU\SOFTWARE\Microsoft\GameBar", "AutoGameModeEnabled", 0)
                },
                "regdword", "HKCU", @"SOFTWARE\Microsoft\GameBar", "AutoGameModeEnabled", "1");

            Add(l, "hags", "Hardware-accelerated GPU scheduling", "HAGS (needs reboot + supported GPU).", "gaming",
                new[] { Commands.RegDword(@"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2) },
                new[] { Commands.RegDword(@"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 1) },
                "regdword", "HKLM", @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", "2");

            // ---- Updates ----
            Add(l, "updNoRestart", "No auto-restart while signed in", "Block reboot for updates while logged on.", "updates",
                new[] { Commands.RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "NoAutoRebootWithLoggedOnUsers", 1) },
                new[] { RegDel("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "NoAutoRebootWithLoggedOnUsers") },
                "regdword", "HKLM", @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "NoAutoRebootWithLoggedOnUsers", "1");

            Add(l, "updExclDrivers", "Exclude driver updates", "Keep Windows Update from replacing drivers.", "updates",
                new[] { Commands.RegDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "ExcludeWUDriversInQualityUpdate", 1) },
                new[] { RegDel("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "ExcludeWUDriversInQualityUpdate") },
                "regdword", "HKLM", @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "ExcludeWUDriversInQualityUpdate", "1");

            // ---- Services (applied == disabled) ----
            Add(l, "svcDiagTrack", "Connected User Experiences (DiagTrack)", "Main telemetry service.", "services",
                new[] { Commands.DisableSvc("DiagTrack") },
                new[] { "sc config DiagTrack start= auto" },
                "svc", "HKLM", "DiagTrack", null, null);

            Add(l, "svcXbox", "Xbox services", "Xbox Live auth/save/networking services.", "services",
                new[] { Commands.DisableSvc("XblAuthManager"), Commands.DisableSvc("XblGameSave"), Commands.DisableSvc("XboxNetApiSvc"), Commands.DisableSvc("XboxGipSvc") },
                new[] { "sc config XblAuthManager start= demand", "sc config XblGameSave start= demand", "sc config XboxNetApiSvc start= demand", "sc config XboxGipSvc start= demand" },
                "svc", "HKLM", "XblAuthManager", null, null);

            Add(l, "svcMaps", "Downloaded Maps Manager", "Background maps updates.", "services",
                new[] { Commands.DisableSvc("MapsBroker") },
                new[] { "sc config MapsBroker start= delayed-auto" },
                "svc", "HKLM", "MapsBroker", null, null);

            Add(l, "svcFax", "Fax", "Fax service.", "services",
                new[] { Commands.DisableSvc("Fax") },
                new[] { "sc config Fax start= demand" },
                "svc", "HKLM", "Fax", null, null);

            Add(l, "svcRemoteReg", "Remote Registry", "Remote registry access (security).", "services",
                new[] { Commands.DisableSvc("RemoteRegistry") },
                new[] { "sc config RemoteRegistry start= demand" },
                "svc", "HKLM", "RemoteRegistry", null, null);

            return l;
        }
    }
}
