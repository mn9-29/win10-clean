using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WinForge.Engine
{
    public class ProcInfo
    {
        public string Name { get; set; }
        public int Pid { get; set; }
        public double MemMB { get; set; }
        public double Cpu { get; set; }
    }

    public class DiskInfo
    {
        public string Name { get; set; }
        public double TotalGB { get; set; }
        public double FreeGB { get; set; }
        public double UsedGB { get; set; }
    }

    public class ResourcesDto
    {
        public int CpuTotal;                 // 0-100
        public int[] CpuPerCore;             // one entry per logical core, 0-100
        public int CpuCores;                 // Environment.ProcessorCount
        public double MemTotalMB;
        public double MemUsedMB;
        public double MemAvailMB;
        public int MemPercent;               // 0-100
        public string Uptime;                // "Xd Yh Zm"
        public List<DiskInfo> Disks;
        public List<ProcInfo> TopByMem;      // top ~10 by working set
        public List<ProcInfo> TopByCpu;      // top ~10 by CPU over a short sample
    }

    public static class Resources
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

        private const double Mb = 1024.0 * 1024.0;
        private const double Gb = 1024.0 * 1024.0 * 1024.0;

        public static ResourcesDto Get()
        {
            var dto = new ResourcesDto
            {
                CpuPerCore = new int[0],
                Disks = new List<DiskInfo>(),
                TopByMem = new List<ProcInfo>(),
                TopByCpu = new List<ProcInfo>(),
                Uptime = ""
            };

            try { dto.CpuCores = Environment.ProcessorCount; } catch { dto.CpuCores = 0; }

            // ---- Memory ----
            try
            {
                var m = new MemoryStatusEx();
                if (GlobalMemoryStatusEx(m))
                {
                    dto.MemTotalMB = m.ullTotalPhys / Mb;
                    dto.MemAvailMB = m.ullAvailPhys / Mb;
                    dto.MemUsedMB = (m.ullTotalPhys - m.ullAvailPhys) / Mb;
                    int pct = (int)m.dwMemoryLoad;
                    if (pct < 0) pct = 0;
                    if (pct > 100) pct = 100;
                    dto.MemPercent = pct;
                }
            }
            catch { }

            // ---- Uptime ----
            try
            {
                ulong ms = GetTickCount64();
                var up = TimeSpan.FromMilliseconds(ms);
                dto.Uptime = string.Format("{0}d {1}h {2}m", up.Days, up.Hours, up.Minutes);
            }
            catch { dto.Uptime = ""; }

            // ---- CPU (total + per core) ----
            FillCpu(dto);

            // ---- Disks ----
            try
            {
                foreach (var d in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (!d.IsReady || d.DriveType != DriveType.Fixed) continue;
                        double total = d.TotalSize / Gb;
                        double free = d.TotalFreeSpace / Gb;
                        dto.Disks.Add(new DiskInfo
                        {
                            Name = d.Name,
                            TotalGB = total,
                            FreeGB = free,
                            UsedGB = total - free
                        });
                    }
                    catch { /* skip unreadable drive */ }
                }
            }
            catch { }

            // ---- Processes ----
            FillProcesses(dto);

            return dto;
        }

        private static void FillCpu(ResourcesDto dto)
        {
            try
            {
                var category = new PerformanceCounterCategory("Processor");
                string[] instances = category.GetInstanceNames();

                var coreCounters = new List<KeyValuePair<int, PerformanceCounter>>();
                PerformanceCounter totalCounter = null;
                try
                {
                    foreach (var inst in instances)
                    {
                        try
                        {
                            if (string.Equals(inst, "_Total", StringComparison.OrdinalIgnoreCase))
                            {
                                totalCounter = new PerformanceCounter("Processor", "% Processor Time", inst);
                            }
                            else
                            {
                                int idx;
                                if (int.TryParse(inst, out idx))
                                {
                                    coreCounters.Add(new KeyValuePair<int, PerformanceCounter>(
                                        idx, new PerformanceCounter("Processor", "% Processor Time", inst)));
                                }
                            }
                        }
                        catch { /* skip bad instance */ }
                    }

                    if (totalCounter == null && coreCounters.Count == 0)
                    {
                        FillCpuFallback(dto);
                        return;
                    }

                    // Prime all counters.
                    if (totalCounter != null) { try { totalCounter.NextValue(); } catch { } }
                    foreach (var c in coreCounters) { try { c.Value.NextValue(); } catch { } }

                    System.Threading.Thread.Sleep(250);

                    if (totalCounter != null)
                    {
                        try { dto.CpuTotal = Clamp(totalCounter.NextValue()); } catch { dto.CpuTotal = 0; }
                    }

                    var ordered = coreCounters.OrderBy(c => c.Key).ToList();
                    var perCore = new int[ordered.Count];
                    for (int i = 0; i < ordered.Count; i++)
                    {
                        try { perCore[i] = Clamp(ordered[i].Value.NextValue()); }
                        catch { perCore[i] = 0; }
                    }
                    dto.CpuPerCore = perCore;
                }
                finally
                {
                    if (totalCounter != null) { try { totalCounter.Dispose(); } catch { } }
                    foreach (var c in coreCounters) { try { c.Value.Dispose(); } catch { } }
                }
            }
            catch
            {
                FillCpuFallback(dto);
            }
        }

        private static void FillCpuFallback(ResourcesDto dto)
        {
            dto.CpuPerCore = new int[0];
            try
            {
                using (var counter = new PerformanceCounter("Processor", "% Processor Time", "_Total"))
                {
                    counter.NextValue();
                    System.Threading.Thread.Sleep(250);
                    dto.CpuTotal = Clamp(counter.NextValue());
                }
            }
            catch { dto.CpuTotal = 0; }
        }

        private static int Clamp(float value)
        {
            int v = (int)Math.Round(value);
            if (v < 0) v = 0;
            if (v > 100) v = 100;
            return v;
        }

        private static void FillProcesses(ResourcesDto dto)
        {
            Process[] processes;
            try { processes = Process.GetProcesses(); }
            catch { return; }

            try
            {
                // Build a memory-sorted list of usable processes (working set + name + pid).
                var byMem = new List<KeyValuePair<Process, long>>();
                foreach (var p in processes)
                {
                    try
                    {
                        long ws = p.WorkingSet64;
                        byMem.Add(new KeyValuePair<Process, long>(p, ws));
                    }
                    catch { /* skip processes that throw on access */ }
                }

                var memSorted = byMem.OrderByDescending(kv => kv.Value).ToList();

                // ---- TopByMem ----
                foreach (var kv in memSorted.Take(10))
                {
                    try
                    {
                        dto.TopByMem.Add(new ProcInfo
                        {
                            Name = kv.Key.ProcessName,
                            Pid = kv.Key.Id,
                            MemMB = kv.Value / Mb,
                            Cpu = 0
                        });
                    }
                    catch { /* skip */ }
                }

                // ---- TopByCpu ----
                // Sample CPU time across the top ~40 by memory.
                var sampleSet = memSorted.Take(40).ToList();
                var first = new List<CpuSample>();
                var sw = Stopwatch.StartNew();
                foreach (var kv in sampleSet)
                {
                    try
                    {
                        var p = kv.Key;
                        first.Add(new CpuSample
                        {
                            Proc = p,
                            Pid = p.Id,
                            Name = p.ProcessName,
                            MemMB = kv.Value / Mb,
                            CpuMs = p.TotalProcessorTime.TotalMilliseconds
                        });
                    }
                    catch { /* skip access denied / exited */ }
                }

                System.Threading.Thread.Sleep(300);
                sw.Stop();

                double elapsedMs = sw.Elapsed.TotalMilliseconds;
                if (elapsedMs < 1) elapsedMs = 1;
                int cores = dto.CpuCores > 0 ? dto.CpuCores : 1;

                var cpuResults = new List<ProcInfo>();
                foreach (var s in first)
                {
                    try
                    {
                        double after = s.Proc.TotalProcessorTime.TotalMilliseconds;
                        double deltaMs = after - s.CpuMs;
                        if (deltaMs < 0) deltaMs = 0;
                        double cpuPct = (deltaMs / (elapsedMs * cores)) * 100.0;
                        if (cpuPct < 0) cpuPct = 0;
                        if (cpuPct > 100) cpuPct = 100;
                        cpuResults.Add(new ProcInfo
                        {
                            Name = s.Name,
                            Pid = s.Pid,
                            MemMB = s.MemMB,
                            Cpu = Math.Round(cpuPct, 1)
                        });
                    }
                    catch { /* skip exited / access denied */ }
                }

                foreach (var r in cpuResults.OrderByDescending(r => r.Cpu).Take(10))
                    dto.TopByCpu.Add(r);
            }
            catch { /* never throw */ }
            finally
            {
                foreach (var p in processes)
                {
                    try { p.Dispose(); } catch { }
                }
            }
        }

        private class CpuSample
        {
            public Process Proc;
            public int Pid;
            public string Name;
            public double MemMB;
            public double CpuMs;
        }
    }
}
