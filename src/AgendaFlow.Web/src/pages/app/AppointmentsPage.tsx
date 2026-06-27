import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { appointmentsApi, type AppointmentDto } from '../../api/client';

const STATUS_LABELS: Record<string, string> = {
  PendingConfirmation: 'Pendente',
  Confirmed: 'Confirmado',
  CheckedIn: 'Presente',
  Completed: 'Concluído',
  Cancelled: 'Cancelado',
  NoShow: 'Não compareceu',
};

export default function AppointmentsPage() {
  const [page, setPage] = useState(1);
  const { data, isLoading, error } = useQuery({
    queryKey: ['appointments', page],
    queryFn: () => appointmentsApi.list({ page: String(page), pageSize: '25' }),
  });

  if (isLoading) return <div style={{ padding: '2rem' }}>Carregando agendamentos...</div>;
  if (error) return <div style={{ padding: '2rem', color: 'var(--color-error)' }}>Erro ao carregar agendamentos.</div>;

  return (
    <div style={{ padding: '2rem' }}>
      <h1>Agendamentos</h1>
      {!data?.items.length ? (
        <p style={{ color: 'var(--color-text-muted)', marginTop: '1rem' }}>Nenhum agendamento encontrado.</p>
      ) : (
        <>
          <table style={{ width: '100%', marginTop: '1rem', borderCollapse: 'collapse' }}>
            <thead>
              <tr>
                {['Data/Hora', 'Cliente', 'Serviço', 'Profissional', 'Status'].map(h => (
                  <th key={h} style={{ textAlign: 'left', padding: '0.5rem', borderBottom: '1px solid var(--color-border)' }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {data.items.map((a: AppointmentDto) => (
                <tr key={a.id}>
                  <td style={{ padding: '0.5rem' }}>
                    {new Date(a.startAtUtc).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' })}
                  </td>
                  <td style={{ padding: '0.5rem' }}>{a.customerName}</td>
                  <td style={{ padding: '0.5rem' }}>{a.serviceName}</td>
                  <td style={{ padding: '0.5rem' }}>{a.professionalName}</td>
                  <td style={{ padding: '0.5rem' }}>{STATUS_LABELS[a.status] ?? a.status}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <div style={{ marginTop: '1rem', display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
            <button onClick={() => setPage(p => p - 1)} disabled={page === 1}>Anterior</button>
            <span>Página {page} de {data.totalPages}</span>
            <button onClick={() => setPage(p => p + 1)} disabled={page >= data.totalPages}>Próxima</button>
          </div>
        </>
      )}
    </div>
  );
}
