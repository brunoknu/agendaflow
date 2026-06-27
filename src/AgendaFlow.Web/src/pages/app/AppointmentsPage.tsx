import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { appointmentsApi, type AppointmentDto, ApiError } from '../../api/client';
import AppShell from '../../components/AppShell';
import Modal from '../../components/Modal';

const STATUS_LABELS: Record<string, string> = {
  PendingConfirmation: 'Pendente',
  Confirmed: 'Confirmado',
  CheckedIn: 'Presente',
  Completed: 'Concluído',
  Cancelled: 'Cancelado',
  NoShow: 'Não compareceu',
};

const STATUS_TRANSITIONS: Record<string, { label: string; next: string; danger?: boolean }[]> = {
  PendingConfirmation: [
    { label: 'Confirmar', next: 'Confirmed' },
    { label: 'Cancelar', next: 'Cancelled', danger: true },
  ],
  Confirmed: [
    { label: 'Chegou', next: 'CheckedIn' },
    { label: 'Cancelar', next: 'Cancelled', danger: true },
    { label: 'Não veio', next: 'NoShow', danger: true },
  ],
  CheckedIn: [
    { label: 'Concluir', next: 'Completed' },
  ],
};

export default function AppointmentsPage() {
  const qc = useQueryClient();
  const [page, setPage] = useState(1);
  const [statusFilter, setStatusFilter] = useState('');
  const [statusModal, setStatusModal] = useState<{ appointment: AppointmentDto; next: string } | null>(null);
  const [reason, setReason] = useState('');
  const [actionError, setActionError] = useState<string | null>(null);

  const { data, isLoading, error } = useQuery({
    queryKey: ['appointments', page, statusFilter],
    queryFn: () => appointmentsApi.list({
      page: String(page),
      pageSize: '25',
      ...(statusFilter ? { status: statusFilter } : {}),
    }),
  });

  const statusMutation = useMutation({
    mutationFn: ({ id, newStatus, reason }: { id: string; newStatus: string; reason?: string }) =>
      appointmentsApi.updateStatus(id, newStatus, reason),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['appointments'] });
      setStatusModal(null);
      setReason('');
    },
    onError: (err) => {
      setActionError(err instanceof ApiError ? (err.detail ?? err.message) : 'Erro ao atualizar status.');
    },
  });

  const openStatusModal = (appointment: AppointmentDto, next: string) => {
    setActionError(null);
    setReason('');
    setStatusModal({ appointment, next });
  };

  return (
    <AppShell>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.5rem', gap: '1rem' }}>
        <h1 style={{ fontSize: '1.25rem', fontWeight: 600 }}>Agendamentos</h1>
        <select
          value={statusFilter}
          onChange={e => { setStatusFilter(e.target.value); setPage(1); }}
          style={{ padding: '0.4rem 0.625rem', borderRadius: 6, border: '1px solid var(--color-border)', fontSize: '0.875rem', background: 'var(--color-surface)', color: 'var(--color-text)' }}
        >
          <option value="">Todos os status</option>
          {Object.entries(STATUS_LABELS).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
        </select>
      </div>

      {isLoading && <p style={{ color: 'var(--color-text-muted)' }}>Carregando...</p>}
      {error && <p style={{ color: 'var(--color-error)' }}>Erro ao carregar agendamentos.</p>}

      {data && data.items.length === 0 && (
        <p style={{ color: 'var(--color-text-muted)', fontSize: '0.875rem' }}>Nenhum agendamento encontrado.</p>
      )}

      {data && data.items.length > 0 && (
        <>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
            {data.items.map((a: AppointmentDto) => {
              const transitions = STATUS_TRANSITIONS[a.status] ?? [];
              return (
                <div key={a.id} style={{
                  background: 'var(--color-surface)',
                  border: '1px solid var(--color-border)',
                  borderRadius: 8,
                  padding: '0.875rem 1rem',
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  gap: '1rem',
                }}>
                  <div style={{ flex: 1 }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', flexWrap: 'wrap' }}>
                      <span style={{ fontWeight: 500, fontSize: '0.9375rem' }}>{a.customerName}</span>
                      <StatusBadge status={a.status} />
                    </div>
                    <p style={{ fontSize: '0.8125rem', color: 'var(--color-text-muted)', marginTop: '0.125rem' }}>
                      {a.serviceName} · {a.professionalName}
                    </p>
                    <p style={{ fontSize: '0.8125rem', color: 'var(--color-text-muted)' }}>
                      {new Date(a.startAtUtc).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' })}
                    </p>
                  </div>
                  {transitions.length > 0 && (
                    <div style={{ display: 'flex', gap: '0.375rem', flexShrink: 0 }}>
                      {transitions.map(t => (
                        <button
                          key={t.next}
                          onClick={() => openStatusModal(a, t.next)}
                          style={{
                            padding: '0.375rem 0.625rem',
                            background: 'none',
                            border: `1px solid ${t.danger ? 'var(--color-error)' : 'var(--color-border)'}`,
                            borderRadius: 6,
                            fontSize: '0.8125rem',
                            cursor: 'pointer',
                            color: t.danger ? 'var(--color-error)' : 'var(--color-text)',
                          }}
                        >
                          {t.label}
                        </button>
                      ))}
                    </div>
                  )}
                </div>
              );
            })}
          </div>

          {data.totalPages > 1 && (
            <div style={{ marginTop: '1rem', display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
              <button onClick={() => setPage(p => p - 1)} disabled={page === 1} style={btnNav}>Anterior</button>
              <span style={{ fontSize: '0.875rem', color: 'var(--color-text-muted)' }}>Página {page} de {data.totalPages}</span>
              <button onClick={() => setPage(p => p + 1)} disabled={page >= data.totalPages} style={btnNav}>Próxima</button>
            </div>
          )}
        </>
      )}

      {statusModal && (
        <Modal
          title={`${STATUS_TRANSITIONS[statusModal.appointment.status]?.find(t => t.next === statusModal.next)?.label ?? 'Atualizar'} agendamento`}
          onClose={() => setStatusModal(null)}
        >
          <p style={{ fontSize: '0.875rem', marginBottom: '1rem', color: 'var(--color-text-muted)' }}>
            Cliente: <strong style={{ color: 'var(--color-text)' }}>{statusModal.appointment.customerName}</strong><br />
            Serviço: <strong style={{ color: 'var(--color-text)' }}>{statusModal.appointment.serviceName}</strong>
          </p>
          <div style={{ marginBottom: '1rem' }}>
            <label htmlFor="reason" style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 500, marginBottom: '0.25rem' }}>
              Motivo (opcional)
            </label>
            <input
              id="reason"
              type="text"
              value={reason}
              onChange={e => setReason(e.target.value)}
              style={{ width: '100%', padding: '0.5rem', borderRadius: 6, border: '1px solid var(--color-border)', fontSize: '0.875rem', background: 'var(--color-bg)', color: 'var(--color-text)' }}
            />
          </div>
          {actionError && <p role="alert" style={{ color: 'var(--color-error)', fontSize: '0.8125rem', marginBottom: '0.75rem' }}>{actionError}</p>}
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem' }}>
            <button onClick={() => setStatusModal(null)} style={btnSecondary}>Cancelar</button>
            <button
              onClick={() => statusMutation.mutate({ id: statusModal.appointment.id, newStatus: statusModal.next, reason: reason || undefined })}
              disabled={statusMutation.isPending}
              style={btnPrimary}
            >
              {statusMutation.isPending ? 'Salvando...' : 'Confirmar'}
            </button>
          </div>
        </Modal>
      )}
    </AppShell>
  );
}

function StatusBadge({ status }: { status: string }) {
  const colors: Record<string, string> = {
    PendingConfirmation: '#f59e0b',
    Confirmed: '#3b82f6',
    CheckedIn: '#8b5cf6',
    Completed: '#16a34a',
    Cancelled: '#6b7280',
    NoShow: '#dc2626',
  };
  return (
    <span style={{
      fontSize: '0.75rem',
      padding: '0.125rem 0.5rem',
      borderRadius: 20,
      background: `${colors[status] ?? '#6b7280'}20`,
      color: colors[status] ?? '#6b7280',
      fontWeight: 500,
    }}>
      {STATUS_LABELS[status] ?? status}
    </span>
  );
}

const btnPrimary: React.CSSProperties = { padding: '0.5rem 1rem', background: 'var(--color-primary)', color: 'white', border: 'none', borderRadius: 6, fontSize: '0.875rem', cursor: 'pointer', fontWeight: 500 };
const btnSecondary: React.CSSProperties = { padding: '0.375rem 0.75rem', background: 'none', color: 'var(--color-text)', border: '1px solid var(--color-border)', borderRadius: 6, fontSize: '0.8125rem', cursor: 'pointer' };
const btnNav: React.CSSProperties = { padding: '0.375rem 0.75rem', background: 'var(--color-surface)', border: '1px solid var(--color-border)', borderRadius: 6, fontSize: '0.8125rem', cursor: 'pointer' };
