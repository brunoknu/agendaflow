import { useQuery } from '@tanstack/react-query';
import { appointmentsApi } from '../../api/client';
import AppShell from '../../components/AppShell';
import { useAuth } from '../../hooks/useAuth';

export default function DashboardPage() {
  const { user } = useAuth();
  const today = new Date();
  const todayStr = today.toISOString().split('T')[0];
  const tomorrowStr = new Date(today.getTime() + 86_400_000).toISOString().split('T')[0];

  const { data: todayData } = useQuery({
    queryKey: ['appointments-today'],
    queryFn: () => appointmentsApi.list({ from: `${todayStr}T00:00:00Z`, to: `${tomorrowStr}T00:00:00Z`, pageSize: '100' }),
  });

  const { data: upcomingData } = useQuery({
    queryKey: ['appointments-upcoming'],
    queryFn: () => appointmentsApi.list({
      from: new Date().toISOString(),
      status: 'Confirmed',
      pageSize: '5',
    }),
  });

  const statuses: Record<string, string> = {
    PendingConfirmation: 'Pendente',
    Confirmed: 'Confirmado',
    CheckedIn: 'Presente',
    Completed: 'Concluído',
    Cancelled: 'Cancelado',
    NoShow: 'Não compareceu',
  };

  const todayByStatus = todayData?.items.reduce<Record<string, number>>((acc, a) => {
    acc[a.status] = (acc[a.status] ?? 0) + 1;
    return acc;
  }, {}) ?? {};

  return (
    <AppShell>
      <h1 style={{ fontSize: '1.25rem', fontWeight: 600, marginBottom: '0.25rem' }}>Dashboard</h1>
      <p style={{ color: 'var(--color-text-muted)', fontSize: '0.875rem', marginBottom: '2rem' }}>
        Bem-vindo, {user?.fullName?.split(' ')[0]}
      </p>

      {/* Stats row */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(180px, 1fr))', gap: '1rem', marginBottom: '2rem' }}>
        <StatCard label="Agendamentos hoje" value={todayData?.totalCount ?? '—'} />
        <StatCard label="Próximos confirmados" value={upcomingData?.totalCount ?? '—'} />
      </div>

      {/* Today breakdown */}
      {todayData && todayData.totalCount > 0 && (
        <section style={{ marginBottom: '2rem' }}>
          <h2 style={{ fontSize: '0.9375rem', fontWeight: 600, marginBottom: '0.75rem' }}>Hoje por status</h2>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.5rem' }}>
            {Object.entries(todayByStatus).map(([s, count]) => (
              <span key={s} style={{
                padding: '0.25rem 0.75rem',
                borderRadius: 20,
                fontSize: '0.8125rem',
                background: 'var(--color-surface)',
                border: '1px solid var(--color-border)',
              }}>
                {statuses[s] ?? s}: <strong>{count}</strong>
              </span>
            ))}
          </div>
        </section>
      )}

      {/* Próximos */}
      {upcomingData && upcomingData.items.length > 0 && (
        <section>
          <h2 style={{ fontSize: '0.9375rem', fontWeight: 600, marginBottom: '0.75rem' }}>Próximos agendamentos</h2>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
            {upcomingData.items.map(a => (
              <div key={a.id} style={{
                background: 'var(--color-surface)',
                border: '1px solid var(--color-border)',
                borderRadius: 8,
                padding: '0.75rem 1rem',
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center',
                gap: '1rem',
              }}>
                <div>
                  <p style={{ fontSize: '0.875rem', fontWeight: 500 }}>{a.customerName}</p>
                  <p style={{ fontSize: '0.8125rem', color: 'var(--color-text-muted)' }}>{a.serviceName} · {a.professionalName}</p>
                </div>
                <p style={{ fontSize: '0.8125rem', color: 'var(--color-text-muted)', whiteSpace: 'nowrap' }}>
                  {new Date(a.startAtUtc).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' })}
                </p>
              </div>
            ))}
          </div>
        </section>
      )}

      {(!todayData || todayData.totalCount === 0) && (!upcomingData || upcomingData.items.length === 0) && (
        <p style={{ color: 'var(--color-text-muted)', fontSize: '0.875rem' }}>Nenhum agendamento hoje.</p>
      )}
    </AppShell>
  );
}

function StatCard({ label, value }: { label: string; value: number | string }) {
  return (
    <div style={{
      background: 'var(--color-surface)',
      border: '1px solid var(--color-border)',
      borderRadius: 8,
      padding: '1.25rem',
    }}>
      <p style={{ fontSize: '0.8125rem', color: 'var(--color-text-muted)', marginBottom: '0.375rem' }}>{label}</p>
      <p style={{ fontSize: '1.75rem', fontWeight: 700, color: 'var(--color-primary)' }}>{value}</p>
    </div>
  );
}
