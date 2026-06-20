@echo off
:: ============================================================================
::  win10-clean.bat   -   Version 2.4.0
::  Windows 10 / 11 Device Setup & Debloat - interactive edition
::  (Prefer the GUI app 'win10-clean-gui.exe' for point-and-click control.)
::
::  Three modes:
::    WORK    - office device: removes Xbox/games, disables Game DVR, etc.
::    GAMING  - keeps Xbox & Game Bar, enables Game Mode + gaming tweaks.
::    BASIC   - home/family PC: junk + ads/telemetry removal only (gentle).
::  All modes share: junk-app removal, telemetry/ads off, cleanup, logging.
::
::  KEEPS (work): Edge, Store, Office, Calculator, Photos, Notepad, Paint,
::               Snipping Tool, Sticky Notes.
::  Undo:  run win10-clean-undo.bat  (or use the restore point).
::  Run as: Right click -> "Run as administrator"
:: ============================================================================

setlocal EnableExtensions EnableDelayedExpansion
set "WIN10CLEAN_VERSION=2.4.0"
title Windows Device Clean Setup  v%WIN10CLEAN_VERSION%
color 0A

:: ------------------------------------------------------------------ ADMIN ---
net session >nul 2>&1
if %errorlevel% NEQ 0 (
    echo Requesting administrator privileges...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs" 2>nul
    if errorlevel 1 (
        echo.
        echo Could not auto-elevate. Right-click this file and choose
        echo "Run as administrator".
        echo.
        pause
    )
    exit /b
)

:: -------------------------------------------------------------------- LOG ---
for /f %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmmss"') do set "TS=%%i"
set "LOG=%~dp0win10-clean_%TS%.log"
call :log "=== win10-clean v%WIN10CLEAN_VERSION% started ==="

:: ----------------------------------------------------------- MODE SELECT ----
:mode_select
cls
echo ============================================================
echo        WINDOWS DEVICE CLEAN SETUP   v%WIN10CLEAN_VERSION%
echo ============================================================
echo   What is this device mainly used for?
echo ------------------------------------------------------------
echo   [1] Office / Work   (removes Xbox ^& games, max focus)
echo   [2] Gaming          (keeps Xbox/Game Bar, gaming tweaks)
echo   [3] Basic / Home    (gentle: junk ^& ads removal only)
echo   [0] Exit
echo ============================================================
choice /C 1230 /N /M "Choose: "
set "MS=%errorlevel%"
if "%MS%"=="1" ( set "MODE=WORK"   & goto :menu )
if "%MS%"=="2" ( set "MODE=GAMING" & goto :menu )
if "%MS%"=="3" ( set "MODE=BASIC"  & goto :menu )
goto :end

:: ------------------------------------------------------------------- MENU ---
:menu
cls
echo ============================================================
echo    %MODE% MODE  -  win10-clean v%WIN10CLEAN_VERSION%
echo ============================================================
echo   [1] Quick clean        (recommended - Medium profile)
echo   [2] Clean with profile (choose Light / Medium / Strong)
echo   [3] Install apps       (winget)
echo   [4] Extra cleanup ^& performance
echo   [5] FULL setup         (clean + apps + extra)
echo   [6] How to undo / restore
echo   [7] WSL ^& startup optimization
echo   [9] Back to mode select
echo   [0] Exit
echo ------------------------------------------------------------
echo   Log file: %LOG%
echo ============================================================
choice /C 123456790 /N /M "Choose an option: "
set "OPT=%errorlevel%"
if "%OPT%"=="1" ( set "PROFILE=MEDIUM" & goto :do_clean )
if "%OPT%"=="2" ( goto :pick_profile )
if "%OPT%"=="3" ( call :restore_point & call :install_apps & goto :after )
if "%OPT%"=="4" ( call :restore_point & call :extra_cleanup & goto :after )
if "%OPT%"=="5" ( set "PROFILE=MEDIUM" & set "DO_FULL=1" & goto :do_clean )
if "%OPT%"=="6" ( goto :undo_info )
if "%OPT%"=="7" ( call :wsl_startup & goto :after )
if "%OPT%"=="8" ( goto :mode_select )
if "%OPT%"=="9" ( goto :end )
goto :menu

:pick_profile
cls
echo ------------------------------------------------------------
echo   Choose how aggressive the junk-app removal should be:
echo ------------------------------------------------------------
echo   [1] Light   - games + 3rd-party junk only (safest)
echo   [2] Medium  - light + most Microsoft bloat (recommended)
echo   [3] Strong  - medium + extra apps (most aggressive)
echo   [0] Back
echo ------------------------------------------------------------
choice /C 1230 /N /M "Profile: "
set "P=%errorlevel%"
if "%P%"=="1" ( set "PROFILE=LIGHT"  & goto :do_clean )
if "%P%"=="2" ( set "PROFILE=MEDIUM" & goto :do_clean )
if "%P%"=="3" ( set "PROFILE=STRONG" & goto :do_clean )
goto :menu

:: -------------------------------------------------------- MAIN CLEAN FLOW ---
:do_clean
call :log "Mode: %MODE% | Profile: %PROFILE%"
call :restore_point
call :remove_apps
call :disable_telemetry
:: BASIC mode stops here (gentle): only junk apps + ads/telemetry.
if /I not "%MODE%"=="BASIC" (
    call :disable_services
    call :disable_cortana
    if /I "%MODE%"=="GAMING" ( call :gaming_tweaks ) else ( call :disable_gamedvr & call :perf_tweaks )
)
if "%DO_FULL%"=="1" (
    call :install_apps
    call :extra_cleanup
)
goto :after

:after
call :log "=== Finished ==="
echo.
echo ============================================================
echo   FINISHED!  Details saved to:
echo   %LOG%
echo ============================================================
echo.
choice /C YN /N /M "Restart now to apply all changes? [Y/N]: "
if errorlevel 2 goto :end
shutdown /r /t 5 /c "Restarting to finish win10-clean setup"
goto :end

:: ============================================================================
::  SUBROUTINES
:: ============================================================================

:: ------------------------------------------------------- RESTORE POINT ------
:restore_point
echo.
call :log "[Restore point] Creating System Restore Point..."
reg add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore" /v SystemRestorePointCreationFrequency /t REG_DWORD /d 0 /f >nul 2>&1
powershell -NoProfile -ExecutionPolicy Bypass -Command "try { Enable-ComputerRestore -Drive 'C:\' -ErrorAction Stop; Checkpoint-Computer -Description 'Before win10-clean' -RestorePointType 'MODIFY_SETTINGS' -ErrorAction Stop; Write-Host '    Restore point created.' } catch { Write-Host '    WARN: restore point skipped -' $_.Exception.Message }"
exit /b

:: --------------------------------------------------- REMOVE BLOAT APPS ------
:remove_apps
echo.
call :log "[Apps] Removing junk apps (mode: %MODE%, profile: %PROFILE%)..."
:: --- Games & 3rd-party junk (removed in BOTH modes) ---
set "APPS='Microsoft.MicrosoftSolitaireCollection','Microsoft.MicrosoftMahjong','king.com.CandyCrushSaga','king.com.CandyCrushSodaSaga','king.com.BubbleWitch3Saga'"
set "APPS=!APPS!,'Microsoft.BingNews','Microsoft.BingWeather','Microsoft.BingFinance','Microsoft.BingSports'"
set "APPS=!APPS!,'*Netflix*','*Spotify*','*Facebook*','*Twitter*','*Disney*','*TikTok*','*Hulu*','*Amazon*','*Instagram*','*Plex*','*Dropbox*','*Duolingo*','*Wunderlist*','*Asphalt*','*Royal*'"
:: --- Xbox / gaming apps: removed in WORK mode ONLY (kept for GAMING & BASIC) ---
if /I "%MODE%"=="WORK" set "APPS=!APPS!,'Microsoft.XboxApp','Microsoft.XboxGameOverlay','Microsoft.XboxGamingOverlay','Microsoft.XboxSpeechToTextOverlay','Microsoft.Xbox.TCUI','Microsoft.GamingApp'"
if /I "%PROFILE%"=="LIGHT" goto :run_remove
:: MEDIUM: + Microsoft bloat
set "APPS=!APPS!,'Microsoft.3DBuilder','Microsoft.Microsoft3DViewer','Microsoft.Print3D','Microsoft.MixedReality.Portal'"
set "APPS=!APPS!,'Microsoft.SkypeApp','Microsoft.GetHelp','Microsoft.Getstarted','Microsoft.People','Microsoft.WindowsFeedbackHub','Microsoft.WindowsMaps','Microsoft.YourPhone'"
set "APPS=!APPS!,'Microsoft.ZuneMusic','Microsoft.ZuneVideo','Microsoft.MicrosoftOfficeHub'"
if /I "%PROFILE%"=="MEDIUM" goto :run_remove
:: STRONG: + extra
set "APPS=!APPS!,'Microsoft.WindowsAlarms','Microsoft.Todos','Clipchamp.Clipchamp','Microsoft.PowerAutomateDesktop','Microsoft.Messaging','Microsoft.OneConnect','Microsoft.Wallet','Microsoft.549981C3F5F10','Microsoft.BingSearch'"
:run_remove
powershell -NoProfile -ExecutionPolicy Bypass -Command "$apps=@(!APPS!); foreach($a in $apps){ Write-Host ('    - '+$a); Get-AppxPackage -AllUsers $a | Remove-AppxPackage -ErrorAction SilentlyContinue; Get-AppxProvisionedPackage -Online | Where-Object { $_.DisplayName -like $a } | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue }"
call :log "[Apps] Done."
exit /b

:: --------------------------------------------------- DISABLE SERVICES -------
:disable_services
echo.
call :log "[Services] Disabling useless background services..."
:: Common to both modes (Xbox services handled separately below).
for %%S in (DiagTrack dmwappushservice MapsBroker RetailDemo WMPNetworkSvc Fax RemoteRegistry WpcMonSvc lfsvc) do (
    echo     - %%S
    sc stop "%%S" >nul 2>&1
    sc config "%%S" start= disabled >nul 2>&1
)
:: Xbox services: disabled in WORK mode only (kept for GAMING).
if /I not "%MODE%"=="GAMING" (
    for %%S in (XblAuthManager XblGameSave XboxNetApiSvc XboxGipSvc) do (
        echo     - %%S
        sc stop "%%S" >nul 2>&1
        sc config "%%S" start= disabled >nul 2>&1
    )
)
call :log "[Services] Done."
exit /b

:: ------------------------------------------- DISABLE TELEMETRY / ADS --------
:disable_telemetry
echo.
call :log "[Telemetry] Disabling telemetry, ads and suggestions..."
reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection" /v AllowTelemetry /t REG_DWORD /d 0 /f >nul 2>&1
reg add "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection" /v AllowTelemetry /t REG_DWORD /d 0 /f >nul 2>&1
reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent" /v DisableWindowsConsumerFeatures /t REG_DWORD /d 1 /f >nul 2>&1
reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent" /v DisableSoftLanding /t REG_DWORD /d 1 /f >nul 2>&1
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" /v SilentInstalledAppsEnabled /t REG_DWORD /d 0 /f >nul 2>&1
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" /v SystemPaneSuggestionsEnabled /t REG_DWORD /d 0 /f >nul 2>&1
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" /v SubscribedContent-338388Enabled /t REG_DWORD /d 0 /f >nul 2>&1
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" /v SubscribedContent-338389Enabled /t REG_DWORD /d 0 /f >nul 2>&1
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" /v SubscribedContent-310093Enabled /t REG_DWORD /d 0 /f >nul 2>&1
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" /v PreInstalledAppsEnabled /t REG_DWORD /d 0 /f >nul 2>&1
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo" /v Enabled /t REG_DWORD /d 0 /f >nul 2>&1
reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo" /v DisabledByGroupPolicy /t REG_DWORD /d 1 /f >nul 2>&1
reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows\System" /v EnableActivityFeed /t REG_DWORD /d 0 /f >nul 2>&1
reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows\System" /v PublishUserActivities /t REG_DWORD /d 0 /f >nul 2>&1
call :log "[Telemetry] Done."
exit /b

:: --------------------------------------------------------- DISABLE CORTANA --
:disable_cortana
echo.
call :log "[Cortana] Disabling Cortana..."
reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search" /v AllowCortana /t REG_DWORD /d 0 /f >nul 2>&1
call :log "[Cortana] Done."
exit /b

:: ------------------------------------------------- DISABLE GAME DVR (WORK) --
:disable_gamedvr
echo.
call :log "[GameDVR] Disabling Game DVR / Game Bar (work mode)..."
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR" /v AppCaptureEnabled /t REG_DWORD /d 0 /f >nul 2>&1
reg add "HKCU\System\GameConfigStore" /v GameDVR_Enabled /t REG_DWORD /d 0 /f >nul 2>&1
reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows\GameDVR" /v AllowGameDVR /t REG_DWORD /d 0 /f >nul 2>&1
call :log "[GameDVR] Done."
exit /b

:: -------------------------------------------------- PERFORMANCE (WORK) ------
:perf_tweaks
echo.
call :log "[Perf] Applying light performance tweaks..."
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Serialize" /v StartupDelayInMSec /t REG_DWORD /d 0 /f >nul 2>&1
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced" /v HideFileExt /t REG_DWORD /d 0 /f >nul 2>&1
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" /v RotatingLockScreenEnabled /t REG_DWORD /d 0 /f >nul 2>&1
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" /v RotatingLockScreenOverlayEnabled /t REG_DWORD /d 0 /f >nul 2>&1
powercfg -setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c >nul 2>&1
call :log "[Perf] Done."
exit /b

:: -------------------------------------------------- GAMING TWEAKS -----------
:gaming_tweaks
echo.
call :log "[Gaming] Applying gaming tweaks (Game Mode, GPU sched, power, mouse, net)..."
:: Enable Game Mode and keep Game Bar / Game DVR working
reg add "HKCU\SOFTWARE\Microsoft\GameBar" /v AllowAutoGameMode /t REG_DWORD /d 1 /f >nul 2>&1
reg add "HKCU\SOFTWARE\Microsoft\GameBar" /v AutoGameModeEnabled /t REG_DWORD /d 1 /f >nul 2>&1
reg add "HKCU\System\GameConfigStore" /v GameDVR_Enabled /t REG_DWORD /d 1 /f >nul 2>&1
:: Hardware-accelerated GPU scheduling (HAGS) - needs supported GPU + reboot
reg add "HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers" /v HwSchMode /t REG_DWORD /d 2 /f >nul 2>&1
:: Disable mouse acceleration for precise aim
reg add "HKCU\Control Panel\Mouse" /v MouseSpeed /t REG_SZ /d 0 /f >nul 2>&1
reg add "HKCU\Control Panel\Mouse" /v MouseThreshold1 /t REG_SZ /d 0 /f >nul 2>&1
reg add "HKCU\Control Panel\Mouse" /v MouseThreshold2 /t REG_SZ /d 0 /f >nul 2>&1
:: Visual effects -> best performance (higher FPS)
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects" /v VisualFXSetting /t REG_DWORD /d 2 /f >nul 2>&1
:: Show file extensions (handy)
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced" /v HideFileExt /t REG_DWORD /d 0 /f >nul 2>&1
:: Network latency: disable Nagle's algorithm on all interfaces (lower ping)
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem 'HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces' | ForEach-Object { New-ItemProperty -Path $_.PSPath -Name TcpAckFrequency -Value 1 -PropertyType DWord -Force -ErrorAction SilentlyContinue | Out-Null; New-ItemProperty -Path $_.PSPath -Name TCPNoDelay -Value 1 -PropertyType DWord -Force -ErrorAction SilentlyContinue | Out-Null }"
:: Power plan: Ultimate Performance (fallback to High Performance)
powercfg -duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61 >nul 2>&1
powercfg -setactive e9a42b02-d5df-448d-aa00-03f14749eb61 >nul 2>&1
if errorlevel 1 powercfg -setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c >nul 2>&1
call :log "[Gaming] Done."
exit /b

:: ------------------------------------------------------ INSTALL APPS --------
:install_apps
echo.
call :log "[Install] Installing apps via winget (mode: %MODE%)..."
where winget >nul 2>&1
if errorlevel 1 (
    call :log "[Install] winget not found - skipping. Update 'App Installer' from Microsoft Store."
    exit /b
)
:: Shared apps for both modes. Edit to fit your needs.
set "APPLIST=Google.Chrome 7zip.7zip VideoLAN.VLC Notepad++.Notepad++"
if /I "%MODE%"=="GAMING" (
    set "APPLIST=!APPLIST! Valve.Steam Discord.Discord OBSProject.OBSStudio Guru3D.Afterburner"
) else (
    set "APPLIST=!APPLIST! Adobe.Acrobat.Reader.64-bit"
)
for %%P in (!APPLIST!) do (
    echo     - Installing %%P
    call :log "[Install] %%P"
    winget install -e --id %%P --silent --accept-package-agreements --accept-source-agreements >>"%LOG%" 2>&1
)
call :log "[Install] Done."
exit /b

:: ------------------------------------------------ EXTRA CLEANUP & PERF -------
:extra_cleanup
echo.
call :log "[Extra] Cleaning temp files..."
del /q /f /s "%TEMP%\*" >nul 2>&1
del /q /f /s "%SystemRoot%\Temp\*" >nul 2>&1
powershell -NoProfile -ExecutionPolicy Bypass -Command "Clear-RecycleBin -Force -ErrorAction SilentlyContinue" >nul 2>&1

call :log "[Extra] Disabling telemetry scheduled tasks..."
for %%T in (
    "\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser"
    "\Microsoft\Windows\Application Experience\ProgramDataUpdater"
    "\Microsoft\Windows\Customer Experience Improvement Program\Consolidator"
    "\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip"
    "\Microsoft\Windows\Feedback\Siuf\DmClient"
    "\Microsoft\Windows\Feedback\Siuf\DmClientOnScenarioDownload"
) do schtasks /Change /TN %%T /Disable >nul 2>&1

echo.
choice /C YN /N /M "Also remove OneDrive? [Y/N]: "
if errorlevel 2 goto :extra_done
call :log "[Extra] Removing OneDrive..."
taskkill /f /im OneDrive.exe >nul 2>&1
if exist "%SystemRoot%\System32\OneDriveSetup.exe" "%SystemRoot%\System32\OneDriveSetup.exe" /uninstall >nul 2>&1
if exist "%SystemRoot%\SysWOW64\OneDriveSetup.exe" "%SystemRoot%\SysWOW64\OneDriveSetup.exe" /uninstall >nul 2>&1
:extra_done
call :log "[Extra] Done."
exit /b

:: ----------------------------------------------------------- UNDO INFO ------
:undo_info
cls
echo ------------------------------------------------------------
echo   HOW TO UNDO
echo ------------------------------------------------------------
echo   1) Full rollback: System Restore -^> pick "Before win10-clean".
echo   2) Reverse settings/services: run  win10-clean-undo.bat
echo      (right click -^> Run as administrator).
echo   3) Reinstall a removed app: get it from Microsoft Store.
echo ------------------------------------------------------------
echo.
pause
goto :menu

:: ----------------------------------------------------- WSL & STARTUP --------
:wsl_startup
echo.
call :log "[WSL] Writing .wslconfig (8GB cap + auto memory reclaim)..."
(echo [wsl2]& echo memory=8GB& echo processors=4& echo swap=2GB& echo.& echo [experimental]& echo autoMemoryReclaim=gradual& echo sparseVhd=true) > "%USERPROFILE%\.wslconfig"
call :log "[WSL] .wslconfig saved to %USERPROFILE%"
echo.
choice /C YN /N /M "Disable heavy apps from startup (Docker, Discord, Steam, SteelSeries, GitHub)? [Y/N]: "
if errorlevel 2 goto :wsl_ask_shutdown
for %%A in (Docker Discord Steam SteelSeries GitHub) do (
    echo     - Disabling %%A from startup
    call :disablestartup "*%%A*"
)
:wsl_ask_shutdown
echo.
choice /C YN /N /M "Shut down WSL now to free its RAM (closes Docker)? [Y/N]: "
if errorlevel 2 goto :wsl_done
call :log "[WSL] Shutting down WSL..."
wsl --shutdown
:wsl_done
call :log "[WSL] Done. (Restart Docker manually if you stopped WSL.)"
exit /b

:: Disable a startup entry by name pattern. %~1 = pattern like *Discord*
:disablestartup
powershell -NoProfile -ExecutionPolicy Bypass -Command "$pat='%~1'; $run='HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run'; $appr='HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run'; if(!(Test-Path $appr)){New-Item $appr -Force | Out-Null}; if(Test-Path $run){ (Get-Item $run).Property | Where-Object { $_ -like $pat } | ForEach-Object { New-ItemProperty -Path $appr -Name $_ -Value ([byte[]](3,0,0,0,0,0,0,0,0,0,0,0)) -PropertyType Binary -Force | Out-Null } }"
exit /b

:: --------------------------------------------------------------- LOGGER -----
:log
echo %~1
>>"%LOG%" echo [%TS%] %~1
exit /b

:end
echo.
echo Bye.
endlocal
exit /b
