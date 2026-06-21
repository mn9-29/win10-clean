using System;
using System.Diagnostics;

namespace WinForge.Engine
{
    // Headless port of the command helpers and runner from MainWindow.xaml.cs.
    public static class Commands
    {
        // ---------------------------------------------------------- command helpers
        public static string RemoveAppx(string name) =>
            "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Get-AppxPackage -AllUsers '" + name +
            "' | Remove-AppxPackage -ErrorAction SilentlyContinue; Get-AppxProvisionedPackage -Online | " +
            "Where-Object { $_.DisplayName -like '" + name + "' } | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue\"";

        public static string RegDword(string path, string val, int data) =>
            "reg add \"" + path + "\" /v " + val + " /t REG_DWORD /d " + data + " /f";

        public static string RegSz(string path, string val, string data) =>
            "reg add \"" + path + "\" /v " + val + " /t REG_SZ /d " + data + " /f";

        public static string DisableSvc(string svc) =>
            "sc stop " + svc + " & sc config " + svc + " start= disabled";

        public static string Winget(string id) =>
            "winget install -e --id " + id + " --silent --accept-package-agreements --accept-source-agreements";

        // Disable a startup entry by matching name pattern (uses the same
        // StartupApproved flag Task Manager uses, so it is reversible there).
        public static string DisableStartup(string pattern) =>
            "powershell -NoProfile -ExecutionPolicy Bypass -Command \"$pat='" + pattern.Replace("'", "''") + "'; " +
            "$run='HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run'; " +
            "$appr='HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\StartupApproved\\Run'; " +
            "if(!(Test-Path $appr)){New-Item $appr -Force | Out-Null}; " +
            "if(Test-Path $run){ (Get-Item $run).Property | Where-Object { $_ -like $pat } | ForEach-Object { " +
            "New-ItemProperty -Path $appr -Name $_ -Value ([byte[]](3,0,0,0,0,0,0,0,0,0,0,0)) -PropertyType Binary -Force | Out-Null } }\"";

        // Writes %USERPROFILE%\.wslconfig (memory cap + auto memory reclaim).
        public const string WslConfigCmd =
            "(echo [wsl2]& echo memory=8GB& echo processors=4& echo swap=2GB& echo.& " +
            "echo [experimental]& echo autoMemoryReclaim=gradual& echo sparseVhd=true) > \"%USERPROFILE%\\.wslconfig\"";

        public const string RestoreCmd =
            "powershell -NoProfile -ExecutionPolicy Bypass -Command \"try { Enable-ComputerRestore -Drive 'C:\\' -ErrorAction Stop; " +
            "Checkpoint-Computer -Description 'Before WinForge' -RestorePointType 'MODIFY_SETTINGS' -ErrorAction Stop; " +
            "'Restore point created.' } catch { 'WARN: restore point skipped - ' + $_.Exception.Message }\"";

        // Mirrors winforge-undo.bat
        public static readonly string[] RevertCommands =
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
            "reg delete \"HKCU\\Software\\Classes\\CLSID\\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\" /f",
            // Restore Win11 taskbar defaults
            "reg add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v TaskbarAl /t REG_DWORD /d 1 /f",
            "reg add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v TaskbarDa /t REG_DWORD /d 1 /f",
            "reg add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v TaskbarMn /t REG_DWORD /d 1 /f",
            "reg add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Search\" /v SearchboxTaskbarMode /t REG_DWORD /d 1 /f",
            "reg delete \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v ShowSecondsInSystemClock /f",
            // Reset DNS back to automatic (DHCP) on all adapters
            "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Get-NetAdapter | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.ifIndex -ResetServerAddresses -ErrorAction SilentlyContinue }\"",
            // Re-enable automatic Windows Update
            "reg delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate\" /v DeferFeatureUpdatesPeriodInDays /f",
            // Restore advertising info policy
            "reg delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\AdvertisingInfo\" /v DisabledByGroupPolicy /f"
        };

        // ------------------------------------------------------------------- runner
        // Runs a command via cmd.exe /c, streaming combined stdout+stderr to onLine
        // (one call per non-empty chunk). Blocks until exit.
        public static void Run(string command, Action<string> onLine)
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
                        onLine?.Invoke(combined);
                }
            }
            catch (Exception ex)
            {
                onLine?.Invoke("ERROR: " + ex.Message);
            }
        }

        // Runs a command and returns its combined stdout/stderr (no logging).
        public static string RunCapture(string command)
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
    }
}
