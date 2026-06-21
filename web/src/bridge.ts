import type {
  ApplyRequest,
  BridgeResponse,
  CatalogItem,
  DashboardData,
  DeviceMode,
  LogEvent,
  ProgressEvent,
  PushEvent,
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

class Bridge {
  private logCbs = new Set<LogCb>()
  private progressCbs = new Set<ProgressCb>()
  private dashboardCbs = new Set<DashboardCb>()
  private pending = new Map<string, (r: BridgeResponse) => void>()
  private webview = getWebView()

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
