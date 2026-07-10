import { KeyRound } from 'lucide-react';
import { FormEvent, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { apiRequest } from '../api/apiClient';
import { Button } from '../components/Button';
import { Field, TextInput } from '../components/FormFields';
import { useI18n } from '../i18n/I18nProvider';
import { LanguageSwitcher } from '../i18n/LanguageSwitcher';

export function ResetPasswordPage() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token') ?? '';
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    setError(null);
    setSubmitting(true);
    try {
      await apiRequest('/api/auth/password/reset', { method: 'POST', body: JSON.stringify({ token, newPassword: String(form.get('password') ?? '') }) });
      navigate('/login', { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Nie udało się zresetować hasła.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <main className="authShell">
      <section className="authCard">
        <div className="authTop">
          <div className="authIcon"><KeyRound size={24} /></div>
          <LanguageSwitcher />
        </div>
        <h1>{t('auth.resetTitle')}</h1>
        {token ? (
          <form className="formGrid" onSubmit={handleSubmit}>
            <Field label={t('auth.newPasswordLabel')} info={t('auth.passwordHint')}><TextInput name="password" type="password" minLength={8} required autoFocus /></Field>
            {error ? <p className="formMessage formMessage--error">{error}</p> : null}
            <Button disabled={submitting} icon={<KeyRound size={16} />}>{submitting ? t('auth.resetLoading') : t('auth.resetButton')}</Button>
          </form>
        ) : (
          <p className="formMessage formMessage--error">{t('auth.resetTokenMissing')}</p>
        )}
        <p className="authFooter"><Link to="/login">{t('auth.backToLogin')}</Link></p>
      </section>
    </main>
  );
}
