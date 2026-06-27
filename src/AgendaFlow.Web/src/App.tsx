import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { lazy, Suspense, type ReactNode } from 'react';
import { AuthProvider, useAuth } from './hooks/useAuth';
import LoginPage from './pages/auth/LoginPage';
import './index.css';

const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: 1, staleTime: 30_000 } },
});

const RegisterPage = lazy(() => import('./pages/auth/RegisterPage'));
const ForgotPasswordPage = lazy(() => import('./pages/auth/ForgotPasswordPage'));
const DashboardPage = lazy(() => import('./pages/app/DashboardPage'));
const ServicesPage = lazy(() => import('./pages/app/ServicesPage'));
const AppointmentsPage = lazy(() => import('./pages/app/AppointmentsPage'));
const PublicBookingPage = lazy(() => import('./pages/public/PublicBookingPage'));
const BookingConfirmPage = lazy(() => import('./pages/public/BookingConfirmPage'));

function RequireAuth({ children }: { children: ReactNode }) {
  const { isAuthenticated, isLoading } = useAuth();
  if (isLoading) return <div style={{ padding: '2rem', textAlign: 'center' }}>Carregando...</div>;
  if (!isAuthenticated) return <Navigate to="/login" replace />;
  return <>{children}</>;
}

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <BrowserRouter>
          <Suspense fallback={<div style={{ padding: '2rem', textAlign: 'center' }}>Carregando...</div>}>
            <Routes>
              <Route path="/login" element={<LoginPage />} />
              <Route path="/register" element={<RegisterPage />} />
              <Route path="/forgot-password" element={<ForgotPasswordPage />} />
              <Route path="/agendar/:tenantSlug" element={<PublicBookingPage />} />
              <Route path="/agendamento/confirmar" element={<BookingConfirmPage purpose="confirm" />} />
              <Route path="/agendamento/cancelar" element={<BookingConfirmPage purpose="cancel" />} />
              <Route path="/app/dashboard" element={<RequireAuth><DashboardPage /></RequireAuth>} />
              <Route path="/app/services" element={<RequireAuth><ServicesPage /></RequireAuth>} />
              <Route path="/app/appointments" element={<RequireAuth><AppointmentsPage /></RequireAuth>} />
              <Route path="/" element={<Navigate to="/app/dashboard" replace />} />
              <Route path="*" element={<Navigate to="/app/dashboard" replace />} />
            </Routes>
          </Suspense>
        </BrowserRouter>
      </AuthProvider>
    </QueryClientProvider>
  );
}
