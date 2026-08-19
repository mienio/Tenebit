import { KeyRound } from 'lucide-react';
import { FormEvent, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { apiRequest } from '../api/apiClient';
import { BackButton } from '../components/BackButton';
import { Button } from '../components/Button';
import { Field, TextInput } from '../components/FormFields';
import { PublicFooter } from '../components/PublicFooter';
import { useI18n } from '../i18n/I18nProvider';
import { LanguageSwitcher } from '../i18n/LanguageSwitcher';

export function ForgotPasswordPage() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const normalizedEmail = email.trim();
    setError(null);
    setSubmitting(true);
    try {
      await apiRequest('/api/auth/password/forgot', {
        method: 'POST',
        body: JSON.stringify({ email: normalizedEmail })
      });
      navigate(`/reset-password#email=${encodeURIComponent(normalizedEmail)}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : t('auth.forgotPasswordFailed'));
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
        <p className="authCard__hint">{t('auth.forgotLead')}</p>
        <form className="formGrid" onSubmit={handleSubmit}>
          <Field label={t('auth.emailLabel')}>
            <TextInput name="email" type="email" required autoFocus autoComplete="email" value={email} onChange={event => setEmail(event.target.value)} />
          </Field>
          {error ? <p className="formMessage formMessage--error">{error}</p> : null}
          <Button disabled={submitting} icon={<KeyRound size={16} />}>{submitting ? t('auth.forgotLoading') : t('auth.forgotButton')}</Button>
        </form>
        <p className="authFooter"><Link to="/login">{t('auth.backToLogin')}</Link></p>
      </section>
      <PublicFooter compact />
    </main>
  );
}
