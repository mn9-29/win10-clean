import type {
  ApplyRequest,
  BridgeResponse,
  CatalogItem,
  DashboardData,
  DeviceMode,
  LogEvent,
  ProgressEvent,
  PushEvent,
  StartupEntry,
  ResourcesDto,
  ProcInfo,
  DiskInfo,
} from './types'
import { MOCK_CATALOG } from './catalog'

// -------------------------------------------------------------------------
// WinForge host bridge.
// REAL mode: when window.chrome.webview is present (running inside WebView2),
//   requests go out via postMessage(JSON) and responses/pushes arrive via
//   the 'message' event.
// MOCK mode: otherwise everything resolves with realistic mock data and
//   apply/runMode/scan stream simulated log + progress events on timers.
// -------------------------------------------------------------------------

type LogCb = (e: LogEvent) => void
type ProgressCb = (e: ProgressEvent) => void
type DashboardCb = (d: DashboardData) => void

interface WebViewLike {
  postMessage: (msg: string) => void
  addEventListener: (type: 'message', cb: (e: { data: unknown }) => void) => void
}

function getWebView(): WebViewLike | null {
  const w = window as unknown as { chrome?: { webview?: WebViewLike } }
  return w.chrome?.webview ?? null
}

export const IS_REAL = getWebView() !== null

function uid(): string {
  return Math.random().toString(36).slice(2) + Date.now().toString(36)
}

const delay = (ms: number) => new Promise<void>((r) => setTimeout(r, ms))

// ---- Mock dashboard generation -----------------------------------------
function makeDashboard(): DashboardData {
  const memTotalGB = 32
  const memUsedGB = +(11 + Math.random() * 6).toFixed(1)
  return {
    os: 'Windows 11 Pro 23H2',
    osBuild: '22631.4317',
    computer: 'FORGE-DESK',
    user: 'iiris',
    cpu: 'AMD Ryzen 7 5800X',
    cpuLoad: Math.round(8 + Math.random() * 40),
    cpuCores: 16,
    memUsedGB,
    memTotalGB,
    gpu: 'NVIDIA GeForce RTX 3070',
    diskLabel: 'Windows-SSD',
    diskUsedGB: 412,
    diskTotalGB: 931,
    uptime: '2d 7h 41m',
    ramPillGB: memUsedGB,
    status: [
      { label: 'Administrator rights', ok: true },
      { label: 'System Restore enabled', ok: true },
      { label: 'Registry backup found', ok: false },
      { label: 'WSL2 installed', ok: true },
      { label: 'winget available', ok: true },
      { label: 'Pending reboot', ok: false },
    ],
  }
}

// ---- Mock startup entries ----------------------------------------------
const MOCK_STARTUP: StartupEntry[] = [
  { id: 'su-discord', name: 'Discord', command: 'C:\\Users\\iiris\\AppData\\Local\\Discord\\Update.exe --processStart Discord.exe', location: 'HKCU Run', scope: 'user', enabled: true },
  { id: 'su-steam', name: 'Steam', command: '"C:\\Program Files (x86)\\Steam\\steam.exe" -silent', location: 'HKCU Run', scope: 'user', enabled: true },
  { id: 'su-spotify', name: 'Spotify', command: 'C:\\Users\\iiris\\AppData\\Roaming\\Spotify\\Spotify.exe --autostart --minimized', location: 'Startup folder', scope: 'user', enabled: false },
  { id: 'su-nvidia', name: 'NVIDIA app', command: '"C:\\Program Files\\NVIDIA Corporation\\NVIDIA app\\CEF\\NVIDIA app.exe" --start-minimized', location: 'HKLM Run', scope: 'machine', enabled: true },
  { id: 'su-onedrive', name: 'OneDrive', command: 'C:\\Users\\iiris\\AppData\\Local\\Microsoft\\OneDrive\\OneDrive.exe /background', location: 'HKCU Run', scope: 'user', enabled: true },
  { id: 'su-docker', name: 'Docker Desktop', command: '"C:\\Program Files\\Docker\\Docker\\Docker Desktop.exe" -Autostart', location: 'HKLM Run', scope: 'machine', enabled: false },
  { id: 'su-epic', name: 'Epic Games Launcher', command: '"C:\\Program Files (x86)\\Epic Games\\Launcher\\Portal\\Binaries\\Win64\\EpicGamesLauncher.exe" -silent', location: 'HKLM Run (32-bit)', scope: 'machine', enabled: false },
  { id: 'su-razer', name: 'Razer Synapse', command: '"C:\\Program Files (x86)\\Razer\\Synapse3\\WPFUI\\Framework\\Razer Synapse 3 Host\\Razer Synapse 3.exe"', location: 'HKLM Run (32-bit)', scope: 'machine', enabled: true },
  { id: 'su-realtek', name: 'Realtek HD Audio Manager', command: 'C:\\Program Files\\Realtek\\Audio\\HDA\\RtkNGUI64.exe -s', location: 'HKLM Run', scope: 'machine', enabled: true },
  { id: 'su-teams', name: 'Microsoft Teams', command: '"C:\\Users\\iiris\\AppData\\Local\\Microsoft\\Teams\\current\\Teams.exe" --process-start-args "--system-initiated"', location: 'Startup folder', scope: 'user', enabled: false },
  { id: 'su-logitech', name: 'Logitech G HUB', command: '"C:\\Program Files\\LGHUB\\lghub.exe" --background', location: 'HKCU Run', scope: 'user', enabled: true },
  { id: 'su-epicgames', name: 'EpicGames Helper', command: '"C:\\Program Files (x86)\\Epic Games\\Launcher\\Engine\\Binaries\\Win64\\EpicWebHelper.exe"', location: 'Startup folder', scope: 'user', enabled: false },
]

// ---- Mock resources generation -----------------------------------------
const rnd = (min: number, max: number) => +(min + Math.random() * (max - min)).toFixed(1)

function makeResources(): ResourcesDto {
  const cores = 16
  const cpuPerCore = Array.from({ length: cores }, () => rnd(5, 60))
  const cpuTotal = +(cpuPerCore.reduce((a, b) => a + b, 0) / cores).toFixed(1)
  const memTotalMB = 32000
  const memUsedMB = Math.round(20000 + Math.random() * 4000)
  const memAvailMB = memTotalMB - memUsedMB
  const memPercent = Math.round((memUsedMB / memTotalMB) * 100)
  const disks: DiskInfo[] = [
    { name: 'C:', totalGB: 931, freeGB: +(519 - Math.random() * 4).toFixed(1), usedGB: +(412 + Math.random() * 4).toFixed(1) },
    { name: 'D:', totalGB: 1863, freeGB: +(1240 - Math.random() * 6).toFixed(1), usedGB: +(623 + Math.random() * 6).toFixed(1) },
  ]
  const procNames = ['chrome.exe', 'Code.exe', 'Discord.exe', 'Spotify.exe', 'Teams.exe', 'explorer.exe', 'steam.exe', 'Docker Desktop.exe', 'pwsh.exe', 'dwm.exe']
  const topByMem: ProcInfo[] = procNames
    .map((name, i) => ({ name, pid: 1000 + i * 137, memMB: Math.round(rnd(180, 1800)), cpu: rnd(0, 14) }))
    .sort((a, b) => b.memMB - a.memMB)
    .slice(0, 8)
  const topByCpu: ProcInfo[] = procNames
    .map((name, i) => ({ name, pid: 1000 + i * 137, memMB: Math.round(rnd(180, 1800)), cpu: rnd(0.5, 38) }))
    .sort((a, b) => b.cpu - a.cpu)
    .slice(0, 8)
  return {
    cpuTotal, cpuPerCore, cpuCores: cores,
    memTotalMB, memUsedMB, memAvailMB, memPercent,
    uptime: '2d 7h 41m', disks, topByMem, topByCpu,
  }
}

class Bridge {
  private logCbs = new Set<LogCb>()
  private progressCbs = new Set<ProgressCb>()
  private dashboardCbs = new Set<DashboardCb>()
  private pending = new Map<string, (r: BridgeResponse) => void>()
  private webview = getWebView()
  private mockStartup: StartupEntry[] = MOCK_STARTUP.map((e) => ({ ...e }))

  constructor() {
    if (this.webview) {
      this.webview.addEventListener('message', (e) => this.onHostMessage(e.data))
    }
  }

  // ---- event subscriptions ----------------------------------------------
  onLog(cb: LogCb) {
    this.logCbs.add(cb)
    return () => this.logCbs.delete(cb)
  }
  onProgress(cb: ProgressCb) {
    this.progressCbs.add(cb)
    return () => this.progressCbs.delete(cb)
  }
  onDashboard(cb: DashboardCb) {
    this.dashboardCbs.add(cb)
    return () => this.dashboardCbs.delete(cb)
  }

  private emitLog(line: string, level: LogEvent['level'] = 'info') {
    this.logCbs.forEach((cb) => cb({ event: 'log', line, level }))
  }
  private emitProgress(value: number, status: string) {
    this.progressCbs.forEach((cb) => cb({ event: 'progress', value, status }))
  }
  private emitDashboard(d: DashboardData) {
    this.dashboardCbs.forEach((cb) => cb(d))
  }

  // ---- REAL host message handling ---------------------------------------
  private onHostMessage(raw: unknown) {
    let msg: unknown
    try {
      msg = typeof raw === 'string' ? JSON.parse(raw) : raw
    } catch {
      return
    }
    const m = msg as Partial<PushEvent> & Partial<BridgeResponse>
    if (m && typeof m === 'object' && 'event' in m && m.event) {
      const ev = m as unknown as PushEvent
      if (ev.event === 'log') this.logCbs.forEach((cb) => cb(ev))
      else if (ev.event === 'progress') this.progressCbs.forEach((cb) => cb(ev))
      else if (ev.event === 'dashboard') this.dashboardCbs.forEach((cb) => cb(ev.data))
      return
    }
    if (m && typeof m === 'object' && 'id' in m && typeof m.id === 'string') {
      const resolve = this.pending.get(m.id)
      if (resolve) {
        this.pending.delete(m.id)
        resolve(m as BridgeResponse)
      }
    }
  }

  private request<T>(type: string, payload?: unknown): Promise<T> {
    const id = uid()
    return new Promise<T>((resolve, reject) => {
      this.pending.set(id, (r) => {
        if (r.ok) resolve(r.data as T)
        else reject(new Error(r.error ?? 'Bridge error'))
      })
      this.webview!.postMessage(JSON.stringify({ id, type, payload }))
      // Safety timeout
      setTimeout(() => {
        if (this.pending.has(id)) {
          this.pending.delete(id)
          reject(new Error(`Bridge timeout for ${type}`))
        }
      }, 30000)
    })
  }

  // ---- API methods -------------------------------------------------------
  async getCatalog(): Promise<CatalogItem[]> {
    if (this.webview) return this.request<CatalogItem[]>('getCatalog')
    await delay(120)
    return MOCK_CATALOG
  }

  async getDashboard(): Promise<DashboardData> {
    if (this.webview) return this.request<DashboardData>('getDashboard')
    await delay(120)
    return makeDashboard()
  }

  async apply(req: ApplyRequest): Promise<{ applied: number }> {
    if (this.webview) return this.request('apply', req)
    return this.mockStream(
      [
        ...(req.restorePoint ? ['Creating system restore point…'] : []),
        ...(req.backupReg ? ['Backing up registry hives…'] : []),
        `Applying ${req.ids.length} selected tweak(s)…`,
        ...req.ids.map((id) => {
          const item = MOCK_CATALOG.find((c) => c.id === id)
          return `Applying: ${item?.title ?? id}`
        }),
        'Refreshing Explorer shell…',
        'All selected tweaks applied successfully.',
      ],
      { applied: req.ids.length },
    )
  }

  async runMode(mode: DeviceMode): Promise<{ mode: DeviceMode }> {
    if (this.webview) return this.request('runMode', { mode })
    const label = mode.charAt(0).toUpperCase() + mode.slice(1)
    return this.mockStream(
      [
        `Activating ${label} mode…`,
        'Creating restore point…',
        'Configuring power plan…',
        'Tuning services and registry…',
        `Optimizing for ${label} workloads…`,
        `${label} mode is now active.`,
      ],
      { mode },
    )
  }

  async scan(): Promise<{ found: number; apps: string[] }> {
    if (this.webview) return this.request('scan')
    const apps = [
      'Google Chrome', 'Visual Studio Code', '7-Zip', 'VLC media player',
      'Steam', 'Discord', 'NVIDIA App', 'Spotify', 'Notion', 'Docker Desktop',
    ]
    return this.mockStream(
      ['Scanning installed applications…', ...apps.map((a) => `Found: ${a}`), `Scan complete: ${apps.length} apps.`],
      { found: apps.length, apps },
    )
  }

  async backupRegistry(): Promise<{ path: string }> {
    if (this.webview) return this.request('backupRegistry')
    return this.mockStream(['Exporting registry hives…', 'Registry backup saved.'], {
      path: 'C:\\WinForge\\backups\\reg_20260621.reg',
    })
  }

  async restoreRegistry(): Promise<{ ok: true }> {
    if (this.webview) return this.request('restoreRegistry')
    return this.mockStream(['Restoring registry from backup…', 'Registry restored.'], { ok: true as const })
  }

  async revert(): Promise<{ reverted: number }> {
    if (this.webview) return this.request('revert')
    return this.mockStream(
      ['Reverting last applied changes…', 'Restoring services…', 'Re-enabling removed components…', 'Revert complete.'],
      { reverted: 7 },
    )
  }

  async checkUpdates(): Promise<{ current: string; latest: string; upToDate: boolean }> {
    if (this.webview) return this.request('checkUpdates')
    await delay(400)
    return { current: 'v2.11.0', latest: 'v2.11.0', upToDate: true }
  }

  async getStartup(): Promise<StartupEntry[]> {
    if (this.webview) return this.request<StartupEntry[]>('getStartup')
    await delay(140)
    return this.mockStartup.map((e) => ({ ...e }))
  }

  async setStartup(id: string, enabled: boolean): Promise<{ id: string; enabled: boolean }> {
    if (this.webview) return this.request('setStartup', { id, enabled })
    await delay(150)
    const entry = this.mockStartup.find((e) => e.id === id)
    if (entry) entry.enabled = enabled
    return { id, enabled }
  }

  async getResources(): Promise<ResourcesDto> {
    if (this.webview) return this.request<ResourcesDto>('getResources')
    await delay(120)
    return makeResources()
  }

  // ---- mock streaming helper --------------------------------------------
  private async mockStream<T>(lines: string[], result: T): Promise<T> {
    const total = lines.length
    this.emitProgress(0, 'Starting…')
    for (let i = 0; i < total; i++) {
      await delay(360 + Math.random() * 260)
      const last = i === total - 1
      this.emitLog(lines[i], last ? 'ok' : 'info')
      this.emitProgress(Math.round(((i + 1) / total) * 100), last ? 'Done' : lines[i])
    }
    // refresh dashboard after an operation
    this.emitDashboard(makeDashboard())
    return result
  }
}

export const bridge = new Bridge()
