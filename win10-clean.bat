@echo off
:: ============================================================================
::  win10-clean.bat   -   Version 1.0.1
::  Windows 10 Work-Device Setup / Debloat Script
::  - Removes useless built-in apps and games (Xbox, Candy Crush, Bing, 3D...)
::  - Disables telemetry, ads, Cortana and unneeded services
::  - KEEPS: Microsoft Edge, Microsoft Store, Office, Calculator,
::           Photos, Notepad, Paint, Snipping Tool, Sticky Notes
::  - Creates a System Restore Point BEFORE making any change (safe to run once)
::  Run as: Right click -> "Run as administrator"
:: ============================================================================

setlocal EnableExtensions EnableDelayedExpansion
set "WIN10CLEAN_VERSION=1.0.1"
title Windows 10 Work-Device Clean Setup  v%WIN10CLEAN_VERSION%
color 0A

:: ------------------------------------------------------------------ ADMIN ---
:: Re-launch elevated if we are not running as administrator
net session >nul 2>&1
if %errorlevel% NEQ 0 (
    echo Requesting administrator privileges...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs" 2>nul
    if errorlevel 1 (
        echo.
        echo Could not auto-elevate. Please right-click this file and choose
        echo "Run as administrator".
        echo.
        pause
    )
    exit /b
)

cls
echo ============================================================
echo        WINDOWS 10 - WORK DEVICE CLEAN SETUP  v%WIN10CLEAN_VERSION%
echo ============================================================
echo  This will:
echo    [+] Create a System Restore Point (safety backup)
echo    [+] Remove built-in junk apps and games
echo    [+] Disable telemetry, ads and useless services
echo    [+] Keep Edge, Store, Office and work tools
echo ------------------------------------------------------------
echo  Press CTRL+C now to cancel, or
pause
echo.

:: ------------------------------------------------------- RESTORE POINT ------
echo [1/6] Creating System Restore Point ...
:: Remove the 24h throttle so a restore point can always be created.
reg add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore" /v SystemRestorePointCreationFrequency /t REG_DWORD /d 0 /f >nul 2>&1
:: Single-line command + try/catch so a failure NEVER stops the script.
powershell -NoProfile -ExecutionPolicy Bypass -Command "try { Enable-ComputerRestore -Drive 'C:\' -ErrorAction Stop; Checkpoint-Computer -Description 'Before win10-clean' -RestorePointType 'MODIFY_SETTINGS' -ErrorAction Stop; Write-Host '    Restore point created.' } catch { Write-Host '    WARN: restore point skipped -' $_.Exception.Message }"
echo.

:: --------------------------------------------------- REMOVE BLOAT APPS ------
echo [2/6] Removing built-in junk apps and games ...
:: Build the list (essential/work apps are intentionally NOT listed).
set "REMOVE_APPS='Microsoft.3DBuilder','Microsoft.Microsoft3DViewer','Microsoft.Print3D'"
set "REMOVE_APPS=!REMOVE_APPS!,'Microsoft.BingNews','Microsoft.BingWeather','Microsoft.BingFinance','Microsoft.BingSports','Microsoft.BingSearch'"
set "REMOVE_APPS=!REMOVE_APPS!,'Microsoft.XboxApp','Microsoft.XboxGameOverlay','Microsoft.XboxGamingOverlay','Microsoft.XboxIdentityProvider','Microsoft.XboxSpeechToTextOverlay','Microsoft.Xbox.TCUI','Microsoft.GamingApp'"
set "REMOVE_APPS=!REMOVE_APPS!,'Microsoft.MicrosoftSolitaireCollection','Microsoft.MicrosoftMahjong','Microsoft.MinecraftUWP','king.com.CandyCrushSaga','king.com.CandyCrushSodaSaga','king.com.BubbleWitch3Saga'"
set "REMOVE_APPS=!REMOVE_APPS!,'Microsoft.ZuneMusic','Microsoft.ZuneVideo','Microsoft.Messaging','Microsoft.OneConnect','Microsoft.Wallet','Microsoft.Advertising.Xaml'"
set "REMOVE_APPS=!REMOVE_APPS!,'Microsoft.SkypeApp','Microsoft.GetHelp','Microsoft.Getstarted','Microsoft.MixedReality.Portal','Microsoft.People','Microsoft.WindowsFeedbackHub','Microsoft.WindowsMaps','Microsoft.YourPhone'"
set "REMOVE_APPS=!REMOVE_APPS!,'Microsoft.549981C3F5F10','Microsoft.WindowsAlarms','Microsoft.Todos','Clipchamp.Clipchamp','Microsoft.PowerAutomateDesktop','Microsoft.MicrosoftOfficeHub'"
set "REMOVE_APPS=!REMOVE_APPS!,'*Netflix*','*Spotify*','*Facebook*','*Twitter*','*Disney*','*TikTok*','*Hulu*','*Amazon*','*Instagram*','*Plex*','*Dropbox*','*Duolingo*','*Wunderlist*','*Asphalt*','*Royal*'"

:: One single PowerShell call handles the whole list (reliable, no caret/loop issues).
powershell -NoProfile -ExecutionPolicy Bypass -Command "$apps=@(!REMOVE_APPS!); foreach($a in $apps){ Write-Host ('    - '+$a); Get-AppxPackage -AllUsers $a | Remove-AppxPackage -ErrorAction SilentlyContinue; Get-AppxProvisionedPackage -Online | Where-Object { $_.DisplayName -like $a } | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue }"
echo     Done.
echo.

:: --------------------------------------------------- DISABLE SERVICES -------
echo [3/6] Disabling useless background services ...
:: Xbox, telemetry, maps, retail demo, fax, remote registry, etc.
for %%S in (XblAuthManager XblGameSave XboxNetApiSvc XboxGipSvc DiagTrack dmwappushservice MapsBroker RetailDemo WMPNetworkSvc Fax RemoteRegistry WpcMonSvc lfsvc) do (
    echo     - Disabling %%S
    sc stop "%%S" >nul 2>&1
    sc config "%%S" start= disabled >nul 2>&1
)
echo     Done.
echo.

:: ------------------------------------------- DISABLE TELEMETRY / ADS --------
echo [4/6] Disabling telemetry, ads and suggestions ...
:: Telemetry level -> 0 (Security)
reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection" /v AllowTelemetry /t REG_DWORD /d 0 /f >nul 2>&1
reg add "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection" /v AllowTelemetry /t REG_DWORD /d 0 /f >nul 2>&1
:: Disable consumer features / suggested apps installs
reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent" /v DisableWindowsConsumerFeatures /t REG_DWORD /d 1 /f >nul 2>&1
reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent" /v DisableSoftLanding /t REG_DWORD /d 1 /f >nul 2>&1
:: Disable Start menu / lock screen suggestions and ads (current user)
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" /v SilentInstalledAppsEnabled /t REG_DWORD /d 0 /f >nul 2>&1
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" /v SystemPaneSuggestionsEnabled /t REG_DWORD /d 0 /f >nul 2>&1
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" /v SubscribedContent-338388Enabled /t REG_DWORD /d 0 /f >nul 2>&1
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" /v SubscribedContent-338389Enabled /t REG_DWORD /d 0 /f >nul 2>&1
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" /v SubscribedContent-310093Enabled /t REG_DWORD /d 0 /f >nul 2>&1
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" /v PreInstalledAppsEnabled /t REG_DWORD /d 0 /f >nul 2>&1
:: Disable advertising ID
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo" /v Enabled /t REG_DWORD /d 0 /f >nul 2>&1
reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo" /v DisabledByGroupPolicy /t REG_DWORD /d 1 /f >nul 2>&1
:: Disable tips, tricks and Windows welcome experience
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" /v SubscribedContent-310091Enabled /t REG_DWORD /d 0 /f >nul 2>&1
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" /v SubscribedContent-338393Enabled /t REG_DWORD /d 0 /f >nul 2>&1
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" /v SubscribedContent-353694Enabled /t REG_DWORD /d 0 /f >nul 2>&1
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" /v SubscribedContent-353696Enabled /t REG_DWORD /d 0 /f >nul 2>&1
:: Disable activity history / timeline
reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows\System" /v EnableActivityFeed /t REG_DWORD /d 0 /f >nul 2>&1
reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows\System" /v PublishUserActivities /t REG_DWORD /d 0 /f >nul 2>&1
echo     Done.
echo.

:: ----------------------------------------------- DISABLE CORTANA/GAMES ------
echo [5/6] Disabling Cortana and Game DVR ...
reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search" /v AllowCortana /t REG_DWORD /d 0 /f >nul 2>&1
:: Disable Game Bar / Game DVR background recording (saves resources)
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR" /v AppCaptureEnabled /t REG_DWORD /d 0 /f >nul 2>&1
reg add "HKCU\System\GameConfigStore" /v GameDVR_Enabled /t REG_DWORD /d 0 /f >nul 2>&1
reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows\GameDVR" /v AllowGameDVR /t REG_DWORD /d 0 /f >nul 2>&1
echo     Done.
echo.

:: -------------------------------------------------- PERFORMANCE TWEAKS ------
echo [6/6] Applying light performance tweaks ...
:: Reduce startup delay for desktop apps
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Serialize" /v StartupDelayInMSec /t REG_DWORD /d 0 /f >nul 2>&1
:: Show file extensions (safer for work, helps spot fake files)
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced" /v HideFileExt /t REG_DWORD /d 0 /f >nul 2>&1
:: Disable Lock screen Spotlight ads -> use plain picture
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" /v RotatingLockScreenEnabled /t REG_DWORD /d 0 /f >nul 2>&1
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" /v RotatingLockScreenOverlayEnabled /t REG_DWORD /d 0 /f >nul 2>&1
echo     Done.
echo.

echo ============================================================
echo   FINISHED!  The device is now cleaned for work use.
echo   A restart is recommended to apply all changes.
echo ============================================================
echo.
choice /C YN /M "Restart now"
if errorlevel 2 goto :end
shutdown /r /t 5 /c "Restarting to finish win10-clean setup"

:end
echo You can restart later. Bye.
pause
endlocal
exit /b
