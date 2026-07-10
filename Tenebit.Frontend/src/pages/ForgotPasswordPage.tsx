import { KeyRound } from 'lucide-react';
import { FormEvent, useState } from 'react';
import { Link } from 'react-router-dom';
import { apiRequest } from '../api/apiClient';
import { BackButton } from '../components/BackButton';
import { Button } from '../components/Button';
import { Field, TextInput } from '../components/FormFields';
import { useI18n } from '../i18n/I18nProvider';
import { LanguageSwitcher } from '../i18n/LanguageSwitcher';

export function ForgotPasswordPage() {
  const { t } = useI18n();
  const [submitting, setSubmitting] = useState(false);
  const [sent, setSent] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    setError(null);
    setSubmitting(true);
    try {
      await apiRequest('/api/auth/password/forgot', { method: 'POST', body: JSON.stringify({ email: String(form.get('email') ?? '') }) });
      setSent(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Nie udało się wysłać wiadomości.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <main className="authShell">
      <section className="authCard">
        <div className="authTop">
          <BackButton to="/login" />
          <LanguageSwitcher />
        </div>
        <h1>{t('auth.forgotTitle')}</h1>
        {sent ? (
          <p className="formMessage formMessage--success">{t('auth.forgotSent')}</p>
        ) : (
          <form className="formGrid" onSubmit={handleSubmit}>
            <Field label={t('auth.emailLabel')}><TextInput name="email" type="email" required autoFocus /></Field>
            {error ? <p className="formMessage formMessage--error">{error}</p> : null}
            <Button disabled={submitting} icon={<KeyRound size={16} />}>{submitting ? t('auth.forgotLoading') : t('auth.forgotButton')}</Button>
          </form>
        )}
        <p className="authFooter"><Link to="/login">{t('auth.backToLogin')}</Link></p>
      </section>
    </main>
  );
}
