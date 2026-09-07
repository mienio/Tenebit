import { FormEvent, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { Button } from '../components/Button';
import { Field, TextInput } from '../components/FormFields';
import { confirmAffiliatePasswordReset } from './partnerApi';
import { usePartnerLocale, type PartnerLocale } from './i18n';
import './partner.css';

const content: Record<PartnerLocale, {
  title: string; email: string; emailCode: string; newPassword: string; newPasswordInfo: string;
  error: string; submit: string; submitting: string; backToLogin: string;
}> = {
  en: {
    title: 'Set a new password', email: 'Email', emailCode: 'Code from the email',
    newPassword: 'New password', newPasswordInfo: 'At least 8 characters.',
    error: 'Could not reset the password.',
    submit: 'Set new password', submitting: 'Saving…', backToLogin: 'Back to login',
  },
  pl: {
    title: 'Ustaw nowe hasło', email: 'E-mail', emailCode: 'Kod z e-maila',
    newPassword: 'Nowe hasło', newPasswordInfo: 'Co najmniej 8 znaków.',
    error: 'Nie udało się zresetować hasła.',
    submit: 'Ustaw nowe hasło', submitting: 'Zapisywanie…', backToLogin: 'Wróć do logowania',
  },
};

export function PartnerResetPasswordPage() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const { path, locale } = usePartnerLocale();
  const t = content[locale];
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
      navigate(path('login'), { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : t.error);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="partnerAuthPage">
      <div className="partnerAuthPage__panel card">
        <h1 style={{ marginTop: 0, fontSize: 22 }}>{t.title}</h1>
        <form className="formGrid" onSubmit={handleSubmit}>
          <Field label={t.email}>
            <TextInput type="email" value={email} onChange={e => setEmail(e.target.value)} required autoFocus />
          </Field>
          <Field label={t.emailCode}>
            <TextInput inputMode="numeric" maxLength={6} minLength={6} value={code} onChange={e => setCode(e.target.value)} required />
          </Field>
          <Field label={t.newPassword} info={t.newPasswordInfo}>
            <TextInput type="password" value={newPassword} onChange={e => setNewPassword(e.target.value)} required minLength={8} />
          </Field>
          {error ? <p className="formMessage formMessage--error">{error}</p> : null}
          <div className="formActions">
            <Button type="submit" disabled={submitting}>{submitting ? t.submitting : t.submit}</Button>
          </div>
        </form>
        <p style={{ marginTop: 16, fontSize: 13 }}><Link to={path('login')}>{t.backToLogin}</Link></p>
      </div>
    </div>
  );
}
