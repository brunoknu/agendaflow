import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { authApi, ApiError } from '../../api/client';
import styles from './Auth.module.css';

const schema = z.object({
  password: z.string().min(8, 'Senha deve ter ao menos 8 caracteres'),
  confirm: z.string(),
}).refine(d => d.password === d.confirm, {
  message: 'As senhas não conferem',
  path: ['confirm'],
});

type FormData = z.infer<typeof schema>;

export default function ResetPasswordPage() {
  const [params] = useSearchParams();
  const [serverError, setServerError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  const email = params.get('email') ?? '';
  const token = params.get('token') ?? '';

  const { register, handleSubmit, formState: { errors, isSubmitting } } =
    useForm<FormData>({ resolver: zodResolver(schema) });

  const onSubmit = async (data: FormData) => {
    setServerError(null);
    try {
      await authApi.resetPassword({ email, token, newPassword: data.password });
      setSuccess(true);
    } catch (err) {
      if (err instanceof ApiError) setServerError(err.detail ?? err.message);
      else setServerError('Ocorreu um erro. Tente novamente.');
    }
  };

  if (!email || !token) {
    return (
      <div className={styles.container}>
        <div className={styles.card}>
          <h1 className={styles.title}>AgendaFlow</h1>
          <p style={{ color: 'var(--color-error)' }}>Link inválido ou expirado.</p>
          <Link to="/forgot-password" style={{ display: 'block', marginTop: '1rem' }}>Solicitar novo link</Link>
        </div>
      </div>
    );
  }

  if (success) {
    return (
      <div className={styles.container}>
        <div className={styles.card}>
          <h1 className={styles.title}>AgendaFlow</h1>
          <p style={{ color: 'var(--color-success)' }}>Senha redefinida com sucesso.</p>
          <Link to="/login" style={{ display: 'block', marginTop: '1rem' }}>Ir para o login</Link>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <div className={styles.card}>
        <h1 className={styles.title}>AgendaFlow</h1>
        <h2 className={styles.subtitle}>Nova senha</h2>
        <form onSubmit={handleSubmit(onSubmit)} noValidate>
          <div className={styles.field}>
            <label htmlFor="password">Nova senha</label>
            <input id="password" type="password" autoComplete="new-password" aria-invalid={!!errors.password} {...register('password')} />
            {errors.password && <span className={styles.error} role="alert">{errors.password.message}</span>}
          </div>
          <div className={styles.field}>
            <label htmlFor="confirm">Confirmar senha</label>
            <input id="confirm" type="password" autoComplete="new-password" aria-invalid={!!errors.confirm} {...register('confirm')} />
            {errors.confirm && <span className={styles.error} role="alert">{errors.confirm.message}</span>}
          </div>
          {serverError && <div className={styles.serverError} role="alert">{serverError}</div>}
          <button type="submit" disabled={isSubmitting} className={styles.submitButton}>
            {isSubmitting ? 'Salvando...' : 'Redefinir senha'}
          </button>
        </form>
      </div>
    </div>
  );
}
