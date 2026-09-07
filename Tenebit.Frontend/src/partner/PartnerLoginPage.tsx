import { FormEvent, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Button } from '../components/Button';
import { Field, TextInput } from '../components/FormFields';
import { usePartnerAuth } from './PartnerAuthProvider';
import { PartnerLanguageSwitch, usePartnerLocale, type PartnerLocale } from './i18n';
import './partner.css';

const content: Record<PartnerLocale, {
  title: string; email: string; password: string; genericError: string;
  submit: string; submitting: string; forgot: string; noAccount: string; register: string;
}> = {
  en: {
    title: 'Tenebit partner panel', email: 'Email', password: 'Password',
    genericError: 'Could not log in.',
    submit: 'Log in', submitting: 'Logging in…',
    forgot: 'Forgot your password?', noAccount: 'Don’t have an account yet?', register: 'Sign up',
  },
  pl: {
    title: 'Panel partnera Tenebit', email: 'E-mail', password: 'Hasło',
    genericError: 'Nie udało się zalogować.',
    submit: 'Zaloguj się', submitting: 'Logowanie…',
    forgot: 'Nie pamiętasz hasła?', noAccount: 'Nie masz jeszcze konta?', register: 'Zarejestruj się',
  },
};

export function PartnerLoginPage() {
  const navigate = useNavigate();
  const { login } = usePartnerAuth();
  const { path, locale } = usePartnerLocale();
  const t = content[locale];
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
      navigate(path('dashboard'), { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : t.genericError);
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
          <Field label={t.password}>
            <TextInput type="password" value={password} onChange={e => setPassword(e.target.value)} required />
          </Field>
          {error ? <p className="formMessage formMessage--error">{error}</p> : null}
          <div className="formActions">
            <Button type="submit" disabled={submitting}>{submitting ? t.submitting : t.submit}</Button>
          </div>
        </form>
        <p style={{ marginTop: 16, fontSize: 13 }}>
          <Link to={path('forgot-password')}>{t.forgot}</Link>
        </p>
        <p style={{ fontSize: 13 }}>
          {t.noAccount} <Link to={path('register')}>{t.register}</Link>
        </p>
        <p style={{ marginTop: 16, fontSize: 13 }}><PartnerLanguageSwitch /></p>
      </div>
    </div>
  );
}
