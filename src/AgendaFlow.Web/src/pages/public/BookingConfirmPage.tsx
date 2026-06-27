import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { publicApi, ApiError } from '../../api/client';

type State = 'loading' | 'success' | 'error';

export default function BookingConfirmPage({ purpose }: { purpose: 'confirm' | 'cancel' }) {
  const [params] = useSearchParams();
  const [state, setState] = useState<State>('loading');
  const [message, setMessage] = useState('');

  useEffect(() => {
    const token = params.get('token');
    if (!token) { setState('error'); setMessage('Link inválido.'); return; }

    const action = purpose === 'confirm'
      ? publicApi.confirmByToken(token)
      : publicApi.cancelByToken(token);

    action
      .then(() => { setState('success'); setMessage(purpose === 'confirm' ? 'Agendamento confirmado!' : 'Agendamento cancelado.'); })
      .catch((err) => {
        setState('error');
        setMessage(err instanceof ApiError ? (err.detail ?? err.message) : 'Ocorreu um erro.');
      });
  }, []);

  const color = state === 'error' ? 'var(--color-error)' : state === 'success' ? 'var(--color-success)' : 'var(--color-text-muted)';

  return (
    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', minHeight: '100vh' }}>
      <div style={{ textAlign: 'center', padding: '2rem', maxWidth: 400 }}>
        {state === 'loading' && <p>Processando...</p>}
        {state !== 'loading' && <p style={{ color, fontSize: '1.125rem' }}>{message}</p>}
        {state !== 'loading' && <a href="/login" style={{ display: 'block', marginTop: '1rem', color: 'var(--color-primary)' }}>Ir para o login</a>}
      </div>
    </div>
  );
}
