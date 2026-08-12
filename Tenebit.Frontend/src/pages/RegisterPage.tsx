import { Rocket } from 'lucide-react';
import { FormEvent, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { BackButton } from '../components/BackButton';
import { Button } from '../components/Button';
import { Field, SelectInput, TextInput } from '../components/FormFields';
import { PasswordStrengthMeter } from '../components/PasswordStrengthMeter';
import { SocialLoginButtons } from '../components/SocialLoginButtons';
import { useAuth } from '../auth/AuthProvider';
import { useI18n } from '../i18n/I18nProvider';
import { LanguageSwitcher } from '../i18n/LanguageSwitcher';

const currencies = ['PLN', 'EUR', 'USD', 'GBP', 'CHF', 'CZK', 'UAH'];

export function RegisterPage() {
  const auth = useAuth();
  const navigate = useNavigate();
  const { t, language } = useI18n();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [password, setPassword] = useState('');

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    setError(null);
    setSubmitting(true);
    try {
      await auth.register(
        String(form.get('organizationName') ?? ''),
        String(form.get('displayName') ?? ''),
        String(form.get('email') ?? ''),
        String(form.get('password') ?? ''),
        String(form.get('currency') ?? 'PLN'),
        language
      );
      navigate('/dashboard', { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : t('auth.registerFailed'));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <main className="authShell">
      <section className="authCard">
        <div className="authTop">
          <BackButton to="/" />
          <LanguageSwitcher />
        </div>
        <h1>{t('auth.registerTitle')}</h1>
        <SocialLoginButtons returnUrl="/dashboard" />
        <form className="formGrid" onSubmit={handleSubmit}>
          <Field label={t('auth.orgNameLabel')}><TextInput name="organizationName" required autoFocus /></Field>
          <Field label={t('auth.displayNameLabel')}><TextInput name="displayName" required /></Field>
          <Field label={t('auth.emailLabel')}><TextInput name="email" type="email" required /></Field>
          <Field label={t('auth.currencyLabel')}>
            <SelectInput name="currency" defaultValue="PLN">
              {currencies.map(code => <option key={code} value={code}>{code}</option>)}
            </SelectInput>
          </Field>
          <Field label={t('auth.passwordLabel')} info={t('auth.passwordHint')}><TextInput name="password" type="password" minLength={8} required value={password} onChange={e => setPassword(e.target.value)} /></Field>
          {!password ? <p className="formHint">{t('auth.passwordHint')}</p> : null}
          <PasswordStrengthMeter password={password} />
          {error ? <p className="formMessage formMessage--error">{error}</p> : null}
          <Button disabled={submitting} icon={<Rocket size={16} />}>{submitting ? t('auth.registerLoading') : t('auth.registerButton')}</Button>
        </form>
        <p className="authFooter">{t('auth.hasAccount')} <Link to="/login">{t('auth.loginLink')}</Link></p>
      </section>
    </main>
  );
}
