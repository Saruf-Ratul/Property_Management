import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { api, unwrapError } from '@/lib/api'
import type { CaseDetail, ClientDto, DelinquentTenantDto, PagedResult } from '@/types'
import { Card, CardBody, CardHeader } from '@/components/ui/Card'
import toast from 'react-hot-toast'
import { Spinner } from '@/components/ui/Spinner'
import { useState } from 'react'

const schema = z.object({
  title: z.string().min(3, 'Title required'),
  caseType: z.enum(['LandlordTenantEviction', 'LandlordTenantHoldover', 'Other']),
  clientId: z.string().min(1, 'Client required'),
  pmsLeaseId: z.string().optional().nullable(),
  amountInControversy: z.preprocess(v => v === '' ? undefined : Number(v), z.number().nonnegative().optional()),
  description: z.string().optional(),
})
type V = z.infer<typeof schema>

export function NewCasePage() {
  const nav = useNavigate()
  const [submitting, setSubmitting] = useState(false)

  const clientsQ = useQuery({
    queryKey: ['clients-mini'],
    queryFn: async () => (await api.get<PagedResult<ClientDto>>('/clients', { params: { pageSize: 100 } })).data,
  })

  const delinquentQ = useQuery({
    queryKey: ['delinquent-mini'],
    queryFn: async () => (await api.get<PagedResult<DelinquentTenantDto>>('/tenants/delinquent', { params: { pageSize: 100, minBalance: 1 } })).data,
  })

  const { register, handleSubmit, watch, formState: { errors } } = useForm<V>({
    resolver: zodResolver(schema),
    defaultValues: { caseType: 'LandlordTenantEviction' },
  })
  const watchedClient = watch('clientId')

  const onSubmit = async (data: V) => {
    setSubmitting(true)
    try {
      const r = await api.post<CaseDetail>('/cases', {
        title: data.title,
        caseType: data.caseType,
        clientId: data.clientId,
        pmsLeaseId: data.pmsLeaseId || null,
        amountInControversy: data.amountInControversy ?? null,
        description: data.description ?? null,
      })
      toast.success(`Case ${r.data.caseNumber} created`)
      // Snapshot PMS data immediately if linked to a lease, then go to detail
      if (data.pmsLeaseId) {
        try { await api.post(`/cases/${r.data.id}/snapshot`) } catch { /* non-fatal */ }
      }
      nav(`/cases/${r.data.id}`)
    } catch (e) {
      toast.error(unwrapError(e))
    } finally {
      setSubmitting(false)
    }
  }

  const filteredDelinquent = (delinquentQ.data?.items ?? [])
    .filter(d => !watchedClient || d.clientId === watchedClient)

  return (
    <div className="max-w-3xl mx-auto space-y-5">
      <div>
        <h1 className="text-xl font-semibold">New case</h1>
        <p className="text-sm text-slate-500">Create manually or from delinquent PMS tenant.</p>
      </div>

      <Card>
        <CardHeader title="Case details" />
        <CardBody>
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
            <div>
              <label className="label">Title</label>
              <input className="input" placeholder="e.g. Smith — Maple Court Apt 1A — Non-payment" {...register('title')} />
              {errors.title && <p className="text-xs text-rose-600 mt-1">{errors.title.message}</p>}
            </div>

            <div className="grid sm:grid-cols-2 gap-4">
              <div>
                <label className="label">Case type</label>
                <select className="input" {...register('caseType')}>
                  <option value="LandlordTenantEviction">LT — Eviction (Non-payment)</option>
                  <option value="LandlordTenantHoldover">LT — Holdover</option>
                  <option value="Other">Other</option>
                </select>
              </div>
              <div>
                <label className="label">Client</label>
                <select className="input" {...register('clientId')}>
                  <option value="">— Select client —</option>
                  {clientsQ.data?.items.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
                </select>
                {errors.clientId && <p className="text-xs text-rose-600 mt-1">{errors.clientId.message}</p>}
              </div>
            </div>

            <div>
              <label className="label">From PMS — delinquent tenant (optional)</label>
              <select className="input" {...register('pmsLeaseId')}>
                <option value="">— Manual entry (no PMS link) —</option>
                {filteredDelinquent.map(d => (
                  <option key={d.leaseId} value={d.leaseId}>
                    {d.tenantName} · {d.propertyName} #{d.unitNumber} · ${d.currentBalance.toFixed(2)} due
                  </option>
                ))}
              </select>
              <p className="text-xs text-slate-500 mt-1">Selecting a lease snapshots the tenant, unit, property, and ledger onto the case.</p>
            </div>

            <div>
              <label className="label">Amount in controversy</label>
              <input className="input" type="number" step="0.01" placeholder="0.00" {...register('amountInControversy' as any)} />
            </div>

            <div>
              <label className="label">Description</label>
              <textarea className="input min-h-[80px]" {...register('description')} />
            </div>

            <div className="flex justify-end gap-2">
              <button type="button" className="btn-secondary" onClick={() => nav(-1)}>Cancel</button>
              <button type="submit" className="btn-primary" disabled={submitting}>
                {submitting ? <Spinner size={14} className="text-white" /> : 'Create case'}
              </button>
            </div>
          </form>
        </CardBody>
      </Card>
    </div>
  )
}
