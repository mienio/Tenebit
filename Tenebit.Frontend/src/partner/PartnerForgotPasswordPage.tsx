import { FormEvent, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Button } from '../components/Button';
import { Field, TextInput } from '../components/FormFields';
import { requestAffiliatePasswordReset } from './partnerApi';
import { usePartnerLocale, type PartnerLocale } from './i18n';
import './partner.css';

const content: Record<PartnerLocale, {
  title: string; sentMessage: string; haveCode: string; email: string; submit: string; submitting: string; backToLogin: string;
}> = {
  en: {
    title: 'Reset password',
    sentMessage: 'If the account exists, we sent a reset code to that address.',
    haveCode: 'I already have a code',
    email: 'Email', submit: 'Send reset code', submitting: 'Sending…', backToLogin: 'Back to login',
  },
  pl: {
    title: 'Reset hasła',
    sentMessage: 'Jeśli konto istnieje, wysłaliśmy kod resetujący na podany adres.',
    haveCode: 'Mam już kod',
    email: 'E-mail', submit: 'Wyślij kod resetujący', submitting: 'Wysyłanie…', backToLogin: 'Wróć do logowania',
  },
};

export function PartnerForgotPasswordPage() {
  const navigate = useNavigate();
  const { path, locale } = usePartnerLocale();
  const t = content[locale];
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
        <h1 style={{ marginTop: 0, fontSize: 22 }}>{t.title}</h1>
        {sent ? (
          <>
            <p>{t.sentMessage}</p>
            <Button onClick={() => navigate(`${path('reset-password')}?email=${encodeURIComponent(email)}`)}>{t.haveCode}</Button>
          </>
        ) : (
          <form className="formGrid" onSubmit={handleSubmit}>
            <Field label={t.email}>
              <TextInput type="email" value={email} onChange={e => setEmail(e.target.value)} required autoFocus />
            </Field>
            <div className="formActions">
              <Button type="submit" disabled={submitting}>{submitting ? t.submitting : t.submit}</Button>
            </div>
          </form>
        )}
        <p style={{ marginTop: 16, fontSize: 13 }}><Link to={path('login')}>{t.backToLogin}</Link></p>
      </div>
    </div>
  );
}
