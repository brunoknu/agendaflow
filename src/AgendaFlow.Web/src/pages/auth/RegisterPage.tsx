import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { authApi, ApiError } from '../../api/client';
import styles from './Auth.module.css';

const schema = z.object({
  fullName: z.string().min(2, 'Nome deve ter ao menos 2 caracteres'),
  email: z.string().email('E-mail inválido'),
  password: z.string().min(8, 'Senha deve ter ao menos 8 caracteres'),
});

type FormData = z.infer<typeof schema>;

export default function RegisterPage() {
  const [serverError, setServerError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  const { register, handleSubmit, formState: { errors, isSubmitting } } =
    useForm<FormData>({ resolver: zodResolver(schema) });

  const onSubmit = async (data: FormData) => {
    setServerError(null);
    try {
      await authApi.register(data as { email: string; password: string; fullName: string });
      setSuccess(true);
    } catch (err) {
      if (err instanceof ApiError) setServerError(err.detail ?? err.message);
      else setServerError('Ocorreu um erro. Tente novamente.');
    }
  };

  if (success) {
    return (
      <div className={styles.container}>
        <div className={styles.card}>
          <h1 className={styles.title}>AgendaFlow</h1>
          <div style={{ textAlign: 'center', padding: '1rem 0' }}>
            <p>Conta criada! Verifique seu e-mail para confirmar.</p>
            <Link to="/login" style={{ marginTop: '1rem', display: 'inline-block' }}>
              Ir para o login
            </Link>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <div className={styles.card}>
        <h1 className={styles.title}>AgendaFlow</h1>
        <h2 className={styles.subtitle}>Criar conta</h2>
        <form onSubmit={handleSubmit(onSubmit)} noValidate>
          <div className={styles.field}>
            <label htmlFor="fullName">Nome completo</label>
            <input id="fullName" type="text" autoComplete="name" aria-invalid={!!errors.fullName} {...register('fullName')} />
            {errors.fullName && <span className={styles.error} role="alert">{errors.fullName.message}</span>}
          </div>
          <div className={styles.field}>
            <label htmlFor="email">E-mail</label>
            <input id="email" type="email" autoComplete="email" aria-invalid={!!errors.email} {...register('email')} />
            {errors.email && <span className={styles.error} role="alert">{errors.email.message}</span>}
          </div>
          <div className={styles.field}>
            <label htmlFor="password">Senha</label>
            <input id="password" type="password" autoComplete="new-password" aria-invalid={!!errors.password} {...register('password')} />
            {errors.password && <span className={styles.error} role="alert">{errors.password.message}</span>}
          </div>
          {serverError && <div className={styles.serverError} role="alert">{serverError}</div>}
          <button type="submit" disabled={isSubmitting} className={styles.submitButton}>
            {isSubmitting ? 'Criando...' : 'Criar conta'}
          </button>
        </form>
        <div className={styles.links}>
          <Link to="/login">Já tenho conta</Link>
        </div>
      </div>
    </div>
  );
}
