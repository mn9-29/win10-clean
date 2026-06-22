import type { ReactNode } from 'react'

export function Card({
  children,
  className = '',
  glow = false,
}: {
  children: ReactNode
  className?: string
  glow?: boolean
}) {
  return (
    <div
      className={`rounded-xl border border-ink-border bg-ink-panel/80 dark:bg-ink-panel transition-all duration-200 ${
        glow ? 'shadow-glow' : 'shadow-soft'
      } ${className}`}
    >
      {children}
    </div>
  )
}

export function Checkbox({ checked }: { checked: boolean }) {
  return (
    <span
      className={`flex h-5 w-5 shrink-0 items-center justify-center rounded-md border transition-all duration-200 ${
        checked
          ? 'wf-gradient border-transparent shadow-[0_0_0_3px_rgba(61,214,181,0.15)]'
          : 'border-ink-border bg-black/20'
      }`}
    >
      {checked && (
        <svg viewBox="0 0 24 24" className="h-3.5 w-3.5 text-[#0c1714]" fill="none" stroke="currentColor" strokeWidth="3.5" strokeLinecap="round" strokeLinejoin="round">
          <path d="M20 6 9 17l-5-5" />
        </svg>
      )}
    </span>
  )
}

export function Switch({
  checked,
  onChange,
  disabled = false,
}: {
  checked: boolean
  onChange: (v: boolean) => void
  disabled?: boolean
}) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      disabled={disabled}
      onClick={() => onChange(!checked)}
      className={`relative inline-flex h-6 w-11 shrink-0 items-center rounded-full border transition-colors duration-200 focus:outline-none focus-visible:ring-2 focus-visible:ring-accent/40 disabled:cursor-not-allowed disabled:opacity-50 ${
        checked ? 'wf-gradient border-transparent' : 'border-ink-border bg-black/30'
      }`}
    >
      <span
        className={`inline-block h-4 w-4 transform rounded-full bg-white shadow transition-transform duration-200 ${
          checked ? 'translate-x-5' : 'translate-x-1'
        }`}
      />
    </button>
  )
}

export function Chip({ children }: { children: ReactNode }) {
  return (
    <span className="rounded-md border border-ink-border bg-black/20 px-2 py-0.5 text-[10px] font-medium uppercase tracking-wide text-ink-dim">
      {children}
    </span>
  )
}

export function Bar({ value, danger = false }: { value: number; danger?: boolean }) {
  const v = Math.max(0, Math.min(100, value))
  return (
    <div className="h-2 w-full overflow-hidden rounded-full bg-black/30">
      <div
        className={`h-full rounded-full transition-all duration-700 ease-out ${
          danger && v > 85 ? 'bg-gradient-to-r from-rose-500 to-orange-400' : 'wf-gradient'
        }`}
        style={{ width: `${v}%` }}
      />
    </div>
  )
}

export function IconButton({
  children,
  onClick,
  title,
  active = false,
}: {
  children: ReactNode
  onClick?: () => void
  title?: string
  active?: boolean
}) {
  return (
    <button
      onClick={onClick}
      title={title}
      className={`flex h-9 w-9 items-center justify-center rounded-lg border transition-all duration-200 hover:border-accent/50 hover:text-accent ${
        active ? 'border-accent/60 text-accent' : 'border-ink-border text-ink-dim'
      }`}
    >
      {children}
    </button>
  )
}
