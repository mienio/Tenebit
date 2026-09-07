import { FormEvent, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Button } from '../components/Button';
import { Field, TextInput } from '../components/FormFields';
import { requestAffiliatePasswordReset } from './partnerApi';
import './partner.css';

export function PartnerForgotPasswordPage() {
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [sent, setSent] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setSubmitting(true);
    try {
      await requestAffiliatePasswordReset(email);
    } finally {
      setSubmitting(false);
      setSent(true);
    }
  }

  return (
    <div className="partnerAuthPage">
      <div className="partnerAuthPage__panel card">
        <h1 style={{ marginTop: 0, fontSize: 22 }}>Reset hasła</h1>
        {sent ? (
          <>
            <p>Jeśli konto istnieje, wysłaliśmy kod resetujący na podany adres.</p>
            <Button onClick={() => navigate(`/partner/reset-password?email=${encodeURIComponent(email)}`)}>Mam już kod</Button>
          </>
        ) : (
          <form className="formGrid" onSubmit={handleSubmit}>
            <Field label="E-mail">
              <TextInput type="email" value={email} onChange={e => setEmail(e.target.value)} required autoFocus />
            </Field>
            <div className="formActions">
              <Button type="submit" disabled={submitting}>{submitting ? 'Wysyłanie…' : 'Wyślij kod resetujący'}</Button>
            </div>
          </form>
        )}
        <p style={{ marginTop: 16, fontSize: 13 }}><Link to="/partner/login">Wróć do logowania</Link></p>
      </div>
    </div>
  );
}
