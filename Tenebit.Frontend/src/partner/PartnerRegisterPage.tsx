import { FormEvent, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Button } from '../components/Button';
import { Field, SelectInput, TextInput } from '../components/FormFields';
import { TurnstileWidget } from '../components/TurnstileWidget';
import { registerAffiliate, verifyAffiliateEmail } from './partnerApi';
import { PartnerLanguageSwitch, usePartnerLocale, type PartnerLocale } from './i18n';
import './partner.css';

const TURNSTILE_SITE_KEY = import.meta.env.VITE_TURNSTILE_SITE_KEY;

const content: Record<PartnerLocale, {
  verifyTitle: string; verifySentBefore: string; verifySentAfter: string;
  emailCodeLabel: string; verifySubmit: string; verifySubmitting: string; noCode: string;
  title: string; intro: string;
  firstName: string; lastName: string; email: string; password: string; passwordInfo: string;
  payoutMethod: string; revtagInfo: string; revtagPlaceholderRevolut: string; revtagPlaceholderPayPal: string;
  termsPrefix: string; termsLink: string; termsSuffix: string;
  termsRequired: string; registerError: string; verifyError: string;
  submit: string; submitting: string; haveAccount: string; login: string;
}> = {
  en: {
    verifyTitle: 'Last step - confirm your email',
    verifySentBefore: 'We sent a 6-digit code to ', verifySentAfter: '. Enter it below to activate your account.',
    emailCodeLabel: 'Code from the email',
    verifySubmit: 'Confirm and go to the panel', verifySubmitting: 'Confirming…',
    noCode: 'Didn’t get the code? Check spam or go back and register again.',
    title: 'Start earning with Tenebit',
    intro: 'Create an account, get your unique promo code, and collect a commission for every person who starts using Tenebit through you. No limits, payouts to PayPal or Revolut.',
    firstName: 'First name', lastName: 'Last name', email: 'Email', password: 'Password',
    passwordInfo: 'At least 8 characters.',
    payoutMethod: 'PayPal / Revolut account (optional)',
    revtagInfo: 'Revolut: format @name. PayPal: your account e-mail - that’s where commission payouts go. You can add or change it later in your profile.',
    revtagPlaceholderRevolut: '@your-name', revtagPlaceholderPayPal: 'you@example.com',
    termsPrefix: 'I accept the ', termsLink: 'partner program terms', termsSuffix: ', including the requirement to hold a PayPal or Revolut account to receive payouts.',
    termsRequired: 'Accepting the partner program terms is required.',
    registerError: 'Could not register.', verifyError: 'Could not confirm the email.',
    submit: 'Sign up', submitting: 'Registering…', haveAccount: 'Already have an account?', login: 'Log in',
  },
  pl: {
    verifyTitle: 'Ostatni krok - potwierdź e-mail',
    verifySentBefore: 'Wysłaliśmy 6-cyfrowy kod na adres ', verifySentAfter: '. Wpisz go poniżej, żeby aktywować konto.',
    emailCodeLabel: 'Kod z e-maila',
    verifySubmit: 'Potwierdź i przejdź do panelu', verifySubmitting: 'Potwierdzanie…',
    noCode: 'Nie dostałeś kodu? Sprawdź spam albo wróć i zarejestruj się ponownie.',
    title: 'Zacznij zarabiać z Tenebit',
    intro: 'Załóż konto, odbierz swój unikalny kod promocyjny i zgarniaj prowizję za każdą osobę, która przez Ciebie skorzysta z Tenebit. Zero limitów, wypłaty na PayPal lub Revolut.',
    firstName: 'Imię', lastName: 'Nazwisko', email: 'E-mail', password: 'Hasło',
    passwordInfo: 'Co najmniej 8 znaków.',
    payoutMethod: 'Konto PayPal / Revolut (opcjonalnie)',
    revtagInfo: 'Revolut: format @nazwa. PayPal: e-mail Twojego konta - tam trafią wypłaty prowizji. Możesz je uzupełnić lub zmienić później w profilu.',
    revtagPlaceholderRevolut: '@twoja-nazwa', revtagPlaceholderPayPal: 'ty@example.com',
    termsPrefix: 'Akceptuję ', termsLink: 'regulamin programu partnerskiego', termsSuffix: ', w tym wymóg posiadania konta PayPal lub Revolut do odbioru wypłat.',
    termsRequired: 'Akceptacja regulaminu programu partnerskiego jest wymagana.',
    registerError: 'Nie udało się zarejestrować.', verifyError: 'Nie udało się potwierdzić e-maila.',
    submit: 'Zarejestruj się', submitting: 'Rejestracja…', haveAccount: 'Masz już konto?', login: 'Zaloguj się',
  },
};

export function PartnerRegisterPage() {
  const navigate = useNavigate();
  const { path, locale } = usePartnerLocale();
  const t = content[locale];
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [payoutMethod, setPayoutMethod] = useState<'Revolut' | 'PayPal'>('Revolut');
  const [payoutAccountTag, setPayoutAccountTag] = useState('');
  const [acceptTerms, setAcceptTerms] = useState(false);
  const [turnstileToken, setTurnstileToken] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [submitted, setSubmitted] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const captchaPending = Boolean(TURNSTILE_SITE_KEY) && !turnstileToken;
  const [code, setCode] = useState('');
  const [verifyError, setVerifyError] = useState<string | null>(null);
  const [verifying, setVerifying] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!acceptTerms) { setError(t.termsRequired); return; }
    setError(null);
    setSubmitting(true);
    try {
      await registerAffiliate({
        email, password, firstName, lastName,
        payoutMethod,
        payoutAccountTag: payoutAccountTag.trim() || null,
        acceptTerms,
        turnstileToken,
      });
      setSubmitted(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : t.registerError);
    } finally {
      setSubmitting(false);
    }
  }

  async function handleVerify(event: FormEvent) {
    event.preventDefault();
    setVerifyError(null);
    setVerifying(true);
    try {
      await verifyAffiliateEmail(email, code);
      navigate(path('login'), { replace: true });
    } catch (err) {
      setVerifyError(err instanceof Error ? err.message : t.verifyError);
    } finally {
      setVerifying(false);
    }
  }

  if (submitted) {
    return (
      <div className="partnerAuthPage">
        <div className="partnerAuthPage__panel card">
          <h1 style={{ marginTop: 0, fontSize: 22 }}>{t.verifyTitle}</h1>
          <p>{t.verifySentBefore}{email}{t.verifySentAfter}</p>
          <form className="formGrid" onSubmit={handleVerify}>
            <Field label={t.emailCodeLabel}>
              <TextInput inputMode="numeric" maxLength={6} minLength={6} value={code} onChange={e => setCode(e.target.value)} required autoFocus />
            </Field>
            {verifyError ? <p className="formMessage formMessage--error">{verifyError}</p> : null}
            <div className="formActions">
              <Button type="submit" disabled={verifying}>{verifying ? t.verifySubmitting : t.verifySubmit}</Button>
            </div>
          </form>
          <p style={{ marginTop: 16, fontSize: 13 }}>{t.noCode}</p>
        </div>
      </div>
    );
  }

  return (
    <div className="partnerAuthPage">
      <div className="partnerAuthPage__panel card">
        <h1 style={{ marginTop: 0, fontSize: 22 }}>{t.title}</h1>
        <p className="adminMuted" style={{ marginBottom: 20 }}>{t.intro}</p>
        <form className="formGrid" onSubmit={handleSubmit}>
          <Field label={t.firstName}>
            <TextInput value={firstName} onChange={e => setFirstName(e.target.value)} required autoFocus />
          </Field>
          <Field label={t.lastName}>
            <TextInput value={lastName} onChange={e => setLastName(e.target.value)} required />
          </Field>
          <Field label={t.email}>
            <TextInput type="email" value={email} onChange={e => setEmail(e.target.value)} required />
          </Field>
          <Field label={t.password} info={t.passwordInfo}>
            <TextInput type="password" value={password} onChange={e => setPassword(e.target.value)} required minLength={8} />
          </Field>
          <Field label={t.payoutMethod} info={t.revtagInfo}>
            <SelectInput value={payoutMethod} onChange={e => setPayoutMethod(e.target.value as 'Revolut' | 'PayPal')}>
              <option value="Revolut">Revolut</option>
              <option value="PayPal">PayPal</option>
            </SelectInput>
            <TextInput
              value={payoutAccountTag}
              onChange={e => setPayoutAccountTag(e.target.value)}
              placeholder={payoutMethod === 'PayPal' ? t.revtagPlaceholderPayPal : t.revtagPlaceholderRevolut}
              style={{ marginTop: 8 }}
            />
          </Field>
          <label style={{ display: 'flex', gap: 8, alignItems: 'flex-start', fontSize: 13.5 }}>
            <input type="checkbox" checked={acceptTerms} onChange={e => setAcceptTerms(e.target.checked)} style={{ marginTop: 3 }} required />
            <span>
              {t.termsPrefix}<Link to={path('terms')} target="_blank" rel="noreferrer">{t.termsLink}</Link>{t.termsSuffix}
            </span>
          </label>
          {TURNSTILE_SITE_KEY ? <TurnstileWidget siteKey={TURNSTILE_SITE_KEY} onToken={setTurnstileToken} /> : null}
          {error ? <p className="formMessage formMessage--error">{error}</p> : null}
          <div className="formActions">
            <Button type="submit" disabled={submitting || captchaPending}>{submitting ? t.submitting : t.submit}</Button>
          </div>
        </form>
        <p style={{ marginTop: 16, fontSize: 13 }}>
          {t.haveAccount} <Link to={path('login')}>{t.login}</Link>
        </p>
        <p style={{ marginTop: 16, fontSize: 13 }}><PartnerLanguageSwitch /></p>
      </div>
    </div>
  );
}
