import { type ReactNode } from 'react';
import { NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';

const NAV_ITEMS = [
  { to: '/app/dashboard', label: 'Dashboard' },
  { to: '/app/appointments', label: 'Agendamentos' },
  { to: '/app/professionals', label: 'Profissionais' },
  { to: '/app/services', label: 'Serviços' },
  { to: '/app/reports', label: 'Relatórios' },
];

export default function AppShell({ children }: { children: ReactNode }) {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = async () => {
    await logout();
    navigate('/login', { replace: true });
  };

  return (
    <div style={{ display: 'flex', minHeight: '100vh' }}>
      {/* Sidebar */}
      <aside style={{
        width: 220,
        background: 'var(--color-surface)',
        borderRight: '1px solid var(--color-border)',
        display: 'flex',
        flexDirection: 'column',
        padding: '1.5rem 0',
        flexShrink: 0,
      }}>
        <div style={{ padding: '0 1.25rem', marginBottom: '2rem' }}>
          <span style={{ fontWeight: 700, fontSize: '1.125rem', color: 'var(--color-primary)' }}>
            AgendaFlow
          </span>
          {user?.tenants[0] && (
            <p style={{ fontSize: '0.75rem', color: 'var(--color-text-muted)', marginTop: '0.25rem', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
              {user.tenants[0].tenantName}
            </p>
          )}
        </div>

        <nav style={{ flex: 1 }}>
          {NAV_ITEMS.map(item => (
            <NavLink
              key={item.to}
              to={item.to}
              style={({ isActive }) => ({
                display: 'block',
                padding: '0.625rem 1.25rem',
                fontSize: '0.875rem',
                fontWeight: isActive ? 600 : 400,
                color: isActive ? 'var(--color-primary)' : 'var(--color-text)',
                background: isActive ? 'color-mix(in srgb, var(--color-primary) 8%, transparent)' : 'none',
                textDecoration: 'none',
                borderRight: isActive ? '3px solid var(--color-primary)' : '3px solid transparent',
              })}
            >
              {item.label}
            </NavLink>
          ))}
        </nav>

        <div style={{ padding: '1.25rem', borderTop: '1px solid var(--color-border)' }}>
          <p style={{ fontSize: '0.8125rem', color: 'var(--color-text-muted)', marginBottom: '0.5rem', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
            {user?.fullName}
          </p>
          <button
            onClick={() => { void handleLogout(); }}
            style={{
              fontSize: '0.8125rem',
              color: 'var(--color-text-muted)',
              background: 'none',
              border: 'none',
              cursor: 'pointer',
              padding: 0,
            }}
          >
            Sair
          </button>
        </div>
      </aside>

      {/* Main content */}
      <main style={{ flex: 1, overflow: 'auto', padding: '2rem', background: 'var(--color-bg)' }}>
        {children}
      </main>
    </div>
  );
}
