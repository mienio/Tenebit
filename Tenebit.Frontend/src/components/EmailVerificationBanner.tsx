import { useState } from 'react';
import { apiRequest } from '../api/apiClient';
import { useAuth } from '../auth/AuthProvider';
import { useI18n } from '../i18n/I18nProvider';

export function EmailVerificationBanner() {
  const auth = useAuth();
  const { t } = useI18n();
  const [sent, setSent] = useState(false);
  const [sending, setSending] = useState(false);

  if (auth.isEmailVerified) return null;

  async function resend() {
    setSending(true);
    try {
      await apiRequest('/api/auth/resend-verification', { method: 'POST' });
      setSent(true);
    } finally {
      setSending(false);
    }
  }

  return (
    <div className="verifyBanner">
      <span>{sent ? t('auth.verifyBannerSent') : t('auth.verifyBannerMessage')}</span>
      {!sent ? <button type="button" onClick={resend} disabled={sending}>{sending ? t('auth.forgotLoading') : t('auth.verifyBannerAction')}</button> : null}
    </div>
  );
}
