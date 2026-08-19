import { MailCheck } from 'lucide-react';
import { FormEvent, useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { apiRequest } from '../api/apiClient';
import { BackButton } from '../components/BackButton';
import { Button } from '../components/Button';
import { PublicFooter } from '../components/PublicFooter';
import { SegmentedCodeInput } from '../components/SegmentedCodeInput';
import { clearUrlFragment, readRecoveryCodeFragment } from '../hooks/usePublicCapabilitySession';
import { useI18n } from '../i18n/I18nProvider';
import { LanguageSwitcher } from '../i18n/LanguageSwitcher';

export function VerifyEmailPage() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const [fragment] = useState(readRecoveryCodeFragment);
  const email = fragment.email;
  const [code, setCode] = useState(fragment.code);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [resending, setResending] = useState(false);
  const [resent, setResent] = useState(false);

  useEffect(() => {
    clearUrlFragment();
  }, []);

  async function resendCode() {
    setResending(true);
    setResent(false);
    try {
      await apiRequest('/api/auth/resend-verification', {
        method: 'POST',
        body: JSON.stringify({ email: email.trim() })
      });
      setResent(true);
    } finally {
      setResending(false);
    }
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      await apiRequest('/api/auth/verify-email', {
        method: 'POST',
        body: JSON.stringify({ email: email.trim(), code })
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
        {email ? (
          <form className="formGrid" onSubmit={submit}>
            <p className="authCard__hint"><strong>{t('auth.verifySentTo')}</strong> {email}</p>
            <SegmentedCodeInput
              value={code}
              onChange={setCode}
              label={t('auth.codeLabel')}
              pasteLabel={t('auth.codePaste')}
              disabled={submitting}
              autoFocus
            />
            <p className="formHint authCard__hint">{t('auth.codeHint')}</p>
            {error ? <p className="formMessage formMessage--error">{error}</p> : null}
            <Button disabled={submitting || code.length !== 6} icon={<MailCheck size={16} />}>
              {submitting ? t('auth.verifyLoading') : t('auth.verifyButton')}
            </Button>
            <p className="authCard__hint">
              {resent ? t('auth.verifyBannerSent') : (
                <button type="button" className="linkButton" onClick={resendCode} disabled={resending}>
                  {resending ? t('auth.forgotLoading') : t('auth.verifyBannerAction')}
                </button>
              )}
            </p>
          </form>
        ) : (
          <p className="formMessage formMessage--error">{t('auth.verifyNoEmail')}</p>
        )}
        <p className="authFooter"><Link to="/login">{t('auth.backToLogin')}</Link></p>
      </section>
      <PublicFooter compact />
    </main>
  );
}
