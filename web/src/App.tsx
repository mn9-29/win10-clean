import { useEffect, useMemo, useRef, useState } from 'react'
import {
  LayoutDashboard, MonitorCog, Boxes, ShieldOff, Cog, Gamepad2, Gauge,
  Network, RefreshCw, AppWindow, Wrench, Trash2, TerminalSquare, Download,
  ScanLine, Sun, Moon, Languages, Info, CheckCircle2, XCircle, Cpu,
  HardDrive, MemoryStick, Clock, Monitor, Server, Play, Undo2, Container,
  RotateCcw, X, ChevronUp, Search, Briefcase, Code2,
} from 'lucide-react'
import { bridge, IS_REAL } from './bridge'
import { type Lang, t } from './i18n'
import type { CatalogItem, CategoryKey, DashboardData, DeviceMode } from './types'
import { Bar, Card, Checkbox, Chip, IconButton } from './components/ui'

const VERSION = 'v2.11.0'

type TabKey = 'dashboard' | 'modes' | 'installed' | CategoryKey

interface TabDef {
  key: TabKey
  labelKey: Parameters<typeof t>[1]
  icon: typeof LayoutDashboard
}

const TABS: TabDef[] = [
  { key: 'dashboard', labelKey: 'dashboard', icon: LayoutDashboard },
  { key: 'modes', labelKey: 'deviceModes', icon: MonitorCog },
  { key: 'apps', labelKey: 'apps', icon: Boxes },
  { key: 'privacy', labelKey: 'privacy', icon: ShieldOff },
  { key: 'services', labelKey: 'services', icon: Cog },
  { key: 'gaming', labelKey: 'gaming', icon: Gamepad2 },
  { key: 'performance', labelKey: 'performance', icon: Gauge },
  { key: 'network', labelKey: 'network', icon: Network },
  { key: 'updates', labelKey: 'updates', icon: RefreshCw },
  { key: 'ui', labelKey: 'ui', icon: AppWindow },
  { key: 'maintenance', labelKey: 'maintenance', icon: Wrench },
  { key: 'cleanup', labelKey: 'cleanup', icon: Trash2 },
  { key: 'system', labelKey: 'system', icon: TerminalSquare },
  { key: 'install', labelKey: 'install', icon: Download },
  { key: 'installed', labelKey: 'installed', icon: ScanLine },
]

type Preset = 'work' | 'gaming' | 'basic' | 'clear'

interface LogLine {
  id: number
  text: string
  level: 'info' | 'ok' | 'warn' | 'err'
}

function ls<T>(key: string, fallback: T): T {
  try {
    const v = localStorage.getItem(key)
    return v === null ? fallback : (JSON.parse(v) as T)
  } catch {
    return fallback
  }
}

export default function App() {
  // ---- persisted prefs ----
  const [dark, setDark] = useState<boolean>(() => ls('wf.dark', true))
  const [lang, setLang] = useState<Lang>(() => ls<Lang>('wf.lang', 'en'))

  // ---- data ----
  const [catalog, setCatalog] = useState<CatalogItem[]>([])
  const [dashboard, setDashboard] = useState<DashboardData | null>(null)
  const [selected, setSelected] = useState<Set<string>>(new Set())

  // ---- ui state ----
  const [tab, setTab] = useState<TabKey>('dashboard')
  const [query, setQuery] = useState('')
  const [restorePoint, setRestorePoint] = useState(true)
  const [backupReg, setBackupReg] = useState(true)
  const [busy, setBusy] = useState(false)
  const [progress, setProgress] = useState(0)
  const [status, setStatus] = useState<string>('')
  const [logOpen, setLogOpen] = useState(false)
  const [logs, setLogs] = useState<LogLine[]>([])
  const [aboutOpen, setAboutOpen] = useState(false)
  const logIdRef = useRef(0)
  const logEndRef = useRef<HTMLDivElement>(null)

  const tr = (k: Parameters<typeof t>[1]) => t(lang, k)

  // ---- apply prefs to <html> + persist ----
  useEffect(() => {
    const html = document.documentElement
    html.classList.toggle('dark', dark)
    html.style.colorScheme = dark ? 'dark' : 'light'
    localStorage.setItem('wf.dark', JSON.stringify(dark))
  }, [dark])

  useEffect(() => {
    const html = document.documentElement
    html.lang = lang
    html.dir = lang === 'ar' ? 'rtl' : 'ltr'
    localStorage.setItem('wf.lang', JSON.stringify(lang))
  }, [lang])

  // ---- load data + subscribe to events ----
  useEffect(() => {
    bridge.getCatalog().then(setCatalog)
    bridge.getDashboard().then(setDashboard)

    const offLog = bridge.onLog((e) => {
      logIdRef.current += 1
      setLogs((prev) => [...prev.slice(-400), { id: logIdRef.current, text: e.line, level: e.level ?? 'info' }])
    })
    const offProg = bridge.onProgress((e) => {
      setProgress(e.value)
      setStatus(e.status)
    })
    const offDash = bridge.onDashboard((d) => setDashboard(d))
    return () => {
      offLog()
      offProg()
      offDash()
    }
  }, [])

  // ---- animate dashboard cpu/ram slightly in mock mode ----
  useEffect(() => {
    if (IS_REAL) return
    const iv = setInterval(() => {
      setDashboard((d) =>
        d
          ? {
              ...d,
              cpuLoad: Math.max(4, Math.min(96, d.cpuLoad + Math.round((Math.random() - 0.5) * 12))),
              memUsedGB: Math.max(8, Math.min(d.memTotalGB - 1, +(d.memUsedGB + (Math.random() - 0.5) * 0.8).toFixed(1))),
              ramPillGB: d.memUsedGB,
            }
          : d,
      )
    }, 2200)
    return () => clearInterval(iv)
  }, [])

  useEffect(() => {
    if (logOpen) logEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [logs, logOpen])

  // ---- derived ----
  const tabItems = useMemo(() => {
    if (tab === 'dashboard' || tab === 'modes' || tab === 'installed') return []
    return catalog.filter((c) => c.category === tab)
  }, [catalog, tab])

  const visibleItems = useMemo(() => {
    const q = query.trim().toLowerCase()
    if (!q) return tabItems
    return tabItems.filter((c) => c.title.toLowerCase().includes(q) || c.desc.toLowerCase().includes(q))
  }, [tabItems, query])

  // ---- actions ----
  const toggle = (id: string) =>
    setSelected((prev) => {
      const n = new Set(prev)
      n.has(id) ? n.delete(id) : n.add(id)
      return n
    })

  const selectAllVisible = () =>
    setSelected((prev) => {
      const n = new Set(prev)
      visibleItems.forEach((i) => n.add(i.id))
      return n
    })

  const selectNoneVisible = () =>
    setSelected((prev) => {
      const n = new Set(prev)
      visibleItems.forEach((i) => n.delete(i.id))
      return n
    })

  const applyPreset = (preset: Preset) => {
    if (preset === 'clear') {
      setSelected(new Set())
      return
    }
    const flag = preset // 'work' | 'gaming' | 'basic'
    setSelected(new Set(catalog.filter((c) => c[flag]).map((c) => c.id)))
  }

  const runAsync = async (fn: () => Promise<unknown>) => {
    if (busy) return
    setBusy(true)
    setLogOpen(true)
    setProgress(0)
    try {
      await fn()
    } catch (e) {
      logIdRef.current += 1
      setLogs((prev) => [...prev, { id: logIdRef.current, text: String(e), level: 'err' }])
    } finally {
      setBusy(false)
    }
  }

  const onApply = () => runAsync(() => bridge.apply({ ids: [...selected], restorePoint, backupReg }))
  const onMode = (m: DeviceMode) => runAsync(() => bridge.runMode(m))
  const onScan = () => runAsync(() => bridge.scan())
  const onRevert = () => runAsync(() => bridge.revert())
  const onDocker = () =>
    runAsync(async () => {
      logIdRef.current += 1
      setLogs((p) => [...p, { id: logIdRef.current, text: 'docker system prune -af …', level: 'info' }])
      await bridge.restoreRegistry().catch(() => undefined)
    })
  const onRestartExplorer = () =>
    runAsync(async () => {
      logIdRef.current += 1
      setLogs((p) => [...p, { id: logIdRef.current, text: 'Restarting explorer.exe …', level: 'info' }])
      await new Promise((r) => setTimeout(r, 700))
      logIdRef.current += 1
      setLogs((p) => [...p, { id: logIdRef.current, text: 'Explorer restarted.', level: 'ok' }])
    })

  const showToolbarSelectors = !['dashboard', 'modes', 'installed'].includes(tab)

  return (
    <div className="flex h-screen flex-col overflow-hidden bg-ink-bg text-ink-text dark:bg-ink-bg dark:text-ink-text [html:not(.dark)_&]:bg-[#f4f6f9] [html:not(.dark)_&]:text-[#1b1f27]">
      <Header dark={dark} lang={lang} ram={dashboard?.ramPillGB ?? 0} total={dashboard?.memTotalGB ?? 32} />

      <Toolbar
        tr={tr}
        query={query}
        setQuery={setQuery}
        applyPreset={applyPreset}
        restorePoint={restorePoint}
        setRestorePoint={setRestorePoint}
        backupReg={backupReg}
        setBackupReg={setBackupReg}
        showSelectors={showToolbarSelectors}
        onAll={selectAllVisible}
        onNone={selectNoneVisible}
        dark={dark}
        toggleDark={() => setDark((d) => !d)}
        lang={lang}
        toggleLang={() => setLang((l) => (l === 'en' ? 'ar' : 'en'))}
        onAbout={() => setAboutOpen(true)}
      />

      <div className="flex min-h-0 flex-1">
        <Sidebar tabs={TABS} tab={tab} setTab={setTab} tr={tr} selected={selected} catalog={catalog} />

        <main className="wf-scroll min-h-0 flex-1 overflow-y-auto p-5">
          {tab === 'dashboard' && <Dashboard data={dashboard} tr={tr} />}
          {tab === 'modes' && <DeviceModes tr={tr} onMode={onMode} busy={busy} />}
          {tab === 'installed' && <Installed tr={tr} onScan={onScan} busy={busy} logs={logs} />}
          {showToolbarSelectors && (
            <TweakList items={visibleItems} selected={selected} toggle={toggle} tr={tr} />
          )}
        </main>
      </div>

      <LogConsole open={logOpen} setOpen={setLogOpen} logs={logs} clear={() => setLogs([])} tr={tr} />

      <BottomBar
        tr={tr}
        progress={progress}
        status={status || (busy ? '…' : tr('ready'))}
        selectedCount={selected.size}
        busy={busy}
        onApply={onApply}
        onRevert={onRevert}
        onDocker={onDocker}
        onRestartExplorer={onRestartExplorer}
      />

      {aboutOpen && <AboutModal tr={tr} close={() => setAboutOpen(false)} />}
    </div>
  )
}

// ===================================================================== HEADER
function Header({ dark, lang, ram, total }: { dark: boolean; lang: Lang; ram: number; total: number }) {
  void dark
  void lang
  const pct = total ? Math.round((ram / total) * 100) : 0
  return (
    <header className="flex items-center justify-between border-b border-ink-border bg-ink-panel/60 px-5 py-3 backdrop-blur">
      <div className="flex items-baseline gap-2">
        <span className="wf-gradient-text text-2xl font-bold tracking-tight">WinForge</span>
        <span className="rounded-md border border-ink-border px-1.5 py-0.5 text-[11px] font-medium text-ink-dim">
          {VERSION}
        </span>
        {IS_REAL ? (
          <span className="text-[10px] uppercase tracking-wider text-accent">host</span>
        ) : (
          <span className="text-[10px] uppercase tracking-wider text-ink-dim">mock</span>
        )}
      </div>
      <div className="flex items-center gap-2 rounded-full border border-ink-border bg-black/20 px-3 py-1.5">
        <span className="h-2 w-2 animate-pulseSoft rounded-full wf-gradient" />
        <MemoryStick size={14} className="text-accent" />
        <span className="text-xs font-medium tabular-nums text-ink-text">
          {ram.toFixed(1)} / {total} GB
        </span>
        <span className="text-[11px] tabular-nums text-ink-dim">{pct}%</span>
      </div>
    </header>
  )
}

// ==================================================================== TOOLBAR
function Toolbar(props: {
  tr: (k: Parameters<typeof t>[1]) => string
  query: string
  setQuery: (v: string) => void
  applyPreset: (p: Preset) => void
  restorePoint: boolean
  setRestorePoint: (v: boolean) => void
  backupReg: boolean
  setBackupReg: (v: boolean) => void
  showSelectors: boolean
  onAll: () => void
  onNone: () => void
  dark: boolean
  toggleDark: () => void
  lang: Lang
  toggleLang: () => void
  onAbout: () => void
}) {
  const { tr } = props
  const presetBtn =
    'rounded-lg border border-ink-border px-3 py-1.5 text-xs font-medium text-ink-text transition-all hover:border-accent/50 hover:text-accent'
  return (
    <div className="flex flex-wrap items-center gap-2 border-b border-ink-border bg-ink-panel/40 px-5 py-2.5">
      <span className="text-[11px] font-semibold uppercase tracking-wider text-ink-dim">{tr('presets')}</span>
      <button className={presetBtn} onClick={() => props.applyPreset('work')}>{tr('presetOffice')}</button>
      <button className={presetBtn} onClick={() => props.applyPreset('gaming')}>{tr('presetGaming')}</button>
      <button className={presetBtn} onClick={() => props.applyPreset('basic')}>{tr('presetBasic')}</button>
      <button className={presetBtn} onClick={() => props.applyPreset('clear')}>{tr('presetClear')}</button>

      <label className="ml-1 flex cursor-pointer select-none items-center gap-1.5 text-xs text-ink-dim">
        <input type="checkbox" checked={props.restorePoint} onChange={(e) => props.setRestorePoint(e.target.checked)} className="accent-[#3DD6B5]" />
        {tr('restorePoint')}
      </label>
      <label className="flex cursor-pointer select-none items-center gap-1.5 text-xs text-ink-dim">
        <input type="checkbox" checked={props.backupReg} onChange={(e) => props.setBackupReg(e.target.checked)} className="accent-[#3DD6B5]" />
        {tr('backupReg')}
      </label>

      <div className="relative ms-auto">
        <Search size={14} className="pointer-events-none absolute start-2.5 top-1/2 -translate-y-1/2 text-ink-dim" />
        <input
          value={props.query}
          onChange={(e) => props.setQuery(e.target.value)}
          placeholder={tr('search')}
          className="w-52 rounded-lg border border-ink-border bg-black/20 py-1.5 ps-8 pe-3 text-xs text-ink-text outline-none transition focus:border-accent/60 focus:ring-2 focus:ring-accent/20"
        />
      </div>

      {props.showSelectors && (
        <div className="flex items-center gap-1">
          <button className={presetBtn} onClick={props.onAll}>{tr('selectAll')}</button>
          <button className={presetBtn} onClick={props.onNone}>{tr('selectNone')}</button>
        </div>
      )}

      <IconButton onClick={props.toggleDark} title={tr('theme')}>
        {props.dark ? <Sun size={16} /> : <Moon size={16} />}
      </IconButton>
      <IconButton onClick={props.toggleLang} title={tr('language')} active={props.lang === 'ar'}>
        <Languages size={16} />
      </IconButton>
      <IconButton onClick={props.onAbout} title={tr('about')}>
        <Info size={16} />
      </IconButton>
    </div>
  )
}

// ==================================================================== SIDEBAR
function Sidebar({
  tabs, tab, setTab, tr, selected, catalog,
}: {
  tabs: TabDef[]
  tab: TabKey
  setTab: (t: TabKey) => void
  tr: (k: Parameters<typeof t>[1]) => string
  selected: Set<string>
  catalog: CatalogItem[]
}) {
  const countFor = (key: TabKey) => {
    if (key === 'dashboard' || key === 'modes' || key === 'installed') return 0
    return catalog.filter((c) => c.category === key && selected.has(c.id)).length
  }
  return (
    <nav className="wf-scroll w-56 shrink-0 overflow-y-auto border-e border-ink-border bg-ink-panel/30 p-2.5">
      {tabs.map((tdef) => {
        const Icon = tdef.icon
        const active = tab === tdef.key
        const cnt = countFor(tdef.key)
        return (
          <button
            key={tdef.key}
            onClick={() => setTab(tdef.key)}
            className={`group relative mb-1 flex w-full items-center gap-3 rounded-lg px-3 py-2 text-sm transition-all duration-200 ${
              active ? 'bg-accent/10 text-ink-text' : 'text-ink-dim hover:bg-white/5 hover:text-ink-text'
            }`}
          >
            <span
              className={`absolute start-0 top-1/2 h-5 -translate-y-1/2 rounded-full wf-gradient transition-all duration-200 ${
                active ? 'w-1 opacity-100' : 'w-0 opacity-0'
              }`}
            />
            <Icon size={17} className={active ? 'text-accent' : ''} />
            <span className="truncate font-medium">{tr(tdef.labelKey)}</span>
            {cnt > 0 && (
              <span className="ms-auto rounded-full wf-gradient px-1.5 text-[10px] font-bold text-[#0c1714]">{cnt}</span>
            )}
          </button>
        )
      })}
    </nav>
  )
}

// ================================================================== DASHBOARD
function Dashboard({ data, tr }: { data: DashboardData | null; tr: (k: Parameters<typeof t>[1]) => string }) {
  if (!data) return <div className="text-ink-dim">Loading…</div>
  const memPct = Math.round((data.memUsedGB / data.memTotalGB) * 100)
  const diskPct = Math.round((data.diskUsedGB / data.diskTotalGB) * 100)
  return (
    <div className="animate-fadeIn grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
      <InfoCard icon={Monitor} title={tr('os')}>
        <div className="text-base font-semibold">{data.os}</div>
        <div className="text-xs text-ink-dim">Build {data.osBuild}</div>
      </InfoCard>

      <InfoCard icon={Server} title={tr('computer')}>
        <div className="text-base font-semibold">{data.computer}</div>
        <div className="text-xs text-ink-dim">{data.user}</div>
      </InfoCard>

      <InfoCard icon={Cpu} title={tr('cpu')}>
        <div className="text-sm font-semibold">{data.cpu}</div>
        <div className="mb-1 mt-2 flex justify-between text-xs text-ink-dim">
          <span>{data.cpuCores} threads</span>
          <span className="tabular-nums">{tr('load')} {data.cpuLoad}%</span>
        </div>
        <Bar value={data.cpuLoad} danger />
      </InfoCard>

      <InfoCard icon={MemoryStick} title={tr('memory')}>
        <div className="mb-1 flex justify-between text-sm">
          <span className="tabular-nums font-semibold">{data.memUsedGB.toFixed(1)} GB</span>
          <span className="text-ink-dim tabular-nums">/ {data.memTotalGB} GB</span>
        </div>
        <Bar value={memPct} danger />
        <div className="mt-1 text-xs text-ink-dim">{memPct}% {tr('used')}</div>
      </InfoCard>

      <InfoCard icon={Monitor} title={tr('gpu')}>
        <div className="text-sm font-semibold">{data.gpu}</div>
        <div className="text-xs text-ink-dim">Driver 551.86</div>
      </InfoCard>

      <InfoCard icon={HardDrive} title={tr('disk')}>
        <div className="mb-1 flex justify-between text-sm">
          <span className="tabular-nums font-semibold">{data.diskUsedGB} GB</span>
          <span className="text-ink-dim tabular-nums">/ {data.diskTotalGB} GB</span>
        </div>
        <Bar value={diskPct} danger />
        <div className="mt-1 text-xs text-ink-dim">{data.diskLabel} · {diskPct}% {tr('used')}</div>
      </InfoCard>

      <InfoCard icon={Clock} title={tr('uptime')}>
        <div className="text-base font-semibold tabular-nums">{data.uptime}</div>
      </InfoCard>

      <Card className="p-4 sm:col-span-2">
        <div className="mb-3 flex items-center gap-2 text-sm font-semibold">
          <CheckCircle2 size={16} className="text-accent" /> {tr('statusCard')}
        </div>
        <div className="grid grid-cols-1 gap-x-6 gap-y-2 sm:grid-cols-2">
          {data.status.map((s) => (
            <div key={s.label} className="flex items-center gap-2 text-sm">
              {s.ok ? (
                <CheckCircle2 size={16} className="shrink-0 text-emerald-400" />
              ) : (
                <XCircle size={16} className="shrink-0 text-rose-400" />
              )}
              <span className={s.ok ? '' : 'text-ink-dim'}>{s.label}</span>
            </div>
          ))}
        </div>
      </Card>
    </div>
  )
}

function InfoCard({
  icon: Icon, title, children,
}: {
  icon: typeof Cpu
  title: string
  children: React.ReactNode
}) {
  return (
    <Card className="p-4">
      <div className="mb-2 flex items-center gap-2 text-[11px] font-semibold uppercase tracking-wider text-ink-dim">
        <Icon size={14} className="text-accent" /> {title}
      </div>
      {children}
    </Card>
  )
}

// =============================================================== DEVICE MODES
function DeviceModes({
  tr, onMode, busy,
}: {
  tr: (k: Parameters<typeof t>[1]) => string
  onMode: (m: DeviceMode) => void
  busy: boolean
}) {
  const modes: { key: DeviceMode; titleKey: Parameters<typeof t>[1]; descKey: Parameters<typeof t>[1]; icon: typeof Gamepad2; grad: string }[] = [
    { key: 'gaming', titleKey: 'gamingMode', descKey: 'gamingModeDesc', icon: Gamepad2, grad: 'from-[#3DD6B5] to-[#2FB8A0]' },
    { key: 'programming', titleKey: 'programmingMode', descKey: 'programmingModeDesc', icon: Code2, grad: 'from-[#5b8def] to-[#3b66c9]' },
    { key: 'office', titleKey: 'officeMode', descKey: 'officeModeDesc', icon: Briefcase, grad: 'from-[#b07cf0] to-[#8a4fd0]' },
  ]
  return (
    <div className="animate-fadeIn grid grid-cols-1 gap-5 md:grid-cols-3">
      {modes.map((m) => {
        const Icon = m.icon
        return (
          <div
            key={m.key}
            className={`relative flex flex-col overflow-hidden rounded-xl border border-ink-border bg-gradient-to-br ${m.grad} p-[1px] shadow-soft transition-transform duration-200 hover:-translate-y-1`}
          >
            <div className="flex flex-1 flex-col rounded-[0.8rem] bg-ink-panel p-5">
              <div className={`mb-4 flex h-12 w-12 items-center justify-center rounded-xl bg-gradient-to-br ${m.grad}`}>
                <Icon size={24} className="text-white" />
              </div>
              <div className="text-lg font-bold">{tr(m.titleKey)}</div>
              <p className="mt-2 flex-1 text-sm leading-relaxed text-ink-dim">{tr(m.descKey)}</p>
              <button
                disabled={busy}
                onClick={() => onMode(m.key)}
                className={`mt-5 flex items-center justify-center gap-2 rounded-lg bg-gradient-to-r ${m.grad} px-4 py-2.5 text-sm font-bold text-white shadow-soft transition-all hover:brightness-110 disabled:opacity-50`}
              >
                <Play size={15} /> {tr('activate')}
              </button>
            </div>
          </div>
        )
      })}
    </div>
  )
}

// =================================================================== INSTALLED
function Installed({
  tr, onScan, busy, logs,
}: {
  tr: (k: Parameters<typeof t>[1]) => string
  onScan: () => void
  busy: boolean
  logs: LogLine[]
}) {
  const found = logs.filter((l) => l.text.startsWith('Found: ')).map((l) => l.text.replace('Found: ', ''))
  return (
    <div className="animate-fadeIn">
      <button
        disabled={busy}
        onClick={onScan}
        className="mb-4 flex items-center gap-2 rounded-lg wf-gradient px-4 py-2.5 text-sm font-bold text-[#0c1714] shadow-soft transition hover:brightness-110 disabled:opacity-50"
      >
        <ScanLine size={16} /> {busy ? tr('scanRunning') : tr('scan')}
      </button>
      {found.length > 0 ? (
        <div className="grid grid-cols-1 gap-2 sm:grid-cols-2 lg:grid-cols-3">
          {found.map((app) => (
            <Card key={app} className="flex items-center gap-2 p-3 text-sm">
              <AppWindow size={15} className="text-accent" /> {app}
            </Card>
          ))}
        </div>
      ) : (
        <div className="text-sm text-ink-dim">{tr('noItems')}</div>
      )}
    </div>
  )
}

// ================================================================== TWEAK LIST
function TweakList({
  items, selected, toggle, tr,
}: {
  items: CatalogItem[]
  selected: Set<string>
  toggle: (id: string) => void
  tr: (k: Parameters<typeof t>[1]) => string
}) {
  if (items.length === 0) {
    return <div className="animate-fadeIn text-sm text-ink-dim">{tr('noItems')}</div>
  }
  return (
    <div className="animate-fadeIn flex flex-col gap-2">
      {items.map((it) => {
        const on = selected.has(it.id)
        return (
          <button
            key={it.id}
            onClick={() => toggle(it.id)}
            className={`group flex items-start gap-3 rounded-xl border bg-ink-panel/80 p-3.5 text-start shadow-soft transition-all duration-200 hover:border-accent/40 ${
              on ? 'border-accent/50 ring-1 ring-accent/20' : 'border-ink-border'
            }`}
          >
            <div className="pt-0.5">
              <Checkbox checked={on} />
            </div>
            <div className="min-w-0 flex-1">
              <div className="flex items-center gap-2">
                <span className="font-semibold text-ink-text">{it.title}</span>
                <Chip>{it.category}</Chip>
              </div>
              <p className="mt-0.5 text-sm text-ink-dim">{it.desc}</p>
            </div>
          </button>
        )
      })}
    </div>
  )
}

// ================================================================ LOG CONSOLE
function LogConsole({
  open, setOpen, logs, clear, tr,
}: {
  open: boolean
  setOpen: (v: boolean) => void
  logs: LogLine[]
  clear: () => void
  tr: (k: Parameters<typeof t>[1]) => string
}) {
  const endRef = useRef<HTMLDivElement>(null)
  useEffect(() => {
    if (open) endRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [logs, open])
  const color = (lvl: LogLine['level']) =>
    lvl === 'ok' ? 'text-emerald-400' : lvl === 'warn' ? 'text-amber-400' : lvl === 'err' ? 'text-rose-400' : 'text-emerald-300/90'
  return (
    <div className="border-t border-ink-border bg-ink-panel/60">
      <button
        onClick={() => setOpen(!open)}
        className="flex w-full items-center gap-2 px-5 py-2 text-xs font-semibold uppercase tracking-wider text-ink-dim transition hover:text-ink-text"
      >
        <TerminalSquare size={14} className="text-accent" />
        {tr('logConsole')}
        <span className="rounded-full border border-ink-border px-1.5 text-[10px] tabular-nums">{logs.length}</span>
        <ChevronUp size={14} className={`ms-auto transition-transform duration-200 ${open ? '' : 'rotate-180'}`} />
      </button>
      {open && (
        <div className="relative">
          <div className="wf-scroll mx-5 mb-3 h-44 overflow-y-auto rounded-lg border border-ink-border bg-[#0b0d10] p-3 font-mono text-[12px] leading-relaxed">
            {logs.length === 0 ? (
              <div className="text-emerald-700">// waiting for output…</div>
            ) : (
              logs.map((l) => (
                <div key={l.id} className={color(l.level)}>
                  <span className="select-none text-emerald-800">$ </span>
                  {l.text}
                </div>
              ))
            )}
            <div ref={endRef} />
          </div>
          {logs.length > 0 && (
            <button
              onClick={clear}
              className="absolute end-7 top-2 rounded-md border border-ink-border bg-black/40 px-2 py-0.5 text-[10px] text-ink-dim transition hover:text-rose-400"
            >
              clear
            </button>
          )}
        </div>
      )}
    </div>
  )
}

// ================================================================= BOTTOM BAR
function BottomBar({
  tr, progress, status, selectedCount, busy, onApply, onRevert, onDocker, onRestartExplorer,
}: {
  tr: (k: Parameters<typeof t>[1]) => string
  progress: number
  status: string
  selectedCount: number
  busy: boolean
  onApply: () => void
  onRevert: () => void
  onDocker: () => void
  onRestartExplorer: () => void
}) {
  const secondaryBtn =
    'flex items-center gap-1.5 rounded-lg border border-ink-border px-3 py-2 text-xs font-medium text-ink-dim transition-all hover:border-accent/50 hover:text-accent disabled:opacity-50'
  return (
    <div className="flex items-center gap-4 border-t border-ink-border bg-ink-panel/70 px-5 py-3 backdrop-blur">
      <div className="min-w-0 flex-1">
        <div className="mb-1 flex items-center gap-2 text-xs text-ink-dim">
          <span className="truncate">{status}</span>
          <span className="ms-auto tabular-nums">{progress}%</span>
        </div>
        <Bar value={progress} />
      </div>

      <div className="flex shrink-0 items-center gap-2">
        <button className={secondaryBtn} onClick={onDocker} disabled={busy}>
          <Container size={14} /> {tr('dockerPrune')}
        </button>
        <button className={secondaryBtn} onClick={onRestartExplorer} disabled={busy}>
          <RotateCcw size={14} /> {tr('restartExplorer')}
        </button>
        <button className={secondaryBtn} onClick={onRevert} disabled={busy}>
          <Undo2 size={14} /> {tr('revert')}
        </button>
        <button
          onClick={onApply}
          disabled={busy || selectedCount === 0}
          className="flex items-center gap-2 rounded-lg wf-gradient px-5 py-2.5 text-sm font-bold text-[#0c1714] shadow-glow transition-all hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-50"
        >
          <Play size={16} />
          {tr('applySelected')}
          {selectedCount > 0 && (
            <span className="rounded-full bg-[#0c1714]/20 px-1.5 text-xs tabular-nums">{selectedCount}</span>
          )}
        </button>
      </div>
    </div>
  )
}

// ================================================================ ABOUT MODAL
function AboutModal({ tr, close }: { tr: (k: Parameters<typeof t>[1]) => string; close: () => void }) {
  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm"
      onClick={close}
    >
      <Card className="animate-fadeIn w-full max-w-md p-6" glow>
        <div className="mb-3 flex items-center justify-between">
          <span className="wf-gradient-text text-xl font-bold">WinForge {VERSION}</span>
          <button onClick={close} className="text-ink-dim transition hover:text-ink-text">
            <X size={18} />
          </button>
        </div>
        <p className="text-sm leading-relaxed text-ink-dim">{tr('aboutText')}</p>
        <div className="mt-4 flex items-center gap-2 text-xs text-ink-dim">
          <span className={`h-2 w-2 rounded-full ${IS_REAL ? 'wf-gradient' : 'bg-ink-dim'}`} />
          {IS_REAL ? 'Connected to WebView2 host' : 'Standalone mock mode'}
        </div>
      </Card>
    </div>
  )
}
