import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { reportsApi } from '../../api/client';
import AppShell from '../../components/AppShell';

function toLocalDateStr(daysAgo: number): string {
  const d = new Date();
  d.setDate(d.getDate() - daysAgo);
  return d.toISOString().split('T')[0];
}

const STATUS_LABELS: Record<string, string> = {
  PendingConfirmation: 'Pendente',
  Confirmed: 'Confirmado',
  CheckedIn: 'Presente',
  Completed: 'Concluído',
  Cancelled: 'Cancelado',
  NoShow: 'Não compareceu',
};

export default function ReportsPage() {
  const [from, setFrom] = useState(toLocalDateStr(30));
  const [to, setTo] = useState(toLocalDateStr(0));
  const [appliedFrom, setAppliedFrom] = useState(from);
  const [appliedTo, setAppliedTo] = useState(to);

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['reports-appointments', appliedFrom, appliedTo],
    queryFn: () => reportsApi.appointments(appliedFrom, appliedTo),
  });

  const apply = () => { setAppliedFrom(from); setAppliedTo(to); void refetch(); };

  return (
    <AppShell>
      <h1 style={{ fontSize: '1.25rem', fontWeight: 600, marginBottom: '1.5rem' }}>Relatórios</h1>

      {/* Date filter */}
      <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'flex-end', marginBottom: '2rem', flexWrap: 'wrap' }}>
        <div>
          <label style={{ display: 'block', fontSize: '0.8125rem', marginBottom: '0.25rem' }}>De</label>
          <input type="date" value={from} max={to} onChange={e => setFrom(e.target.value)}
            style={{ padding: '0.4rem 0.625rem', border: '1px solid var(--color-border)', borderRadius: 6, fontSize: '0.875rem', background: 'var(--color-surface)', color: 'var(--color-text)' }} />
        </div>
        <div>
          <label style={{ display: 'block', fontSize: '0.8125rem', marginBottom: '0.25rem' }}>Até</label>
          <input type="date" value={to} min={from} onChange={e => setTo(e.target.value)}
            style={{ padding: '0.4rem 0.625rem', border: '1px solid var(--color-border)', borderRadius: 6, fontSize: '0.875rem', background: 'var(--color-surface)', color: 'var(--color-text)' }} />
        </div>
        <button onClick={apply} style={{ padding: '0.45rem 1rem', background: 'var(--color-primary)', color: 'white', border: 'none', borderRadius: 6, fontSize: '0.875rem', cursor: 'pointer' }}>
          Filtrar
        </button>
      </div>

      {isLoading && <p style={{ color: 'var(--color-text-muted)' }}>Carregando...</p>}
      {error && <p style={{ color: 'var(--color-error)' }}>Erro ao carregar relatório.</p>}

      {data && (
        <>
          {/* Summary cards */}
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(160px, 1fr))', gap: '1rem', marginBottom: '2rem' }}>
            <StatCard label="Total" value={data.total} />
            <StatCard label="Concluídos" value={data.completed} color="var(--color-success)" />
            <StatCard label="Cancelados" value={data.cancelled} color="var(--color-error)" />
            <StatCard label="Não compareceu" value={data.noShow} color="#f59e0b" />
            <StatCard label="Taxa cancelamento" value={`${data.cancellationRate}%`} />
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))', gap: '1.5rem' }}>
            {/* By professional */}
            {data.byProfessional.length > 0 && (
              <Section title="Por profissional">
                {data.byProfessional.map(p => (
                  <BarRow key={p.professionalName} label={p.professionalName} value={p.count} max={data.byProfessional[0].count} />
                ))}
              </Section>
            )}

            {/* By service */}
            {data.byService.length > 0 && (
              <Section title="Por serviço">
                {data.byService.map(s => (
                  <BarRow key={s.serviceName} label={s.serviceName} value={s.count} max={data.byService[0].count} />
                ))}
              </Section>
            )}

            {/* By status */}
            {data.byStatus.length > 0 && (
              <Section title="Por status">
                {data.byStatus.map(s => (
                  <BarRow key={s.status} label={STATUS_LABELS[s.status] ?? s.status} value={s.count} max={data.byStatus[0].count} />
                ))}
              </Section>
            )}
          </div>

          {/* Daily breakdown */}
          {data.byDay.length > 0 && (
            <section style={{ marginTop: '1.5rem' }}>
              <h2 style={{ fontSize: '0.9375rem', fontWeight: 600, marginBottom: '0.75rem' }}>Agendamentos por dia</h2>
              <div style={{ display: 'flex', alignItems: 'flex-end', gap: '4px', height: 80, overflowX: 'auto', padding: '0.5rem 0' }}>
                {data.byDay.map(d => {
                  const maxCount = Math.max(...data.byDay.map(x => x.count), 1);
                  const pct = Math.max((d.count / maxCount) * 100, 4);
                  return (
                    <div key={d.date} title={`${d.date}: ${d.count}`} style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', flex: '0 0 auto', minWidth: 20 }}>
                      <div style={{ width: 14, height: `${pct}%`, background: 'var(--color-primary)', borderRadius: '2px 2px 0 0', opacity: 0.75 }} />
                    </div>
                  );
                })}
              </div>
              <p style={{ fontSize: '0.75rem', color: 'var(--color-text-muted)', marginTop: '0.25rem' }}>
                {data.byDay[0]?.date} → {data.byDay[data.byDay.length - 1]?.date}
              </p>
            </section>
          )}

          {data.total === 0 && (
            <p style={{ color: 'var(--color-text-muted)', fontSize: '0.875rem', marginTop: '1rem' }}>
              Nenhum agendamento no período selecionado.
            </p>
          )}
        </>
      )}
    </AppShell>
  );
}

function StatCard({ label, value, color }: { label: string; value: number | string; color?: string }) {
  return (
    <div style={{ background: 'var(--color-surface)', border: '1px solid var(--color-border)', borderRadius: 8, padding: '1rem' }}>
      <p style={{ fontSize: '0.8125rem', color: 'var(--color-text-muted)', marginBottom: '0.25rem' }}>{label}</p>
      <p style={{ fontSize: '1.5rem', fontWeight: 700, color: color ?? 'var(--color-primary)' }}>{value}</p>
    </div>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div style={{ background: 'var(--color-surface)', border: '1px solid var(--color-border)', borderRadius: 8, padding: '1rem' }}>
      <h2 style={{ fontSize: '0.9375rem', fontWeight: 600, marginBottom: '0.75rem' }}>{title}</h2>
      <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>{children}</div>
    </div>
  );
}

function BarRow({ label, value, max }: { label: string; value: number; max: number }) {
  const pct = max > 0 ? Math.round((value / max) * 100) : 0;
  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.8125rem', marginBottom: '0.2rem' }}>
        <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', maxWidth: '80%' }}>{label}</span>
        <span style={{ color: 'var(--color-text-muted)', flexShrink: 0 }}>{value}</span>
      </div>
      <div style={{ background: 'var(--color-border)', borderRadius: 4, height: 6 }}>
        <div style={{ width: `${pct}%`, background: 'var(--color-primary)', borderRadius: 4, height: '100%', minWidth: value > 0 ? 4 : 0 }} />
      </div>
    </div>
  );
}
