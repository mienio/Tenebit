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

// EUR first as the default/fallback (same role FALLBACK_LANGUAGE plays for language below), then the
// rest of the roughly top 50 currencies by global trade volume.
const currencies = [
  'EUR', 'USD', 'GBP', 'CHF', 'JPY', 'CNY', 'AUD', 'CAD', 'NZD', 'SEK',
  'NOK', 'DKK', 'PLN', 'CZK', 'HUF', 'RON', 'BGN', 'ISK', 'TRY', 'RUB',
  'UAH', 'ILS', 'AED', 'SAR', 'QAR', 'KWD', 'BHD', 'OMR', 'JOD', 'EGP',
  'ZAR', 'NGN', 'KES', 'GHS', 'MAD', 'INR', 'PKR', 'BDT', 'LKR', 'IDR',
  'MYR', 'SGD', 'THB', 'PHP', 'VND', 'KRW', 'TWD', 'HKD', 'MXN', 'BRL'
];

const FALLBACK_CURRENCY = 'EUR';

// Same idea as the language auto-detection above: guess a sane default from the visitor's own browser
// locale (its region subtag, e.g. "pl" in "pl-PL") instead of always defaulting to one currency. A
// country outside this map - or with no currency in our list - falls back to EUR, exactly like an
// unsupported browser language falls back to English.
const countryToCurrency: Record<string, string> = {
  AT: 'EUR', BE: 'EUR', CY: 'EUR', EE: 'EUR', FI: 'EUR', FR: 'EUR', DE: 'EUR', GR: 'EUR', IE: 'EUR',
  IT: 'EUR', LV: 'EUR', LT: 'EUR', LU: 'EUR', MT: 'EUR', NL: 'EUR', PT: 'EUR', SK: 'EUR', SI: 'EUR',
  ES: 'EUR', HR: 'EUR',
  US: 'USD', GB: 'GBP', CH: 'CHF', LI: 'CHF', JP: 'JPY', CN: 'CNY', AU: 'AUD', CA: 'CAD', NZ: 'NZD',
  SE: 'SEK', NO: 'NOK', DK: 'DKK', PL: 'PLN', CZ: 'CZK', HU: 'HUF', RO: 'RON', BG: 'BGN', IS: 'ISK',
  TR: 'TRY', RU: 'RUB', UA: 'UAH', IL: 'ILS', AE: 'AED', SA: 'SAR', QA: 'QAR', KW: 'KWD', BH: 'BHD',
  OM: 'OMR', JO: 'JOD', EG: 'EGP', ZA: 'ZAR', NG: 'NGN', KE: 'KES', GH: 'GHS', MA: 'MAD', IN: 'INR',
  PK: 'PKR', BD: 'BDT', LK: 'LKR', ID: 'IDR', MY: 'MYR', SG: 'SGD', TH: 'THB', PH: 'PHP', VN: 'VND',
  KR: 'KRW', TW: 'TWD', HK: 'HKD', MX: 'MXN', BR: 'BRL'
};

function detectInitialCurrency(): string {
  const browserLanguages = window.navigator.languages ?? [window.navigator.language];
  for (const browserLanguage of browserLanguages) {
    const country = browserLanguage.split('-')[1]?.toUpperCase();
    const currency = country ? countryToCurrency[country] : undefined;
    if (currency && currencies.includes(currency)) return currency;
  }
  return FALLBACK_CURRENCY;
}

export function RegisterPage() {
  const auth = useAuth();
  const navigate = useNavigate();
  const { t, language } = useI18n();
  const legal = legalContentFor(language).ui;
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [currency] = useState(detectInitialCurrency);
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
        String(form.get('currency') ?? FALLBACK_CURRENCY),
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
            <SelectInput name="currency" defaultValue={currency}>
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
