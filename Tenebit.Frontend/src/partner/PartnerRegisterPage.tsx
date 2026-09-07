import { FormEvent, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Button } from '../components/Button';
import { Field, TextInput } from '../components/FormFields';
import { registerAffiliate } from './partnerApi';
import './partner.css';

export function PartnerRegisterPage() {
  const navigate = useNavigate();
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [revolutTag, setRevolutTag] = useState('');
  const [acceptTerms, setAcceptTerms] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [submitted, setSubmitted] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!acceptTerms) { setError('Akceptacja regulaminu programu partnerskiego jest wymagana.'); return; }
    setError(null);
    setSubmitting(true);
    try {
      await registerAffiliate({
        email, password, firstName, lastName,
        revolutTag: revolutTag.trim() || null,
        acceptTerms,
      });
      setSubmitted(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Nie udało się zarejestrować.');
    } finally {
      setSubmitting(false);
    }
  }

  if (submitted) {
    return (
      <div className="partnerAuthPage">
        <div className="partnerAuthPage__panel card">
          <h1 style={{ marginTop: 0, fontSize: 22 }}>Sprawdź skrzynkę e-mail</h1>
          <p>Wysłaliśmy kod weryfikacyjny na adres {email}. Potwierdź e-mail, aby dokończyć rejestrację.</p>
          <Button onClick={() => navigate(`/partner/verify-email?email=${encodeURIComponent(email)}`)}>Mam już kod</Button>
        </div>
      </div>
    );
  }

  return (
    <div className="partnerAuthPage">
      <div className="partnerAuthPage__panel card">
        <h1 style={{ marginTop: 0, fontSize: 22 }}>Dołącz do programu partnerskiego</h1>
        <p className="adminMuted" style={{ marginBottom: 20 }}>
          Po rejestracji Twoje konto czeka na zatwierdzenie przez Tenebit - to standardowy krok
          zabezpieczający przed nadużyciami, zwykle zajmuje krótko.
        </p>
        <form className="formGrid" onSubmit={handleSubmit}>
          <Field label="Imię">
            <TextInput value={firstName} onChange={e => setFirstName(e.target.value)} required autoFocus />
          </Field>
          <Field label="Nazwisko">
            <TextInput value={lastName} onChange={e => setLastName(e.target.value)} required />
          </Field>
          <Field label="E-mail">
            <TextInput type="email" value={email} onChange={e => setEmail(e.target.value)} required />
          </Field>
          <Field label="Hasło" info="Co najmniej 8 znaków.">
            <TextInput type="password" value={password} onChange={e => setPassword(e.target.value)} required minLength={8} />
          </Field>
          <Field label="Revtag Revolut (opcjonalnie)" info="Identyfikator Twojego konta Revolut w formacie @nazwa - tam trafią wypłaty prowizji. Możesz go uzupełnić później w profilu.">
            <TextInput value={revolutTag} onChange={e => setRevolutTag(e.target.value)} placeholder="@twoja-nazwa" />
          </Field>
          <label style={{ display: 'flex', gap: 8, alignItems: 'flex-start', fontSize: 13.5 }}>
            <input type="checkbox" checked={acceptTerms} onChange={e => setAcceptTerms(e.target.checked)} style={{ marginTop: 3 }} required />
            <span>
              Akceptuję <Link to="/partner/terms" target="_blank" rel="noreferrer">regulamin programu partnerskiego</Link>,
              w tym wymóg posiadania konta Revolut do odbioru wypłat.
            </span>
          </label>
          {error ? <p className="formMessage formMessage--error">{error}</p> : null}
          <div className="formActions">
            <Button type="submit" disabled={submitting}>{submitting ? 'Rejestracja…' : 'Zarejestruj się'}</Button>
          </div>
        </form>
        <p style={{ marginTop: 16, fontSize: 13 }}>
          Masz już konto? <Link to="/partner/login">Zaloguj się</Link>
        </p>
      </div>
    </div>
  );
}
