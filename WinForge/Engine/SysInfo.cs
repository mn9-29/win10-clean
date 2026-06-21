using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

namespace WinForge.Engine
{
    public class StatusRow
    {
        public string Label { get; set; }
        public bool Ok { get; set; }
    }

    public class DashboardDto
    {
        public string Os;
        public string OsBuild;
        public string Computer;
        public string User;
        public string Cpu;
        public int CpuLoad;
        public int CpuCores;
        public double MemUsedGB;
        public double MemTotalGB;
        public string Gpu;
        public string DiskLabel;
        public double DiskUsedGB;
        public double DiskTotalGB;
        public string Uptime;
        public double RamPillGB;
        public List<StatusRow> Status;
    }

    public static class SysInfo
    {
        // ---- live RAM meter (GlobalMemoryStatusEx) ----
        [StructLayout(LayoutKind.Sequential)]
        private class MemoryStatusEx
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
        private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx lpBuffer);

        [DllImport("kernel32.dll")]
        private static extern ulong GetTickCount64();

        public static DashboardDto Get()
        {
            var d = new DashboardDto { Status = new List<StatusRow>() };

            // ---- Host ----
            try { d.Computer = Environment.MachineName; } catch { d.Computer = ""; }
            try { d.User = Environment.UserName; } catch { d.User = ""; }

            // ---- OS ----
            try
            {
                string product = RegRead(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName", "Windows");
                string disp = RegRead(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DisplayVersion", "");
                string build = RegRead(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "CurrentBuild", "");
                string ubr = RegRead(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "UBR", "");
                // Windows 11 still reports "Windows 10" in ProductName; correct it by build.
                int buildNum;
                if (int.TryParse(build, out buildNum) && buildNum >= 22000)
                    product = product.Replace("Windows 10", "Windows 11");
                string ver = product;
                if (!string.IsNullOrEmpty(disp)) ver += "  " + disp;
                d.Os = ver;
                d.OsBuild = string.IsNullOrEmpty(build) ? "" : build + (string.IsNullOrEmpty(ubr) ? "" : "." + ubr);
            }
            catch { d.Os = "Windows"; d.OsBuild = ""; }

            // ---- CPU ----
            try { d.Cpu = RegRead(Registry.LocalMachine, @"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString", "-").Trim(); }
            catch { d.Cpu = "-"; }
            try { d.CpuCores = Environment.ProcessorCount; } catch { d.CpuCores = 0; }
            d.CpuLoad = GetCpuLoad();

            // ---- Memory ----
            try
            {
                var m = new MemoryStatusEx();
                if (GlobalMemoryStatusEx(m))
                {
                    d.MemTotalGB = m.ullTotalPhys / 1073741824.0;
                    d.MemUsedGB = (m.ullTotalPhys - m.ullAvailPhys) / 1073741824.0;
                    d.RamPillGB = d.MemUsedGB;
                }
            }
            catch { }

            // ---- GPU ----
            try { d.Gpu = RegRead(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000", "DriverDesc", "-"); }
            catch { d.Gpu = "-"; }

            // ---- Disk ----
            try
            {
                var c = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
                d.DiskTotalGB = c.TotalSize / 1073741824.0;
                double freeGB = c.AvailableFreeSpace / 1073741824.0;
                d.DiskUsedGB = d.DiskTotalGB - freeGB;
                string label = null;
                try { label = c.VolumeLabel; } catch { }
                d.DiskLabel = string.IsNullOrEmpty(label) ? c.Name : label;
            }
            catch { }

            // ---- Uptime ----
            try
            {
                ulong ms = GetTickCount64();
                var up = TimeSpan.FromMilliseconds(ms);
                d.Uptime = string.Format("{0}d {1}h {2}m", up.Days, up.Hours, up.Minutes);
            }
            catch { d.Uptime = ""; }

            // ---- Status rows ----
            d.Status.Add(new StatusRow { Label = "Administrator rights", Ok = IsAdmin() });
            d.Status.Add(new StatusRow { Label = "Telemetry disabled", Ok = RegReadInt(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry") == 0 });
            d.Status.Add(new StatusRow { Label = "Cortana disabled", Ok = RegReadInt(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana") == 0 });
            d.Status.Add(new StatusRow { Label = "Game DVR off", Ok = RegReadInt(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled") == 0 });
            d.Status.Add(new StatusRow { Label = "Advertising ID off", Ok = RegReadInt(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled") == 0 });
            d.Status.Add(new StatusRow { Label = "winget available", Ok = WingetAvailable() });

            return d;
        }

        private static int GetCpuLoad()
        {
            try
            {
                using (var counter = new PerformanceCounter("Processor", "% Processor Time", "_Total"))
                {
                    counter.NextValue();
                    System.Threading.Thread.Sleep(200);
                    int load = (int)Math.Round(counter.NextValue());
                    if (load < 0) load = 0;
                    if (load > 100) load = 100;
                    return load;
                }
            }
            catch { return 0; }
        }

        private static bool IsAdmin()
        {
            try
            {
                using (var id = WindowsIdentity.GetCurrent())
                {
                    var p = new WindowsPrincipal(id);
                    return p.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch { return false; }
        }

        private static bool WingetAvailable()
        {
            try
            {
                string output = Commands.RunCapture("where winget");
                return output != null && output.IndexOf("winget.exe", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
        }

        private static string RegRead(RegistryKey root, string path, string name, string fallback)
        {
            try { using (var k = root.OpenSubKey(path)) { var v = k?.GetValue(name); return v != null ? v.ToString() : fallback; } }
            catch { return fallback; }
        }

        // Returns the DWORD value, or -1 if the value is missing.
        private static int RegReadInt(RegistryKey root, string path, string name)
        {
            try { using (var k = root.OpenSubKey(path)) { var v = k?.GetValue(name); return v is int i ? i : -1; } }
            catch { return -1; }
        }
    }
}
