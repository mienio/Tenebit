import { FormEvent, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { Button } from '../components/Button';
import { Field, TextInput } from '../components/FormFields';
import { verifyAffiliateEmail } from './partnerApi';
import './partner.css';

export function PartnerVerifyEmailPage() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const [email, setEmail] = useState(params.get('email') ?? '');
  const [code, setCode] = useState(params.get('code') ?? '');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await verifyAffiliateEmail(email, code);
      navigate('/partner/login', { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Nie udało się potwierdzić e-maila.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="partnerAuthPage">
      <div className="partnerAuthPage__panel card">
        <h1 style={{ marginTop: 0, fontSize: 22 }}>Potwierdź adres e-mail</h1>
        <form className="formGrid" onSubmit={handleSubmit}>
          <Field label="E-mail">
            <TextInput type="email" value={email} onChange={e => setEmail(e.target.value)} required autoFocus />
          </Field>
          <Field label="Kod z e-maila">
            <TextInput inputMode="numeric" maxLength={6} minLength={6} value={code} onChange={e => setCode(e.target.value)} required />
          </Field>
          {error ? <p className="formMessage formMessage--error">{error}</p> : null}
          <div className="formActions">
            <Button type="submit" disabled={submitting}>{submitting ? 'Potwierdzanie…' : 'Potwierdź'}</Button>
          </div>
        </form>
        <p style={{ marginTop: 16, fontSize: 13 }}><Link to="/partner/login">Wróć do logowania</Link></p>
      </div>
    </div>
  );
}
