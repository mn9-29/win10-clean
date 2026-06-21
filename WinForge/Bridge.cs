using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using WinForge.Engine;

namespace WinForge
{
    /// <summary>
    /// Message bridge between the React UI (WebView2) and the C# engine.
    /// JS sends {id,type,payload}; we reply {id,ok,data|error} and push
    /// {event:'log'|'progress'|'dashboard', ...} during long operations.
    /// </summary>
    public class Bridge
    {
        const string Version = "2.11.0";
        const string RepoOwner = "mn9-29";
        const string RepoName = "WinForge";

        readonly Action<string> _post;
        readonly List<EngineItem> _items;
        readonly Dictionary<string, EngineItem> _byId;

        static readonly JsonSerializerSettings Camel = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        public Bridge(Action<string> post)
        {
            _post = post;
            _items = Catalog.Build();
            _byId = new Dictionary<string, EngineItem>();
            foreach (var it in _items) _byId[it.Id] = it;
        }

        // ---------------------------------------------------------- dispatch
        public void HandleMessage(string raw)
        {
            JObject o;
            try { o = JObject.Parse(raw); } catch { return; }
            string id = (string)o["id"];
            string type = (string)o["type"];
            var payload = o["payload"];
            if (string.IsNullOrEmpty(type)) return;
            try
            {
                switch (type)
                {
                    case "getCatalog": Respond(id, _items.Select(Project).ToList()); break;
                    case "getDashboard": Respond(id, SysInfo.Get()); break;
                    case "apply": Task.Run(() => Apply(id, payload)); break;
                    case "runMode": Task.Run(() => RunMode(id, (string)payload?["mode"])); break;
                    case "scan": Task.Run(() => Scan(id)); break;
                    case "revert": Task.Run(() => Revert(id)); break;
                    case "backupRegistry": Task.Run(() => BackupRegistry(id)); break;
                    case "restoreRegistry":
                        RespondError(id, "Restore is done from the saved .reg backup folder in Documents.");
                        break;
                    case "checkUpdates": Task.Run(() => CheckUpdates(id)); break;
                    default: RespondError(id, "Unknown command: " + type); break;
                }
            }
            catch (Exception ex) { RespondError(id, ex.Message); }
        }

        static object Project(EngineItem it) => new
        {
            it.Id, it.Title, it.Desc, it.Category, it.Work, it.Gaming, it.Basic
        };

        // ----------------------------------------------------------- senders
        void Send(object o) { try { _post(JsonConvert.SerializeObject(o, Camel)); } catch { } }
        void Respond(string id, object data) => Send(new { id, ok = true, data });
        void RespondError(string id, string error) => Send(new { id, ok = false, error });
        void EmitLog(string line, string level = "info") => Send(new { @event = "log", line, level });
        void EmitProgress(int value, string status) => Send(new { @event = "progress", value, status });
        void EmitDashboard() { try { Send(new { @event = "dashboard", data = SysInfo.Get() }); } catch { } }

        static int Pct(int done, int total) => total < 1 ? 100 : (int)Math.Round(100.0 * done / total);

        // ------------------------------------------------------------- apply
        void Apply(string id, JToken payload)
        {
            try
            {
                var ids = payload?["ids"]?.ToObject<List<string>>() ?? new List<string>();
                bool restore = (bool?)payload?["restorePoint"] ?? false;
                bool backup = (bool?)payload?["backupReg"] ?? false;
                var sel = ids.Where(_byId.ContainsKey).Select(x => _byId[x]).ToList();

                int total = (restore ? 1 : 0) + (backup ? 1 : 0) + sel.Sum(i => i.Commands.Length);
                int done = 0;
                EmitProgress(0, "Starting…");

                if (restore)
                {
                    EmitLog("Creating system restore point…");
                    Commands.Run(Commands.RestoreCmd, l => EmitLog("  " + l));
                    EmitProgress(Pct(++done, total), "Restore point");
                }
                if (backup)
                {
                    EmitLog("Backing up registry branches…");
                    EmitLog("  Saved to: " + DoRegBackup());
                    EmitProgress(Pct(++done, total), "Registry backup");
                }
                foreach (var it in sel)
                {
                    EmitLog("Applying: " + it.Title);
                    foreach (var c in it.Commands)
                    {
                        Commands.Run(c, l => EmitLog("  " + l));
                        EmitProgress(Pct(++done, total), it.Title);
                    }
                }
                EmitLog("All selected tweaks applied. A restart is recommended.", "ok");
                EmitProgress(100, "Done");
                EmitDashboard();
                Respond(id, new { applied = sel.Count });
            }
            catch (Exception ex) { RespondError(id, ex.Message); }
        }

        // ------------------------------------------------------- device modes
        static readonly (string label, string cmd)[] GamingMode =
        {
            ("Power plan: High Performance", "powercfg -setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"),
            ("Enable Game Mode", "reg add \"HKCU\\SOFTWARE\\Microsoft\\GameBar\" /v AllowAutoGameMode /t REG_DWORD /d 1 /f & reg add \"HKCU\\SOFTWARE\\Microsoft\\GameBar\" /v AutoGameModeEnabled /t REG_DWORD /d 1 /f"),
            ("Stop Docker Desktop", "taskkill /f /im \"Docker Desktop.exe\" /im com.docker.backend.exe /im com.docker.service 2>nul"),
            ("Shut down WSL (frees RAM)", "wsl --shutdown"),
            ("Close dev/comms apps", "taskkill /f /im Code.exe /im devenv.exe /im GitHubDesktop.exe 2>nul"),
        };
        static readonly (string label, string cmd)[] DevMode =
        {
            ("Power plan: High Performance", "powercfg -setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"),
            ("Close gaming apps", "taskkill /f /im steam.exe /im Discord.exe /im EpicGamesLauncher.exe /im EADesktop.exe /im RiotClientServices.exe 2>nul"),
            ("Start Docker Desktop (starts WSL)", "start \"\" \"%ProgramFiles%\\Docker\\Docker\\Docker Desktop.exe\""),
        };
        static readonly (string label, string cmd)[] OfficeMode =
        {
            ("Power plan: Balanced", "powercfg -setactive 381b4222-f694-41f0-9685-ff5bb260df2e"),
            ("Close gaming apps", "taskkill /f /im steam.exe /im Discord.exe /im EpicGamesLauncher.exe /im EADesktop.exe 2>nul"),
            ("Stop Docker Desktop", "taskkill /f /im \"Docker Desktop.exe\" /im com.docker.backend.exe 2>nul"),
            ("Shut down WSL (frees RAM)", "wsl --shutdown"),
        };

        void RunMode(string id, string mode)
        {
            try
            {
                (string label, string cmd)[] actions;
                switch ((mode ?? "").ToLowerInvariant())
                {
                    case "gaming": actions = GamingMode; break;
                    case "programming": case "dev": actions = DevMode; break;
                    case "office": actions = OfficeMode; break;
                    default: RespondError(id, "Unknown mode: " + mode); return;
                }
                int total = actions.Length, done = 0;
                EmitProgress(0, "Starting…");
                EmitLog("Activating " + mode + " mode…");
                foreach (var a in actions)
                {
                    EmitLog(a.label);
                    Commands.Run(a.cmd, l => EmitLog("  " + l));
                    EmitProgress(Pct(++done, total), a.label);
                }
                EmitLog(mode + " mode is now active.", "ok");
                EmitProgress(100, "Done");
                EmitDashboard();
                Respond(id, new { mode });
            }
            catch (Exception ex) { RespondError(id, ex.Message); }
        }

        // -------------------------------------------------------------- scan
        void Scan(string id)
        {
            try
            {
                EmitLog("Scanning installed applications…");
                EmitProgress(20, "Scanning…");
                string outp = Commands.RunCapture(
                    "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Get-AppxPackage | " +
                    "Where-Object { -not $_.IsFramework -and $_.NonRemovable -ne $true } | ForEach-Object { $_.Name }\"");
                var apps = (outp ?? "").Split('\n')
                    .Select(s => s.Trim()).Where(s => s.Length > 0)
                    .Distinct().OrderBy(s => s).ToList();
                foreach (var a in apps.Take(80)) EmitLog("Found: " + a);
                EmitProgress(100, "Done");
                Respond(id, new { found = apps.Count, apps });
            }
            catch (Exception ex) { RespondError(id, ex.Message); }
        }

        // ------------------------------------------------------------ revert
        void Revert(string id)
        {
            try
            {
                int total = Commands.RevertCommands.Length, done = 0;
                EmitProgress(0, "Reverting…");
                EmitLog("Reverting WinForge changes…");
                foreach (var c in Commands.RevertCommands)
                {
                    Commands.Run(c, l => EmitLog("  " + l));
                    EmitProgress(Pct(++done, total), "Reverting");
                }
                EmitLog("Revert complete. A restart is recommended.", "ok");
                EmitProgress(100, "Done");
                EmitDashboard();
                Respond(id, new { reverted = total });
            }
            catch (Exception ex) { RespondError(id, ex.Message); }
        }

        // -------------------------------------------------- registry backup
        static readonly string[] RegBackupBranches =
        {
            @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
            @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo",
            @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
            @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR",
            @"HKCU\SOFTWARE\Microsoft\GameBar",
            @"HKCU\System\GameConfigStore",
            @"HKCU\Control Panel\Mouse",
            @"HKLM\SOFTWARE\Policies\Microsoft\Windows",
            @"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers",
        };

        static string DoRegBackup()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "WinForge", "RegBackup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(folder);
            int i = 0;
            foreach (var branch in RegBackupBranches)
            {
                string file = Path.Combine(folder, (++i).ToString("00") + "_" +
                    branch.Replace('\\', '-').Replace(":", "").Replace("{", "").Replace("}", "") + ".reg");
                Commands.Run("reg export \"" + branch + "\" \"" + file + "\" /y", l => { });
            }
            return folder;
        }

        void BackupRegistry(string id)
        {
            try
            {
                EmitLog("Exporting registry branches…");
                string f = DoRegBackup();
                EmitLog("Saved to: " + f, "ok");
                Respond(id, new { path = f });
            }
            catch (Exception ex) { RespondError(id, ex.Message); }
        }

        // ------------------------------------------------------ self-update
        void CheckUpdates(string id)
        {
            string tag = null;
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "WinForge");
                    string json = wc.DownloadString(
                        "https://api.github.com/repos/" + RepoOwner + "/" + RepoName + "/releases/latest");
                    var m = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
                    if (m.Success) tag = m.Groups[1].Value;
                }
            }
            catch { }
            bool up = true;
            System.Version rv = ParseVer(tag), lv = ParseVer(Version);
            if (rv != null && lv != null) up = rv <= lv;
            Respond(id, new { current = "v" + Version, latest = tag ?? ("v" + Version), upToDate = up });
        }

        static System.Version ParseVer(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            var m = Regex.Match(s, "[0-9]+(\\.[0-9]+){1,3}");
            System.Version v;
            return m.Success && System.Version.TryParse(m.Value, out v) ? v : null;
        }
    }
}
