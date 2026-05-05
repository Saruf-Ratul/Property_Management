import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useNavigate, useLocation, Navigate } from 'react-router-dom'
import { useAuth } from '@/lib/auth'
import { Spinner } from '@/components/ui/Spinner'
import { Scale } from 'lucide-react'

const schema = z.object({
  email: z.string().email('Enter a valid email'),
  password: z.string().min(1, 'Password required'),
})
type FormValues = z.infer<typeof schema>

export function LoginPage() {
  const { login, isAuthenticated, isLoading, isClientUser } = useAuth()
  const nav = useNavigate()
  const loc = useLocation() as { state?: { from?: { pathname?: string } } }

  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: '', password: '' },
  })

  // Already-authenticated users land on /portal (client) or / (firm).
  const defaultLanding = isClientUser ? '/portal' : '/'
  if (isAuthenticated) return <Navigate to={loc.state?.from?.pathname || defaultLanding} replace />

  const onSubmit = async (data: FormValues) => {
    try {
      const u = await login(data.email, data.password)
      const isClient = u.roles.some(r => r === 'ClientAdmin' || r === 'ClientUser')
      const target = loc.state?.from?.pathname ?? (isClient ? '/portal' : '/')
      nav(target, { replace: true })
    } catch { /* toast already shown */ }
  }

  return (
    <div className="min-h-full grid lg:grid-cols-2">
      <div className="hidden lg:flex bg-brand-700 text-white p-12 flex-col justify-between bg-gradient-to-br from-brand-700 via-brand-800 to-brand-950">
        <div className="flex items-center gap-3">
          <div className="rounded-lg bg-white/15 p-2"><Scale size={20} /></div>
          <div className="text-lg font-semibold">Property Management</div>
        </div>
        <div className="space-y-3 max-w-md">
          <h1 className="text-3xl font-bold leading-tight">Run a modern landlord-tenant practice.</h1>
          <p className="text-brand-100 leading-relaxed">
            Pull live property data from Rent Manager and other PMS systems. Auto-fill New Jersey LT
            court forms. Track every case from intake to warrant. Give your clients a clean portal.
          </p>
        </div>
        <div className="text-xs text-brand-200">© Property Management Platform</div>
      </div>

      <div className="flex items-center justify-center p-6 sm:p-12">
        <div className="w-full max-w-md">
          <h2 className="text-2xl font-semibold tracking-tight">Sign in</h2>
          <p className="text-sm text-slate-500 mt-1">Use your firm or client portal account</p>

          <form className="mt-6 space-y-4" onSubmit={handleSubmit(onSubmit)}>
            <div>
              <label className="label">Email</label>
              <input type="email" className="input" autoComplete="email" {...register('email')} />
              {errors.email && <p className="text-xs text-rose-600 mt-1">{errors.email.message}</p>}
            </div>
            <div>
              <label className="label">Password</label>
              <input type="password" className="input" autoComplete="current-password" {...register('password')} />
              {errors.password && <p className="text-xs text-rose-600 mt-1">{errors.password.message}</p>}
            </div>
            <button type="submit" disabled={isSubmitting || isLoading} className="btn-primary w-full">
              {isSubmitting || isLoading ? <Spinner size={14} className="text-white" /> : 'Sign in'}
            </button>
          </form>
        </div>
      </div>
    </div>
  )
}
