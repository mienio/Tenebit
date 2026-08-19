import { KeyRound } from 'lucide-react';
import { FormEvent, useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { apiRequest } from '../api/apiClient';
import { BackButton } from '../components/BackButton';
import { Button } from '../components/Button';
import { Field, TextInput } from '../components/FormFields';
import { PasswordStrengthMeter } from '../components/PasswordStrengthMeter';
import { PublicFooter } from '../components/PublicFooter';
import { SegmentedCodeInput } from '../components/SegmentedCodeInput';
import { clearUrlFragment, readRecoveryCodeFragment } from '../hooks/usePublicCapabilitySession';
import { useI18n } from '../i18n/I18nProvider';
import { LanguageSwitcher } from '../i18n/LanguageSwitcher';

export function ResetPasswordPage() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const [fragment] = useState(readRecoveryCodeFragment);
  const [email, setEmail] = useState(fragment.email);
  const [code, setCode] = useState(fragment.code);
  const [password, setPassword] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    clearUrlFragment();
  }, []);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await apiRequest('/api/auth/password/reset', {
        method: 'POST',
        body: JSON.stringify({ email: email.trim(), code, newPassword: password })
      });
      navigate('/login?reset=1', { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : t('auth.resetPasswordFailed'));
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
        <h1>{t('auth.resetTitle')}</h1>
        <p className="authCard__hint">{t('auth.resetLead')}</p>
        <form className="formGrid" onSubmit={handleSubmit}>
          <Field label={t('auth.emailLabel')}>
            <TextInput type="email" required autoComplete="email" value={email} onChange={event => setEmail(event.target.value)} />
          </Field>
          <SegmentedCodeInput
            value={code}
            onChange={setCode}
            label={t('auth.codeLabel')}
            pasteLabel={t('auth.codePaste')}
            disabled={submitting}
            autoFocus={Boolean(email) && !code}
          />
          <p className="formHint authCard__hint">{t('auth.codeHint')}</p>
          <Field label={t('auth.newPasswordLabel')} info={t('auth.passwordHint')}>
            <TextInput type="password" minLength={8} required autoComplete="new-password" value={password} onChange={event => setPassword(event.target.value)} />
          </Field>
          <PasswordStrengthMeter password={password} />
          {error ? <p className="formMessage formMessage--error">{error}</p> : null}
          <Button disabled={submitting || code.length !== 6 || password.length < 8} icon={<KeyRound size={16} />}>
            {submitting ? t('auth.resetLoading') : t('auth.resetButton')}
          </Button>
        </form>
        <p className="authFooter"><Link to="/forgot-password">{t('auth.requestNewCode')}</Link></p>
      </section>
      <PublicFooter compact />
    </main>
  );
}
