import { FormEvent, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { Button } from '../components/Button';
import { Field, TextInput } from '../components/FormFields';
import { verifyAffiliateEmail } from './partnerApi';
import { usePartnerLocale, type PartnerLocale } from './i18n';
import './partner.css';

const content: Record<PartnerLocale, {
  title: string; email: string; emailCode: string; error: string; submit: string; submitting: string; backToLogin: string;
}> = {
  en: {
    title: 'Confirm your email address', email: 'Email', emailCode: 'Code from the email',
    error: 'Could not confirm the email.',
    submit: 'Confirm', submitting: 'Confirming…', backToLogin: 'Back to login',
  },
  pl: {
    title: 'Potwierdź adres e-mail', email: 'E-mail', emailCode: 'Kod z e-maila',
    error: 'Nie udało się potwierdzić e-maila.',
    submit: 'Potwierdź', submitting: 'Potwierdzanie…', backToLogin: 'Wróć do logowania',
  },
};

export function PartnerVerifyEmailPage() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const { path, locale } = usePartnerLocale();
  const t = content[locale];
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
