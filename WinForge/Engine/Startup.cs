using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WinForge.Engine
{
    public class StartupEntry
    {
        public string Id { get; set; }       // stable, encodes hive+kind+name
        public string Name { get; set; }      // the value name / shortcut name
        public string Command { get; set; }   // the launch path/command
        public string Location { get; set; }  // human label
        public string Scope { get; set; }     // "user" or "machine"
        public bool Enabled { get; set; }
    }

    public static class Startup
    {
        // Id separator unlikely to appear in a value name / shortcut name.
        private const string Sep = "|::|";

        // Run-key registry paths.
        private const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string Run32Path = @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Run";

        // StartupApproved subkeys (relative to the Explorer key).
        private const string ApprovedBase = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";
        private const string ApprovedRun = ApprovedBase + @"\Run";
        private const string ApprovedRun32 = ApprovedBase + @"\Run32";
        private const string ApprovedFolder = ApprovedBase + @"\StartupFolder";

        public static List<StartupEntry> List()
        {
            var result = new List<StartupEntry>();

            // 1. HKCU Run
            AddRunEntries(result, Registry.CurrentUser, RunPath, "run", "user", "HKCU Run",
                          Registry.CurrentUser, ApprovedRun);

            // 2. HKLM Run
            AddRunEntries(result, Registry.LocalMachine, RunPath, "run", "machine", "HKLM Run",
                          Registry.LocalMachine, ApprovedRun);

            // 3. HKLM Run (32-bit, Wow6432Node)
            AddRunEntries(result, Registry.LocalMachine, Run32Path, "run32", "machine", "HKLM Run (32-bit)",
                          Registry.LocalMachine, ApprovedRun32);

            // 4. User Startup folder shortcuts
            string userStartup = SafeFolder(Environment.SpecialFolder.Startup);
            AddFolderEntries(result, userStartup, "folder", "user", "Startup folder",
                             Registry.CurrentUser, ApprovedFolder);

            // 5. Common Startup folder shortcuts
            string commonStartup = SafeFolder(Environment.SpecialFolder.CommonStartup);
            AddFolderEntries(result, commonStartup, "folderCommon", "machine", "Common Startup folder",
                             Registry.LocalMachine, ApprovedFolder);

            return result;
        }

        private static string SafeFolder(Environment.SpecialFolder folder)
        {
            try { return Environment.GetFolderPath(folder); }
            catch { return null; }
        }

        private static void AddRunEntries(List<StartupEntry> result, RegistryKey runRoot, string runPath,
                                          string kind, string scope, string location,
                                          RegistryKey approvedRoot, string approvedPath)
        {
            try
            {
                using (var k = runRoot.OpenSubKey(runPath))
                {
                    if (k == null) return;
                    foreach (var name in k.GetValueNames())
                    {
                        if (string.IsNullOrEmpty(name)) continue;
                        try
                        {
                            object v = k.GetValue(name);
                            string command = v != null ? v.ToString() : "";
                            bool enabled = ReadApprovedEnabled(approvedRoot, approvedPath, name);
                            result.Add(new StartupEntry
                            {
                                Id = MakeId(kind, scope, name),
                                Name = name,
                                Command = command,
                                Location = location,
                                Scope = scope,
                                Enabled = enabled
                            });
                        }
                        catch { /* skip unreadable value */ }
                    }
                }
            }
            catch { /* skip unreadable key */ }
        }

        private static void AddFolderEntries(List<StartupEntry> result, string folder,
                                             string kind, string scope, string location,
                                             RegistryKey approvedRoot, string approvedPath)
        {
            if (string.IsNullOrEmpty(folder)) return;
            try
            {
                if (!Directory.Exists(folder)) return;
                foreach (var file in Directory.GetFiles(folder))
                {
                    try
                    {
                        string fileName = Path.GetFileName(file);
                        // Skip desktop.ini and similar non-shortcut housekeeping files.
                        if (string.Equals(fileName, "desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                        string name = Path.GetFileNameWithoutExtension(file);
                        if (string.IsNullOrEmpty(name)) continue;
                        // StartupApproved keys the entry by the full file name (with extension).
                        bool enabled = ReadApprovedEnabled(approvedRoot, approvedPath, fileName);
                        result.Add(new StartupEntry
                        {
                            Id = MakeId(kind, scope, fileName),
                            Name = name,
                            Command = file,
                            Location = location,
                            Scope = scope,
                            Enabled = enabled
                        });
                    }
                    catch { /* skip unreadable file */ }
                }
            }
            catch { /* skip unreadable folder */ }
        }

        // enabled = no value, OR first byte has low bit clear (even).
        private static bool ReadApprovedEnabled(RegistryKey root, string approvedPath, string name)
        {
            try
            {
                using (var k = root.OpenSubKey(approvedPath))
                {
                    if (k == null) return true;
                    var v = k.GetValue(name) as byte[];
                    return (v == null) || (v.Length > 0 && (v[0] & 1) == 0);
                }
            }
            catch { return true; }
        }

        public static bool SetEnabled(string id, bool enabled)
        {
            try
            {
                if (string.IsNullOrEmpty(id)) return false;
                string[] parts = id.Split(new[] { Sep }, StringSplitOptions.None);
                if (parts.Length < 3) return false;
                string kind = parts[0];
                string scope = parts[1];
                // Rejoin the remainder defensively; the chosen separator won't appear in a
                // real value/shortcut name, so this normally yields the name as-is.
                string name = string.Join(Sep, parts.Skip(2));

                RegistryKey root;
                string approvedPath;
                if (!ResolveApprovedTarget(kind, scope, out root, out approvedPath)) return false;

                byte[] data = new byte[12];
                if (enabled)
                {
                    data[0] = 2; // enabled
                }
                else
                {
                    data[0] = 3; // disabled (low bit set)
                    try
                    {
                        long ft = DateTime.Now.ToFileTime();
                        byte[] ftBytes = BitConverter.GetBytes(ft); // little-endian on Windows
                        // bytes[4..11] hold the FILETIME timestamp.
                        Array.Copy(ftBytes, 0, data, 4, 8);
                    }
                    catch { /* zeros are acceptable */ }
                }

                using (var k = root.CreateSubKey(approvedPath))
                {
                    if (k == null) return false;
                    k.SetValue(name, data, RegistryValueKind.Binary);
                }
                return true;
            }
            catch { return false; }
        }

        private static bool ResolveApprovedTarget(string kind, string scope, out RegistryKey root, out string approvedPath)
        {
            root = null;
            approvedPath = null;
            bool machine = string.Equals(scope, "machine", StringComparison.Ordinal);
            switch (kind)
            {
                case "run":
                    // HKCU Run -> HKCU StartupApproved\Run; HKLM Run -> HKLM StartupApproved\Run.
                    root = machine ? Registry.LocalMachine : Registry.CurrentUser;
                    approvedPath = ApprovedRun;
                    return true;
                case "run32":
                    // 32-bit Run lives only under HKLM\Wow6432Node.
                    root = Registry.LocalMachine;
                    approvedPath = ApprovedRun32;
                    return true;
                case "folder":
                    // User Startup folder -> HKCU StartupApproved\StartupFolder.
                    root = Registry.CurrentUser;
                    approvedPath = ApprovedFolder;
                    return true;
                case "folderCommon":
                    // Common Startup folder -> HKLM StartupApproved\StartupFolder.
                    root = Registry.LocalMachine;
                    approvedPath = ApprovedFolder;
                    return true;
                default:
                    return false;
            }
        }

        private static string MakeId(string kind, string scope, string name)
        {
            return kind + Sep + scope + Sep + name;
        }
    }
}
