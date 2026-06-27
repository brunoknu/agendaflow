import { useQuery } from '@tanstack/react-query';
import { servicesApi, type ServiceDto } from '../../api/client';

export default function ServicesPage() {
  const { data: services, isLoading, error } = useQuery({
    queryKey: ['services'],
    queryFn: () => servicesApi.list(),
  });

  if (isLoading) return <div style={{ padding: '2rem' }}>Carregando serviços...</div>;
  if (error) return <div style={{ padding: '2rem', color: 'var(--color-error)' }}>Erro ao carregar serviços.</div>;

  return (
    <div style={{ padding: '2rem' }}>
      <h1>Serviços</h1>
      {services?.length === 0 ? (
        <p style={{ color: 'var(--color-text-muted)', marginTop: '1rem' }}>Nenhum serviço cadastrado.</p>
      ) : (
        <table style={{ width: '100%', marginTop: '1rem', borderCollapse: 'collapse' }}>
          <thead>
            <tr>
              {['Nome', 'Duração', 'Preço', 'Status'].map(h => (
                <th key={h} style={{ textAlign: 'left', padding: '0.5rem', borderBottom: '1px solid var(--color-border)' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {services?.map((s: ServiceDto) => (
              <tr key={s.id}>
                <td style={{ padding: '0.5rem' }}>{s.name}</td>
                <td style={{ padding: '0.5rem' }}>{s.durationMinutes} min</td>
                <td style={{ padding: '0.5rem' }}>{s.price.toLocaleString('pt-BR', { style: 'currency', currency: s.currency })}</td>
                <td style={{ padding: '0.5rem', color: s.isActive ? 'var(--color-success)' : 'var(--color-text-muted)' }}>
                  {s.isActive ? 'Ativo' : 'Inativo'}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
