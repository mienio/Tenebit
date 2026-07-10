import { LogIn } from 'lucide-react';
import { FormEvent, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Button } from '../components/Button';
import { Field, TextInput } from '../components/FormFields';
import { SocialLoginButtons } from '../components/SocialLoginButtons';
import { useAuth } from '../auth/AuthProvider';
import { useI18n } from '../i18n/I18nProvider';
import { LanguageSwitcher } from '../i18n/LanguageSwitcher';

export function LoginPage() {
  const auth = useAuth();
  const navigate = useNavigate();
  const { t } = useI18n();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    setError(null);
    setSubmitting(true);
    try {
      await auth.login(String(form.get('email') ?? ''), String(form.get('password') ?? ''));
      navigate('/dashboard', { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Nie udało się zalogować.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <main className="authShell">
      <section className="authCard">
        <div className="authTop">
          <div className="authIcon"><LogIn size={24} /></div>
          <LanguageSwitcher />
        </div>
        <h1>{t('auth.loginTitle')}</h1>
        <SocialLoginButtons returnUrl="/dashboard" />
        <form className="formGrid" onSubmit={handleSubmit}>
          <Field label={t('auth.emailLabel')}><TextInput name="email" type="email" required autoFocus /></Field>
          <Field label={t('auth.passwordLabel')}><TextInput name="password" type="password" required /></Field>
          {error ? <p className="formMessage formMessage--error">{error}</p> : null}
          <Button disabled={submitting} icon={<LogIn size={16} />}>{submitting ? t('auth.loginLoading') : t('auth.loginButton')}</Button>
        </form>
        <p className="authFooter">{t('auth.noAccount')} <Link to="/register">{t('auth.registerLink')}</Link></p>
      </section>
    </main>
  );
}
