import { TriangleAlert } from 'lucide-react';
import { useEffect, useState } from 'react';
import { apiRequest } from '../api/apiClient';
import { useAuth } from '../auth/AuthProvider';
import { useI18n } from '../i18n/I18nProvider';

const SENT_MESSAGE_DURATION_MS = 6000;

export function EmailVerificationNotice() {
  const auth = useAuth();
  const { t } = useI18n();
  const [sent, setSent] = useState(false);
  const [sending, setSending] = useState(false);

  useEffect(() => {
    if (!sent) return;
    const timeout = window.setTimeout(() => setSent(false), SENT_MESSAGE_DURATION_MS);
    return () => window.clearTimeout(timeout);
  }, [sent]);

  if (auth.isEmailVerified) return null;

  async function resend() {
    setSending(true);
    try {
      await apiRequest('/api/auth/resend-verification', {
        method: 'POST',
        body: JSON.stringify({ email: auth.userEmail })
      });
      setSent(true);
    } finally {
      setSending(false);
    }
  }

  return (
    <div className="settingsNotice">
      <TriangleAlert size={16} />
      <span>{sent ? t('auth.verifyBannerSent') : t('auth.verifyBannerMessage')}</span>
      {!sent ? <button type="button" onClick={resend} disabled={sending}>{sending ? t('auth.forgotLoading') : t('auth.verifyBannerAction')}</button> : null}
    </div>
  );
}
