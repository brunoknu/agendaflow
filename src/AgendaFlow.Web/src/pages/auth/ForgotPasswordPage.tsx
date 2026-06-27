import { useState } from 'react';
import { Link } from 'react-router-dom';
import { authApi } from '../../api/client';
import styles from './Auth.module.css';

export default function ForgotPasswordPage() {
  const [email, setEmail] = useState('');
  const [sent, setSent] = useState(false);
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    try { await authApi.forgotPassword(email); } finally { setLoading(false); setSent(true); }
  };

  if (sent) return (
    <div className={styles.container}>
      <div className={styles.card}>
        <p>Se o e-mail estiver cadastrado, você receberá as instruções em breve.</p>
        <div className={styles.links}><Link to="/login">Voltar ao login</Link></div>
      </div>
    </div>
  );

  return (
    <div className={styles.container}>
      <div className={styles.card}>
        <h2 className={styles.subtitle}>Recuperar senha</h2>
        <form onSubmit={handleSubmit}>
          <div className={styles.field}>
            <label htmlFor="email">E-mail</label>
            <input id="email" type="email" value={email} onChange={e => setEmail(e.target.value)} required />
          </div>
          <button type="submit" disabled={loading} className={styles.submitButton}>
            {loading ? 'Enviando...' : 'Enviar instruções'}
          </button>
        </form>
        <div className={styles.links}><Link to="/login">Voltar ao login</Link></div>
      </div>
    </div>
  );
}
