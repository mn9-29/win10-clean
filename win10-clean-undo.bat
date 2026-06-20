@echo off
:: ============================================================================
::  win10-clean-undo.bat   -   Version 1.2.0
::  Reverses the settings/services changed by win10-clean.bat:
::    * Re-enables the disabled services
::    * Removes the telemetry/ads/Cortana/GameDVR policy registry values
::    * Reverts gaming tweaks (mouse / visual effects / network latency)
::    * Restores the Balanced power plan
::    * Re-enables the telemetry scheduled tasks
::
::  NOTE: Removed Store apps are NOT reinstalled here - get them from the
::        Microsoft Store, or use a System Restore Point for a full rollback.
::  Run as: Right click -> "Run as administrator"
:: ============================================================================

setlocal EnableExtensions
title win10-clean - UNDO
color 0E

net session >nul 2>&1
if %errorlevel% NEQ 0 (
    echo Requesting administrator privileges...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs" 2>nul
    exit /b
)

cls
echo ============================================================
echo            WIN10-CLEAN  -  UNDO / RESTORE SETTINGS
echo ============================================================
echo  This reverses services and settings changed by win10-clean.
echo  (It does NOT reinstall removed apps - use the Store for that.)
echo ------------------------------------------------------------
pause
echo.

echo [1/4] Re-enabling services ...
:: Map services back to sensible default start types.
for %%S in (XblAuthManager XblGameSave XboxNetApiSvc XboxGipSvc dmwappushservice WMPNetworkSvc Fax WpcMonSvc lfsvc) do (
    echo     - %%S
    sc config "%%S" start= demand >nul 2>&1
)
:: These default to automatic / delayed-auto on a normal install.
sc config DiagTrack  start= auto  >nul 2>&1
sc config MapsBroker start= delayed-auto >nul 2>&1
echo     Done.
echo.

echo [2/4] Removing policy registry values ...
reg delete "HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection" /v AllowTelemetry /f >nul 2>&1
reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection" /v AllowTelemetry /f >nul 2>&1
reg delete "HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent" /v DisableWindowsConsumerFeatures /f >nul 2>&1
reg delete "HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent" /v DisableSoftLanding /f >nul 2>&1
reg delete "HKLM\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo" /v DisabledByGroupPolicy /f >nul 2>&1
reg delete "HKLM\SOFTWARE\Policies\Microsoft\Windows\System" /v EnableActivityFeed /f >nul 2>&1
reg delete "HKLM\SOFTWARE\Policies\Microsoft\Windows\System" /v PublishUserActivities /f >nul 2>&1
reg delete "HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search" /v AllowCortana /f >nul 2>&1
reg delete "HKLM\SOFTWARE\Policies\Microsoft\Windows\GameDVR" /v AllowGameDVR /f >nul 2>&1
:: Re-enable Start menu suggestions / ads (current user)
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" /v SilentInstalledAppsEnabled /t REG_DWORD /d 1 /f >nul 2>&1
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" /v SystemPaneSuggestionsEnabled /t REG_DWORD /d 1 /f >nul 2>&1
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo" /v Enabled /t REG_DWORD /d 1 /f >nul 2>&1
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR" /v AppCaptureEnabled /t REG_DWORD /d 1 /f >nul 2>&1
reg add "HKCU\System\GameConfigStore" /v GameDVR_Enabled /t REG_DWORD /d 1 /f >nul 2>&1
echo     Done.
echo.

echo [3/5] Reverting gaming tweaks (mouse / visuals / network) ...
:: Restore default mouse acceleration
reg add "HKCU\Control Panel\Mouse" /v MouseSpeed /t REG_SZ /d 1 /f >nul 2>&1
reg add "HKCU\Control Panel\Mouse" /v MouseThreshold1 /t REG_SZ /d 6 /f >nul 2>&1
reg add "HKCU\Control Panel\Mouse" /v MouseThreshold2 /t REG_SZ /d 10 /f >nul 2>&1
:: Let Windows manage visual effects again
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects" /v VisualFXSetting /t REG_DWORD /d 0 /f >nul 2>&1
:: Remove Nagle (low-latency) tweaks from all network interfaces
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem 'HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces' | ForEach-Object { Remove-ItemProperty -Path $_.PSPath -Name TcpAckFrequency -ErrorAction SilentlyContinue; Remove-ItemProperty -Path $_.PSPath -Name TCPNoDelay -ErrorAction SilentlyContinue }"
echo     Done.
echo.

echo [4/5] Restoring Balanced power plan ...
powercfg -setactive 381b4222-f694-41f0-9685-ff5bb260df2e >nul 2>&1
echo     Done.
echo.

echo [5/5] Re-enabling scheduled tasks ...
for %%T in (
    "\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser"
    "\Microsoft\Windows\Application Experience\ProgramDataUpdater"
    "\Microsoft\Windows\Customer Experience Improvement Program\Consolidator"
    "\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip"
    "\Microsoft\Windows\Feedback\Siuf\DmClient"
    "\Microsoft\Windows\Feedback\Siuf\DmClientOnScenarioDownload"
) do schtasks /Change /TN %%T /Enable >nul 2>&1
echo     Done.
echo.

echo ============================================================
echo   Settings restored. A restart is recommended.
echo   For removed apps, reinstall them from the Microsoft Store.
echo ============================================================
pause
endlocal
exit /b
