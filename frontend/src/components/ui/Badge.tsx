import clsx from 'clsx'
import type { ReactNode } from 'react'

type Tone = 'gray' | 'blue' | 'green' | 'amber' | 'rose' | 'violet' | 'sky'

const tones: Record<Tone, string> = {
  gray: 'bg-slate-100 text-slate-700',
  blue: 'bg-brand-50 text-brand-700',
  green: 'bg-emerald-50 text-emerald-700',
  amber: 'bg-amber-50 text-amber-700',
  rose: 'bg-rose-50 text-rose-700',
  violet: 'bg-violet-50 text-violet-700',
  sky: 'bg-sky-50 text-sky-700',
}

export function Badge({ children, tone = 'gray', className }: { children: ReactNode; tone?: Tone; className?: string }) {
  return <span className={clsx('badge', tones[tone], className)}>{children}</span>
}

export function stageTone(code: string): Tone {
  switch (code) {
    case 'Closed': case 'Dismissed': return 'gray'
    case 'Filed': case 'CourtDateScheduled': case 'WarrantRequested': return 'amber'
    case 'Judgment': case 'Settlement': return 'green'
    case 'ReadyToFile': return 'blue'
    case 'Intake': case 'Draft': case 'FormReview': return 'sky'
    default: return 'gray'
  }
}
