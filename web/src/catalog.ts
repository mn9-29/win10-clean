import type { CatalogItem } from './types'

// Rich mock catalog (~45 items) spread across categories.
// work / gaming / basic flags drive the presets.
export const MOCK_CATALOG: CatalogItem[] = [
  // ---- Apps / Games (debloat) ----
  { id: 'app-xbox', title: 'Remove Xbox app', desc: 'Uninstalls the Xbox console companion app and overlay.', category: 'apps', work: true, gaming: false, basic: true },
  { id: 'app-gamebar', title: 'Remove Xbox Game Bar', desc: 'Removes the Game Bar overlay (Win+G).', category: 'apps', work: true, gaming: false, basic: false },
  { id: 'app-candycrush', title: 'Remove Candy Crush Saga', desc: 'Uninstalls the pre-installed Candy Crush game.', category: 'apps', work: true, gaming: true, basic: true },
  { id: 'app-solitaire', title: 'Remove Microsoft Solitaire', desc: 'Uninstalls the bundled Solitaire Collection.', category: 'apps', work: true, gaming: true, basic: true },
  { id: 'app-tiktok', title: 'Remove TikTok stub', desc: 'Removes the promotional TikTok app stub.', category: 'apps', work: true, gaming: true, basic: true },
  { id: 'app-clipchamp', title: 'Remove Clipchamp', desc: 'Uninstalls the Clipchamp video editor.', category: 'apps', work: true, gaming: false, basic: false },
  { id: 'app-teamsconsumer', title: 'Remove Teams (consumer)', desc: 'Removes the personal Chat / Teams consumer app.', category: 'apps', work: true, gaming: true, basic: true },
  { id: 'app-3dviewer', title: 'Remove 3D Viewer & Mixed Reality', desc: 'Uninstalls 3D Viewer and Mixed Reality Portal.', category: 'apps', work: true, gaming: true, basic: true },
  { id: 'app-feedback', title: 'Remove Feedback Hub', desc: 'Uninstalls the Windows Feedback Hub app.', category: 'apps', work: true, gaming: true, basic: true },
  { id: 'app-getstarted', title: 'Remove Get Started / Tips', desc: 'Removes the Tips and Get Started promo apps.', category: 'apps', work: true, gaming: true, basic: true },

  // ---- Privacy / Ads ----
  { id: 'priv-telemetry', title: 'Disable telemetry', desc: 'Sets diagnostic data to the minimum and disables data collection.', category: 'privacy', work: true, gaming: true, basic: true },
  { id: 'priv-adid', title: 'Disable advertising ID', desc: 'Turns off the per-app advertising identifier.', category: 'privacy', work: true, gaming: true, basic: true },
  { id: 'priv-startads', title: 'Remove Start menu ads', desc: 'Disables suggested apps and promoted tiles in Start.', category: 'privacy', work: true, gaming: true, basic: true },
  { id: 'priv-lockscreenads', title: 'Disable lock-screen ads', desc: 'Turns off Spotlight tips and ads on the lock screen.', category: 'privacy', work: true, gaming: true, basic: true },
  { id: 'priv-cortana', title: 'Disable Cortana', desc: 'Disables the Cortana assistant and background tasks.', category: 'privacy', work: true, gaming: true, basic: false },
  { id: 'priv-activityhistory', title: 'Disable activity history', desc: 'Stops collection and upload of your timeline activity.', category: 'privacy', work: true, gaming: true, basic: true },
  { id: 'priv-location', title: 'Disable location tracking', desc: 'Turns off the system-wide location service.', category: 'privacy', work: true, gaming: false, basic: false },

  // ---- Services ----
  { id: 'svc-diagtrack', title: 'Disable DiagTrack service', desc: 'Stops and disables the Connected User Experiences and Telemetry service.', category: 'services', work: true, gaming: true, basic: true },
  { id: 'svc-dmwappush', title: 'Disable dmwappushservice', desc: 'Disables the WAP Push message routing telemetry service.', category: 'services', work: true, gaming: true, basic: true },
  { id: 'svc-sysmain', title: 'Disable SysMain (Superfetch)', desc: 'Reduces disk thrashing on SSD systems.', category: 'services', work: false, gaming: true, basic: false },
  { id: 'svc-fax', title: 'Disable Fax service', desc: 'Disables the rarely used Fax service.', category: 'services', work: true, gaming: true, basic: false },
  { id: 'svc-remoteregistry', title: 'Disable Remote Registry', desc: 'Hardens the box by disabling remote registry access.', category: 'services', work: true, gaming: true, basic: false },

  // ---- Gaming ----
  { id: 'game-gamemode', title: 'Enable Game Mode', desc: 'Prioritises CPU/GPU for the foreground game.', category: 'gaming', work: false, gaming: true, basic: false },
  { id: 'game-gpuschedule', title: 'Enable Hardware-accelerated GPU scheduling', desc: 'Lets the GPU manage its own VRAM for lower latency.', category: 'gaming', work: false, gaming: true, basic: false },
  { id: 'game-gamedvr', title: 'Disable Game DVR background recording', desc: 'Stops background capture to reclaim FPS.', category: 'gaming', work: true, gaming: true, basic: false },
  { id: 'game-fso', title: 'Disable Fullscreen Optimizations', desc: 'Forces true exclusive fullscreen for games.', category: 'gaming', work: false, gaming: true, basic: false },
  { id: 'game-mouseaccel', title: 'Disable mouse acceleration', desc: 'Turns off pointer precision for 1:1 aim.', category: 'gaming', work: false, gaming: true, basic: false },

  // ---- Performance ----
  { id: 'perf-highperf', title: 'High Performance power plan', desc: 'Activates the High Performance power scheme.', category: 'performance', work: false, gaming: true, basic: false },
  { id: 'perf-visualfx', title: 'Optimize visual effects for performance', desc: 'Disables non-essential animations and shadows.', category: 'performance', work: true, gaming: true, basic: false },
  { id: 'perf-startupboost', title: 'Trim startup programs', desc: 'Disables heavy non-essential startup entries.', category: 'performance', work: true, gaming: true, basic: true },
  { id: 'perf-hibernate', title: 'Disable hibernation', desc: 'Removes hiberfil.sys to free disk space.', category: 'performance', work: true, gaming: true, basic: false },
  { id: 'perf-pagefile', title: 'Optimize page file', desc: 'Sets a system-managed page file sized for your RAM.', category: 'performance', work: false, gaming: true, basic: false },

  // ---- Network ----
  { id: 'net-dnscloudflare', title: 'Set DNS to Cloudflare (1.1.1.1)', desc: 'Switches resolvers to Cloudflare for speed and privacy.', category: 'network', work: true, gaming: true, basic: true },
  { id: 'net-flushdns', title: 'Flush DNS cache', desc: 'Clears the resolver cache to fix stale lookups.', category: 'network', work: true, gaming: true, basic: true },
  { id: 'net-nagle', title: 'Disable Nagle algorithm', desc: 'Lowers latency for twitch online gaming.', category: 'network', work: false, gaming: true, basic: false },
  { id: 'net-resetwinsock', title: 'Reset Winsock catalog', desc: 'Repairs broken network sockets and proxies.', category: 'network', work: true, gaming: true, basic: false },

  // ---- Updates ----
  { id: 'upd-pausefeature', title: 'Defer feature updates', desc: 'Holds back major feature updates for stability.', category: 'updates', work: true, gaming: false, basic: false },
  { id: 'upd-p2p', title: 'Disable update delivery over P2P', desc: 'Stops sharing updates with other PCs on the internet.', category: 'updates', work: true, gaming: true, basic: true },
  { id: 'upd-activehours', title: 'Set active hours 8am–11pm', desc: 'Prevents reboots during your working day.', category: 'updates', work: true, gaming: true, basic: true },

  // ---- Win 11 / UI ----
  { id: 'ui-taskbarleft', title: 'Align taskbar to the left', desc: 'Moves the Windows 11 taskbar icons to the left.', category: 'ui', work: true, gaming: true, basic: true },
  { id: 'ui-classiccontext', title: 'Restore classic right-click menu', desc: 'Brings back the full Windows 10 context menu.', category: 'ui', work: true, gaming: true, basic: true },
  { id: 'ui-widgets', title: 'Disable Widgets board', desc: 'Removes the Widgets button and feed.', category: 'ui', work: true, gaming: true, basic: true },
  { id: 'ui-chat', title: 'Hide taskbar Chat icon', desc: 'Hides the Teams Chat button from the taskbar.', category: 'ui', work: true, gaming: true, basic: true },
  { id: 'ui-darkmode', title: 'Enable system dark mode', desc: 'Sets apps and system to the dark theme.', category: 'ui', work: true, gaming: true, basic: true },
  { id: 'ui-explorer-thispc', title: 'Open Explorer to This PC', desc: 'Sets File Explorer to launch at This PC, not Quick Access.', category: 'ui', work: true, gaming: true, basic: true },

  // ---- Maintenance ----
  { id: 'maint-sfc', title: 'Run SFC system file check', desc: 'Scans and repairs protected system files.', category: 'maintenance', work: true, gaming: true, basic: true },
  { id: 'maint-dism', title: 'Run DISM health restore', desc: 'Repairs the Windows component store image.', category: 'maintenance', work: true, gaming: true, basic: false },
  { id: 'maint-restorepoint', title: 'Create restore point', desc: 'Snapshots the system before applying changes.', category: 'maintenance', work: true, gaming: true, basic: true },
  { id: 'maint-defrag', title: 'Optimize / TRIM drives', desc: 'Runs TRIM on SSDs and defrag on HDDs.', category: 'maintenance', work: true, gaming: true, basic: false },

  // ---- Cleanup ----
  { id: 'clean-temp', title: 'Clean temp files', desc: 'Empties %TEMP% and Windows temp folders.', category: 'cleanup', work: true, gaming: true, basic: true },
  { id: 'clean-recyclebin', title: 'Empty Recycle Bin', desc: 'Permanently clears the Recycle Bin.', category: 'cleanup', work: true, gaming: true, basic: true },
  { id: 'clean-prefetch', title: 'Clear Prefetch cache', desc: 'Removes stale prefetch trace files.', category: 'cleanup', work: false, gaming: true, basic: false },
  { id: 'clean-winupdate', title: 'Clean Windows Update cache', desc: 'Frees space used by old downloaded updates.', category: 'cleanup', work: true, gaming: true, basic: true },
  { id: 'clean-thumbnails', title: 'Clear thumbnail cache', desc: 'Rebuilds the Explorer thumbnail cache.', category: 'cleanup', work: true, gaming: true, basic: true },

  // ---- WSL / Startup (system) ----
  { id: 'sys-wsl2', title: 'Optimize WSL2 memory', desc: 'Caps WSL2 RAM/CPU via a tuned .wslconfig file.', category: 'system', work: true, gaming: false, basic: false },
  { id: 'sys-wsl-update', title: 'Update WSL kernel', desc: 'Pulls the latest WSL2 Linux kernel.', category: 'system', work: true, gaming: false, basic: false },
  { id: 'sys-fastboot', title: 'Disable Fast Startup', desc: 'Ensures a clean full shutdown each time.', category: 'system', work: true, gaming: true, basic: false },

  // ---- Install (winget) ----
  { id: 'inst-chrome', title: 'Install Google Chrome', desc: 'winget install Google.Chrome', category: 'install', work: true, gaming: true, basic: true },
  { id: 'inst-vscode', title: 'Install Visual Studio Code', desc: 'winget install Microsoft.VisualStudioCode', category: 'install', work: true, gaming: false, basic: false },
  { id: 'inst-7zip', title: 'Install 7-Zip', desc: 'winget install 7zip.7zip', category: 'install', work: true, gaming: true, basic: true },
  { id: 'inst-vlc', title: 'Install VLC media player', desc: 'winget install VideoLAN.VLC', category: 'install', work: true, gaming: true, basic: true },
  { id: 'inst-steam', title: 'Install Steam', desc: 'winget install Valve.Steam', category: 'install', work: false, gaming: true, basic: false },
  { id: 'inst-discord', title: 'Install Discord', desc: 'winget install Discord.Discord', category: 'install', work: false, gaming: true, basic: false },
  { id: 'inst-powertoys', title: 'Install PowerToys', desc: 'winget install Microsoft.PowerToys', category: 'install', work: true, gaming: false, basic: false },
]
