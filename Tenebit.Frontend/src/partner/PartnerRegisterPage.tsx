import { FormEvent, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Button } from '../components/Button';
import { Field, TextInput } from '../components/FormFields';
import { registerAffiliate, verifyAffiliateEmail } from './partnerApi';
import { PartnerLanguageSwitch, usePartnerLocale, type PartnerLocale } from './i18n';
import './partner.css';

const content: Record<PartnerLocale, {
  verifyTitle: string; verifySentBefore: string; verifySentAfter: string;
  emailCodeLabel: string; verifySubmit: string; verifySubmitting: string; noCode: string;
  title: string; intro: string;
  firstName: string; lastName: string; email: string; password: string; passwordInfo: string;
  revtag: string; revtagInfo: string; revtagPlaceholder: string;
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
    intro: 'Create an account, get your unique promo code, and collect a commission for every person who starts using Tenebit through you. No limits, payouts to Revolut.',
    firstName: 'First name', lastName: 'Last name', email: 'Email', password: 'Password',
    passwordInfo: 'At least 8 characters.',
    revtag: 'Revolut revtag (optional)',
    revtagInfo: 'Your Revolut account identifier in the format @name - that’s where commission payouts go. You can add it later in your profile.',
    revtagPlaceholder: '@your-name',
    termsPrefix: 'I accept the ', termsLink: 'partner program terms', termsSuffix: ', including the requirement to hold a Revolut account to receive payouts.',
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
    intro: 'Załóż konto, odbierz swój unikalny kod promocyjny i zgarniaj prowizję za każdą osobę, która przez Ciebie skorzysta z Tenebit. Zero limitów, wypłaty na Revolut.',
    firstName: 'Imię', lastName: 'Nazwisko', email: 'E-mail', password: 'Hasło',
    passwordInfo: 'Co najmniej 8 znaków.',
    revtag: 'Revtag Revolut (opcjonalnie)',
    revtagInfo: 'Identyfikator Twojego konta Revolut w formacie @nazwa - tam trafią wypłaty prowizji. Możesz go uzupełnić później w profilu.',
    revtagPlaceholder: '@twoja-nazwa',
    termsPrefix: 'Akceptuję ', termsLink: 'regulamin programu partnerskiego', termsSuffix: ', w tym wymóg posiadania konta Revolut do odbioru wypłat.',
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
  const [revolutTag, setRevolutTag] = useState('');
  const [acceptTerms, setAcceptTerms] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [submitted, setSubmitted] = useState(false);
  const [submitting, setSubmitting] = useState(false);
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
        revolutTag: revolutTag.trim() || null,
        acceptTerms,
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
          <Field label={t.revtag} info={t.revtagInfo}>
            <TextInput value={revolutTag} onChange={e => setRevolutTag(e.target.value)} placeholder={t.revtagPlaceholder} />
          </Field>
          <label style={{ display: 'flex', gap: 8, alignItems: 'flex-start', fontSize: 13.5 }}>
            <input type="checkbox" checked={acceptTerms} onChange={e => setAcceptTerms(e.target.checked)} style={{ marginTop: 3 }} required />
            <span>
              {t.termsPrefix}<Link to={path('terms')} target="_blank" rel="noreferrer">{t.termsLink}</Link>{t.termsSuffix}
            </span>
          </label>
          {error ? <p className="formMessage formMessage--error">{error}</p> : null}
          <div className="formActions">
            <Button type="submit" disabled={submitting}>{submitting ? t.submitting : t.submit}</Button>
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
