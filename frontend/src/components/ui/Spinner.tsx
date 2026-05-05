import { Loader2 } from 'lucide-react'
import clsx from 'clsx'

export function Spinner({ size = 16, className }: { size?: number; className?: string }) {
  return <Loader2 size={size} className={clsx('animate-spin text-brand-600', className)} />
}

export function PageLoader() {
  return (
    <div className="h-full w-full flex items-center justify-center py-24">
      <Spinner size={28} />
    </div>
  )
}
