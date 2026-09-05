import { ArrowLeft, LogIn, ShieldCheck } from 'lucide-react';
import { FormEvent, useEffect, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { BackButton } from '../components/BackButton';
import { Button } from '../components/Button';
import { Field, TextInput } from '../components/FormFields';
import { SocialLoginButtons } from '../components/SocialLoginButtons';
import { PublicFooter } from '../components/PublicFooter';
import { useAuth } from '../auth/AuthProvider';
import { useI18n } from '../i18n/I18nProvider';
import { LanguageSwitcher } from '../i18n/LanguageSwitcher';
import { legalContentFor } from '../legal/legalContent';

export function LoginPage() {
  const auth = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const { t, language } = useI18n();
  const legal = legalContentFor(language).ui;
  const routeState = location.state as { from?: unknown; challengeToken?: unknown } | null;
  const fromState = routeState?.from;
  const returnTo = typeof fromState === 'string' && fromState.startsWith('/') ? fromState : '/dashboard';
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [challengeToken, setChallengeToken] = useState<string | null>(
    typeof routeState?.challengeToken === 'string' ? routeState.challengeToken : null
  );
  // Captured once at mount: RequireAuth bounces here the instant the token refresh gives up, and the
  // flag must survive long enough to render the notice, but a subsequent unrelated visit to /login
  // (e.g. after a deliberate logout) should not keep echoing a stale "session expired" message.
  const [showSessionExpired] = useState(auth.sessionExpired);
  useEffect(() => { auth.clearSessionExpired(); }, [auth]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    setError(null);
    setSubmitting(true);
    try {
      const outcome = await auth.login(String(form.get('email') ?? ''), String(form.get('password') ?? ''));
      if (outcome.requiresTwoFactor) {
        setChallengeToken(outcome.challengeToken);
        return;
      }
      navigate(returnTo, { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : t('auth.loginFailed'));
    } finally {
      setSubmitting(false);
    }
  }

  async function handleTwoFactorSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!challengeToken) return;
    const form = new FormData(event.currentTarget);
    setError(null);
    setSubmitting(true);
    try {
      await auth.completeTwoFactorLogin(challengeToken, String(form.get('code') ?? ''), form.get('rememberDevice') === 'on');
      navigate(returnTo, { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : t('twoFactor.invalidCode'));
    } finally {
      setSubmitting(false);
    }
  }

  if (challengeToken) {
    return (
      <main className="authShell">
        {/* Distinct key forces React to remount this subtree instead of patching the login form's
            DOM in place - otherwise the uncontrolled "code" input reuses the "email" input's node
            and inherits its stale value, since both branches have the same tag/position shape. */}
        <section className="authCard" key="two-factor-challenge">
          <div className="authTop">
            <button type="button" className="authIcon" aria-label={t('common.back')} title={t('common.back')} onClick={() => { setChallengeToken(null); setError(null); }}><ArrowLeft size={24} /></button>
            <LanguageSwitcher />
          </div>
          <h1>{t('auth.twoFactorTitle')}</h1>
          <p>{t('auth.twoFactorPrompt')}</p>
          <form className="formGrid" onSubmit={handleTwoFactorSubmit}>
            <Field label={t('auth.twoFactorCodeLabel')} info={t('auth.twoFactorCodeHint')}><TextInput name="code" maxLength={11} required autoFocus autoComplete="one-time-code" /></Field>
            <label className="checkField"><input name="rememberDevice" type="checkbox" /> {t('auth.rememberDevice')}</label>
            {error ? <p className="formMessage formMessage--error">{error}</p> : null}
            <Button disabled={submitting} icon={<ShieldCheck size={16} />}>{submitting ? t('auth.loginLoading') : t('auth.twoFactorVerifyButton')}</Button>
          </form>
          <p className="authFooter"><button type="button" className="linkButton" onClick={() => { setChallengeToken(null); setError(null); }}>{t('auth.backToLogin')}</button></p>
        </section>
        <PublicFooter compact />
      </main>
    );
  }

  return (
    <main className="authShell">
      <section className="authCard" key="login-form">
        <div className="authTop">
          <BackButton to="/" />
          <LanguageSwitcher />
        </div>
        <h1>{t('auth.loginTitle')}</h1>
        {showSessionExpired ? <p className="formMessage formMessage--error">{t('auth.sessionExpired')}</p> : null}
        <SocialLoginButtons returnUrl={returnTo} />
        <p className="authCard__hint">
          {t('auth.socialTermsNotice')} <Link to="/terms">{legal.terms}</Link> {t('auth.acceptTermsAnd')} <Link to="/privacy">{legal.privacy}</Link>.
        </p>
        <form className="formGrid" onSubmit={handleSubmit}>
          <Field label={t('auth.emailLabel')}><TextInput name="email" type="email" required autoFocus /></Field>
          <Field label={t('auth.passwordLabel')}><TextInput name="password" type="password" required /></Field>
          <p className="authInlineLink"><Link to="/forgot-password">{t('auth.forgotLink')}</Link></p>
          {error ? <p className="formMessage formMessage--error">{error}</p> : null}
          <Button disabled={submitting} icon={<LogIn size={16} />}>{submitting ? t('auth.loginLoading') : t('auth.loginButton')}</Button>
        </form>
        <p className="authFooter">{t('auth.noAccount')} <Link to="/register">{t('auth.registerLink')}</Link></p>
      </section>
      <PublicFooter compact />
    </main>
  );
}
