import { FormEvent, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { Button } from '../components/Button';
import { Field, TextInput } from '../components/FormFields';
import { confirmAffiliatePasswordReset } from './partnerApi';
import './partner.css';

export function PartnerResetPasswordPage() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const [email, setEmail] = useState(params.get('email') ?? '');
  const [code, setCode] = useState(params.get('code') ?? '');
  const [newPassword, setNewPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await confirmAffiliatePasswordReset(email, code, newPassword);
      navigate('/partner/login', { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Nie udało się zresetować hasła.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="partnerAuthPage">
      <div className="partnerAuthPage__panel card">
        <h1 style={{ marginTop: 0, fontSize: 22 }}>Ustaw nowe hasło</h1>
        <form className="formGrid" onSubmit={handleSubmit}>
          <Field label="E-mail">
            <TextInput type="email" value={email} onChange={e => setEmail(e.target.value)} required autoFocus />
          </Field>
          <Field label="Kod z e-maila">
            <TextInput inputMode="numeric" maxLength={6} minLength={6} value={code} onChange={e => setCode(e.target.value)} required />
          </Field>
          <Field label="Nowe hasło" info="Co najmniej 8 znaków.">
            <TextInput type="password" value={newPassword} onChange={e => setNewPassword(e.target.value)} required minLength={8} />
          </Field>
          {error ? <p className="formMessage formMessage--error">{error}</p> : null}
          <div className="formActions">
            <Button type="submit" disabled={submitting}>{submitting ? 'Zapisywanie…' : 'Ustaw nowe hasło'}</Button>
          </div>
        </form>
        <p style={{ marginTop: 16, fontSize: 13 }}><Link to="/partner/login">Wróć do logowania</Link></p>
      </div>
    </div>
  );
}
