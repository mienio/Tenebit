import { ShieldCheck, ShieldOff } from 'lucide-react';
import { useState } from 'react';
import { apiRequest, refreshAccessToken } from '../api/apiClient';
import { useAuth } from '../auth/AuthProvider';
import { useI18n } from '../i18n/I18nProvider';
import { Button } from './Button';
import { Card } from './Card';
import { Field, TextInput } from './FormFields';

type SetupResponse = { secret: string; otpAuthUri: string; qrSvg: string };
type Message = { type: 'success' | 'error'; text: string } | null;

export function TwoFactorCard() {
  const auth = useAuth();
  const { t } = useI18n();
  const [setup, setSetup] = useState<SetupResponse | null>(null);
  const [code, setCode] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<Message>(null);
  const [showDisable, setShowDisable] = useState(false);

  async function syncSession() {
    const token = await refreshAccessToken();
    if (token) auth.loginWithToken(token);
  }

  async function startSetup() {
    setBusy(true);
    setMessage(null);
    try {
      const response = await apiRequest<SetupResponse>('/api/auth/2fa/setup', { method: 'POST' });
      setSetup(response);
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('twoFactor.setupFailed') });
    } finally {
      setBusy(false);
    }
  }

  async function confirmEnable() {
    setBusy(true);
    setMessage(null);
    try {
      await apiRequest('/api/auth/2fa/enable', { method: 'POST', body: JSON.stringify({ code }) });
      setSetup(null);
      setCode('');
      await syncSession();
      setMessage({ type: 'success', text: t('twoFactor.enabled') });
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('twoFactor.invalidCode') });
    } finally {
      setBusy(false);
    }
  }

  async function confirmDisable() {
    setBusy(true);
    setMessage(null);
    try {
      await apiRequest('/api/auth/2fa/disable', { method: 'POST', body: JSON.stringify({ code }) });
      setShowDisable(false);
      setCode('');
      await syncSession();
      setMessage({ type: 'success', text: t('twoFactor.disabled') });
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('twoFactor.invalidCode') });
    } finally {
      setBusy(false);
    }
  }

  return (
    <Card>
      <div className="sectionTitle"><div><h2>{t('twoFactor.title')}</h2><p>{t('twoFactor.description')}</p></div></div>
      {message ? <p className={`formMessage formMessage--${message.type}`}>{message.text}</p> : null}

      {auth.isTwoFactorEnabled ? (
        showDisable ? (
          <div className="formGrid">
            <Field label={t('twoFactor.codeLabel')}><TextInput value={code} onChange={e => setCode(e.target.value)} inputMode="numeric" maxLength={6} autoFocus /></Field>
            <div className="formActions formActions--split">
              <Button variant="ghost" type="button" onClick={() => { setShowDisable(false); setCode(''); }}>{t('common.cancel')}</Button>
              <Button variant="danger" type="button" disabled={busy || code.length !== 6} onClick={confirmDisable} icon={<ShieldOff size={16} />}>{t('twoFactor.disableButton')}</Button>
            </div>
          </div>
        ) : (
          <div className="formActions">
            <span className="muted">{t('twoFactor.statusEnabled')}</span>
            <Button variant="danger" type="button" onClick={() => setShowDisable(true)} icon={<ShieldOff size={16} />}>{t('twoFactor.disableButton')}</Button>
          </div>
        )
      ) : setup ? (
        <div className="twoFactorSetup">
          <p>{t('twoFactor.scanPrompt')}</p>
          <div className="twoFactorSetup__qr" dangerouslySetInnerHTML={{ __html: setup.qrSvg }} />
          <p className="muted">{t('twoFactor.manualEntry')}</p>
          <code className="twoFactorSetup__secret">{setup.secret}</code>
          <Field label={t('twoFactor.codeLabel')}><TextInput value={code} onChange={e => setCode(e.target.value)} inputMode="numeric" maxLength={6} autoFocus /></Field>
          <div className="formActions formActions--split">
            <Button variant="ghost" type="button" onClick={() => { setSetup(null); setCode(''); }}>{t('common.cancel')}</Button>
            <Button type="button" disabled={busy || code.length !== 6} onClick={confirmEnable} icon={<ShieldCheck size={16} />}>{t('twoFactor.enableButton')}</Button>
          </div>
        </div>
      ) : (
        <div className="formActions">
          <span className="muted">{t('twoFactor.statusDisabled')}</span>
          <Button type="button" disabled={busy} onClick={startSetup} icon={<ShieldCheck size={16} />}>{t('twoFactor.setupButton')}</Button>
        </div>
      )}
    </Card>
  );
}
