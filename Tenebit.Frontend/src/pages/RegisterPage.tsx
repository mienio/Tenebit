import { Rocket } from 'lucide-react';
import { FormEvent, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthProvider';
import { BackButton } from '../components/BackButton';
import { Button } from '../components/Button';
import { Field, SelectInput, TextInput } from '../components/FormFields';
import { PasswordStrengthMeter } from '../components/PasswordStrengthMeter';
import { PublicFooter } from '../components/PublicFooter';
import { SocialLoginButtons } from '../components/SocialLoginButtons';
import { useI18n } from '../i18n/I18nProvider';
import { LanguageSwitcher } from '../i18n/LanguageSwitcher';
import { legalContentFor } from '../legal/legalContent';

const currencies = ['PLN', 'EUR', 'USD', 'GBP', 'CHF', 'CZK', 'UAH'];

export function RegisterPage() {
  const auth = useAuth();
  const navigate = useNavigate();
  const { t, language } = useI18n();
  const legal = legalContentFor(language).ui;
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const passwordsMismatch = confirmPassword.length > 0 && password !== confirmPassword;

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (password !== confirmPassword) {
      setError(t('auth.passwordMismatch'));
      return;
    }
    const form = new FormData(event.currentTarget);
    const email = String(form.get('email') ?? '').trim();
    setError(null);
    setSubmitting(true);
    try {
      const result = await auth.register(
        String(form.get('organizationName') ?? ''),
        String(form.get('displayName') ?? ''),
        email,
        password,
        String(form.get('currency') ?? 'PLN'),
        language,
        form.get('acceptTerms') === 'on'
      );
      const destination = result.requiresEmailVerification
        ? `/verify-email#email=${encodeURIComponent(email)}`
        : '/login?registered=1';
      navigate(destination, { replace: true });
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
        <p className="authCard__hint">
          {t('auth.socialTermsNotice')} <Link to="/terms">{legal.terms}</Link> {t('auth.acceptTermsAnd')} <Link to="/privacy">{legal.privacy}</Link>.
        </p>
        <form className="formGrid" onSubmit={handleSubmit}>
          <Field label={t('auth.orgNameLabel')}><TextInput name="organizationName" required autoFocus /></Field>
          <Field label={t('auth.displayNameLabel')}><TextInput name="displayName" required autoComplete="name" /></Field>
          <Field label={t('auth.emailLabel')}><TextInput name="email" type="email" required autoComplete="email" /></Field>
          <Field label={t('auth.currencyLabel')}>
            <SelectInput name="currency" defaultValue="PLN">
              {currencies.map(code => <option key={code} value={code}>{code}</option>)}
            </SelectInput>
          </Field>
          <Field label={t('auth.passwordLabel')} info={t('auth.passwordHint')}>
            <TextInput name="password" type="password" minLength={8} required autoComplete="new-password" value={password} onChange={event => setPassword(event.target.value)} />
          </Field>
          {!password ? <p className="formHint">{t('auth.passwordHint')}</p> : null}
          <PasswordStrengthMeter password={password} />
          <Field label={t('auth.confirmPasswordLabel')}>
            <TextInput
              name="confirmPassword"
              type="password"
              minLength={8}
              required
              autoComplete="new-password"
              value={confirmPassword}
              onChange={event => setConfirmPassword(event.target.value)}
            />
          </Field>
          {passwordsMismatch ? <p className="formMessage formMessage--error">{t('auth.passwordMismatch')}</p> : null}
          <label className="authLegalConsent">
            <input type="checkbox" name="acceptTerms" required />
            <span>{t('auth.acceptTermsPrefix')} <Link to="/terms">{legal.terms}</Link> {t('auth.acceptTermsAnd')} <Link to="/privacy">{legal.privacy}</Link>.</span>
          </label>
          {error ? <p className="formMessage formMessage--error">{error}</p> : null}
          <Button disabled={submitting || passwordsMismatch} icon={<Rocket size={16} />}>{submitting ? t('auth.registerLoading') : t('auth.registerButton')}</Button>
        </form>
        <p className="authFooter">{t('auth.hasAccount')} <Link to="/login">{t('auth.loginLink')}</Link></p>
      </section>
      <PublicFooter compact />
    </main>
  );
}
