import { MailCheck } from 'lucide-react';
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

export function VerifyEmailPage() {
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

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      await apiRequest('/api/auth/verify-email', {
        method: 'POST',
        body: JSON.stringify({ email: email.trim(), code, newPassword: password })
      });
      navigate('/login?verified=1', { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : t('auth.verifyError'));
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
        <h1>{t('auth.verifyTitle')}</h1>
        <p className="authCard__hint">{t('auth.verifyLead')}</p>
        <form className="formGrid" onSubmit={submit}>
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
          <Button disabled={submitting || code.length !== 6 || password.length < 8} icon={<MailCheck size={16} />}>
            {submitting ? t('auth.verifyLoading') : t('auth.verifyButton')}
          </Button>
        </form>
        <p className="authFooter"><Link to="/login">{t('auth.backToLogin')}</Link></p>
      </section>
      <PublicFooter compact />
    </main>
  );
}
