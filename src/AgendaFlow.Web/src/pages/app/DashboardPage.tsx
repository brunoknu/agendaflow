import { useAuth } from '../../hooks/useAuth';

export default function DashboardPage() {
  const { user, logout } = useAuth();

  return (
    <div style={{ padding: '2rem' }}>
      <header style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '2rem' }}>
        <h1 style={{ color: 'var(--color-primary)' }}>AgendaFlow</h1>
        <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
          <span style={{ color: 'var(--color-text-muted)', fontSize: '0.875rem' }}>{user?.fullName}</span>
          <button
            onClick={() => { void logout(); }}
            style={{ padding: '0.375rem 0.75rem', cursor: 'pointer', border: '1px solid var(--color-border)', borderRadius: 6, background: 'none' }}
          >
            Sair
          </button>
        </div>
      </header>

      <nav style={{ display: 'flex', gap: '1.5rem', marginBottom: '2rem', borderBottom: '1px solid var(--color-border)', paddingBottom: '1rem' }}>
        <a href="/app/services" style={{ color: 'var(--color-primary)', textDecoration: 'none', fontSize: '0.875rem', fontWeight: 500 }}>Serviços</a>
        <a href="/app/appointments" style={{ color: 'var(--color-primary)', textDecoration: 'none', fontSize: '0.875rem', fontWeight: 500 }}>Agendamentos</a>
      </nav>

      <section>
        <h2 style={{ fontSize: '1.125rem', marginBottom: '0.5rem' }}>Bem-vindo</h2>
        <p style={{ color: 'var(--color-text-muted)', fontSize: '0.875rem' }}>
          Use o menu acima para gerenciar seus serviços e agendamentos.
        </p>
      </section>

      {user?.tenants.length === 0 && (
        <div style={{ marginTop: '2rem', padding: '1rem', background: 'var(--color-surface)', border: '1px solid var(--color-border)', borderRadius: 8 }}>
          <p style={{ fontSize: '0.875rem' }}>Você ainda não possui uma empresa cadastrada.</p>
          <a href="/onboarding" style={{ color: 'var(--color-primary)', fontSize: '0.875rem' }}>Criar empresa</a>
        </div>
      )}
    </div>
  );
}
