import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { api, unwrapError } from '@/lib/api'
import type { ClientDto, PagedResult } from '@/types'
import { Table } from '@/components/ui/Table'
import { Modal } from '@/components/ui/Modal'
import { Pencil, Plus, Trash2 } from 'lucide-react'
import toast from 'react-hot-toast'
import { useAuth } from '@/lib/auth'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'

const schema = z.object({
  name: z.string().min(2, 'Name must be at least 2 characters'),
  contactName: z.string().optional().nullable(),
  contactEmail: z.string().email('Enter a valid email').optional().or(z.literal('')),
  contactPhone: z.string().optional().nullable(),
  addressLine1: z.string().optional().nullable(),
  city: z.string().optional().nullable(),
  state: z.string().optional().nullable(),
  postalCode: z.string().optional().nullable(),
  isActive: z.boolean().optional(),
})
type V = z.infer<typeof schema>

type DialogMode =
  | { kind: 'closed' }
  | { kind: 'create' }
  | { kind: 'edit'; client: ClientDto }
  | { kind: 'delete'; client: ClientDto }

export function ClientsPage() {
  const [dialog, setDialog] = useState<DialogMode>({ kind: 'closed' })
  const { hasAnyRole } = useAuth()
  const canWrite = hasAnyRole(['FirmAdmin'])
  const qc = useQueryClient()

  const q = useQuery({
    queryKey: ['clients'],
    queryFn: async () =>
      (await api.get<PagedResult<ClientDto>>('/clients', { params: { pageSize: 100 } })).data,
  })

  const isEdit = dialog.kind === 'edit'
  const isCreateOrEdit = dialog.kind === 'create' || dialog.kind === 'edit'

  const form = useForm<V>({
    resolver: zodResolver(schema),
    defaultValues: { name: '', contactName: '', contactEmail: '', contactPhone: '', city: '', state: '', isActive: true },
  })

  // Hydrate the form whenever the dialog opens.
  useEffect(() => {
    if (dialog.kind === 'edit') {
      form.reset({
        name: dialog.client.name,
        contactName: dialog.client.contactName ?? '',
        contactEmail: dialog.client.contactEmail ?? '',
        contactPhone: dialog.client.contactPhone ?? '',
        city: dialog.client.city ?? '',
        state: dialog.client.state ?? '',
        isActive: dialog.client.isActive,
      })
    } else if (dialog.kind === 'create') {
      form.reset({ name: '', contactName: '', contactEmail: '', contactPhone: '', city: '', state: '', isActive: true })
    }
  }, [dialog, form])

  const closeDialog = () => setDialog({ kind: 'closed' })

  const createM = useMutation({
    mutationFn: async (data: V) =>
      (await api.post<ClientDto>('/clients', { ...data, contactEmail: data.contactEmail || null })).data,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['clients'] })
      toast.success('Client created')
      closeDialog()
    },
    onError: e => toast.error(unwrapError(e)),
  })

  const updateM = useMutation({
    mutationFn: async ({ id, data }: { id: string; data: V }) =>
      (await api.put<ClientDto>(`/clients/${id}`, {
        ...data,
        contactEmail: data.contactEmail || null,
        isActive: data.isActive ?? true,
      })).data,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['clients'] })
      toast.success('Client updated')
      closeDialog()
    },
    onError: e => toast.error(unwrapError(e)),
  })

  const deleteM = useMutation({
    mutationFn: async (id: string) => (await api.delete(`/clients/${id}`)).data,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['clients'] })
      toast.success('Client deleted')
      closeDialog()
    },
    onError: e => toast.error(unwrapError(e)),
  })

  const onSubmit = (data: V) => {
    if (dialog.kind === 'edit') updateM.mutate({ id: dialog.client.id, data })
    else if (dialog.kind === 'create') createM.mutate(data)
  }

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">Clients</h1>
          <p className="text-sm text-slate-500">Property management companies your firm represents.</p>
        </div>
        {canWrite && (
          <button className="btn-primary" onClick={() => setDialog({ kind: 'create' })}>
            <Plus size={14} /> New client
          </button>
        )}
      </div>

      <Table<ClientDto>
        loading={q.isLoading}
        rows={q.data?.items ?? []}
        rowKey={r => r.id}
        columns={[
          { key: 'name', header: 'Name', render: r => <span className="font-medium">{r.name}</span> },
          {
            key: 'contact',
            header: 'Contact',
            render: r => `${r.contactName ?? '—'}${r.contactEmail ? ` · ${r.contactEmail}` : ''}`,
          },
          {
            key: 'city',
            header: 'Location',
            render: r => [r.city, r.state].filter(Boolean).join(', ') || '—',
          },
          { key: 'integ', header: 'Integrations', render: r => r.integrationsCount, className: 'text-right' },
          { key: 'cases', header: 'Cases', render: r => r.casesCount, className: 'text-right' },
          {
            key: 'active',
            header: 'Status',
            render: r => (
              <span className={r.isActive ? 'text-emerald-700' : 'text-slate-400'}>
                {r.isActive ? 'Active' : 'Inactive'}
              </span>
            ),
          },
          ...(canWrite
            ? [{
                key: 'actions',
                header: '',
                className: 'text-right',
                render: (r: ClientDto) => (
                  <div className="flex items-center justify-end gap-2">
                    <button
                      type="button"
                      title="Edit client"
                      className="rounded-md p-1.5 hover:bg-slate-100 text-slate-600"
                      onClick={() => setDialog({ kind: 'edit', client: r })}
                    >
                      <Pencil size={15} />
                    </button>
                    <button
                      type="button"
                      title="Delete client"
                      className="rounded-md p-1.5 hover:bg-rose-50 text-rose-600 disabled:opacity-40"
                      onClick={() => setDialog({ kind: 'delete', client: r })}
                    >
                      <Trash2 size={15} />
                    </button>
                  </div>
                ),
              }]
            : []),
        ]}
      />

      {/* Create / Edit modal */}
      <Modal
        open={isCreateOrEdit}
        onClose={closeDialog}
        title={isEdit ? `Edit — ${dialog.kind === 'edit' ? dialog.client.name : ''}` : 'New client'}
      >
        <form className="space-y-3" onSubmit={form.handleSubmit(onSubmit)}>
          <div>
            <label className="label">Name</label>
            <input className="input" {...form.register('name')} />
            {form.formState.errors.name && (
              <p className="text-xs text-rose-600 mt-1">{form.formState.errors.name.message}</p>
            )}
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="label">Contact name</label>
              <input className="input" {...form.register('contactName')} />
            </div>
            <div>
              <label className="label">Contact email</label>
              <input className="input" {...form.register('contactEmail')} />
              {form.formState.errors.contactEmail && (
                <p className="text-xs text-rose-600 mt-1">{form.formState.errors.contactEmail.message}</p>
              )}
            </div>
            <div>
              <label className="label">Phone</label>
              <input className="input" {...form.register('contactPhone')} />
            </div>
            <div>
              <label className="label">Address</label>
              <input className="input" {...form.register('addressLine1')} />
            </div>
            <div>
              <label className="label">City</label>
              <input className="input" {...form.register('city')} />
            </div>
            <div>
              <label className="label">State</label>
              <input className="input" {...form.register('state')} />
            </div>
            <div>
              <label className="label">Postal code</label>
              <input className="input" {...form.register('postalCode')} />
            </div>
          </div>
          {isEdit && (
            <label className="flex items-center gap-2 text-sm text-slate-600">
              <input type="checkbox" {...form.register('isActive')} /> Active
            </label>
          )}
          <div className="flex justify-end gap-2 pt-2">
            <button type="button" className="btn-secondary" onClick={closeDialog}>
              Cancel
            </button>
            <button
              type="submit"
              className="btn-primary"
              disabled={form.formState.isSubmitting || createM.isPending || updateM.isPending}
            >
              {isEdit ? 'Save' : 'Create'}
            </button>
          </div>
        </form>
      </Modal>

      {/* Delete confirm modal */}
      <Modal
        open={dialog.kind === 'delete'}
        onClose={closeDialog}
        title={dialog.kind === 'delete' ? `Delete ${dialog.client.name}?` : ''}
      >
        {dialog.kind === 'delete' && (
          <div className="space-y-4">
            <p className="text-sm text-slate-600">
              This will remove <span className="font-medium text-slate-900">{dialog.client.name}</span> from your
              firm's client list. The action is recorded in the audit log.
            </p>
            {(dialog.client.casesCount > 0 || dialog.client.integrationsCount > 0) && (
              <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800">
                This client has {dialog.client.casesCount} case(s) and {dialog.client.integrationsCount} PMS integration(s).
                The server will reject the delete — close those out first or set the client inactive instead.
              </div>
            )}
            <div className="flex justify-end gap-2 pt-2">
              <button type="button" className="btn-secondary" onClick={closeDialog}>
                Cancel
              </button>
              <button
                type="button"
                className="rounded-md bg-rose-600 px-3 py-2 text-sm font-medium text-white hover:bg-rose-700 disabled:opacity-50"
                disabled={deleteM.isPending}
                onClick={() => deleteM.mutate(dialog.client.id)}
              >
                {deleteM.isPending ? 'Deleting…' : 'Delete client'}
              </button>
            </div>
          </div>
        )}
      </Modal>
    </div>
  )
}
