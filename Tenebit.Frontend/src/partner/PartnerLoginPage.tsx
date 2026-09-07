import { FormEvent, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Button } from '../components/Button';
import { Field, TextInput } from '../components/FormFields';
import { usePartnerAuth } from './PartnerAuthProvider';
import './partner.css';

export function PartnerLoginPage() {
  const navigate = useNavigate();
  const { login } = usePartnerAuth();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await login(email, password);
      navigate('/partner/dashboard', { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Nie udało się zalogować.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="partnerAuthPage">
      <div className="partnerAuthPage__panel card">
        <h1 style={{ marginTop: 0, fontSize: 22 }}>Panel partnera Tenebit</h1>
        <form className="formGrid" onSubmit={handleSubmit}>
          <Field label="E-mail">
            <TextInput type="email" value={email} onChange={e => setEmail(e.target.value)} required autoFocus />
          </Field>
          <Field label="Hasło">
            <TextInput type="password" value={password} onChange={e => setPassword(e.target.value)} required />
          </Field>
          {error ? <p className="formMessage formMessage--error">{error}</p> : null}
          <div className="formActions">
            <Button type="submit" disabled={submitting}>{submitting ? 'Logowanie…' : 'Zaloguj się'}</Button>
          </div>
        </form>
        <p style={{ marginTop: 16, fontSize: 13 }}>
          <Link to="/partner/forgot-password">Nie pamiętasz hasła?</Link>
        </p>
        <p style={{ fontSize: 13 }}>
          Nie masz jeszcze konta? <Link to="/partner/register">Zarejestruj się</Link>
        </p>
      </div>
    </div>
  );
}
