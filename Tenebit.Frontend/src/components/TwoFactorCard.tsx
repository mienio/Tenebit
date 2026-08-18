import { Download, KeyRound, ShieldCheck, ShieldOff } from 'lucide-react';
import { useEffect, useState } from 'react';
import { apiRequest } from '../api/apiClient';
import { useAuth } from '../auth/AuthProvider';
import { useI18n } from '../i18n/I18nProvider';
import { Button } from './Button';
import { Card } from './Card';
import { ConfirmDialog } from './ConfirmDialog';
import { Field, TextInput } from './FormFields';

type SetupResponse = { secret: string; otpAuthUri: string; qrSvg: string };
type EnableResponse = { recoveryCodes: string[]; token: string };
type RecoveryCodesResponse = { recoveryCodes: string[]; remainingUnused: number };
type SessionResponse = { token: string };
type Message = { type: 'success' | 'error'; text: string } | null;

function downloadRecoveryCodes(codes: string[], header: string, fileName: string) {
  const content = `${header}\n\n${codes.join('\n')}\n`;
  const blob = new Blob(['﻿' + content], { type: 'text/plain' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

export function TwoFactorCard() {
  const auth = useAuth();
  const { t } = useI18n();
  const [setup, setSetup] = useState<SetupResponse | null>(null);
  const [code, setCode] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<Message>(null);
  const [showDisable, setShowDisable] = useState(false);
  const [recoveryCodes, setRecoveryCodes] = useState<string[] | null>(null);
  const [remainingCodes, setRemainingCodes] = useState<number | null>(null);
  const [showRegenerate, setShowRegenerate] = useState(false);
  const [regenerateCode, setRegenerateCode] = useState('');

  useEffect(() => {
    if (!auth.isTwoFactorEnabled) { setRemainingCodes(null); return; }
    apiRequest<number>('/api/auth/2fa/recovery-codes/status')
      .then(setRemainingCodes)
      .catch(() => {});
  }, [auth.isTwoFactorEnabled]);

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
      const response = await apiRequest<EnableResponse>('/api/auth/2fa/enable', { method: 'POST', body: JSON.stringify({ code }) });
      setSetup(null);
      setCode('');
      auth.loginWithToken(response.token);
      setRecoveryCodes(response.recoveryCodes);
      setRemainingCodes(response.recoveryCodes.length);
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('twoFactor.invalidCode') });
    } finally {
      setBusy(false);
    }
  }

  function acknowledgeRecoveryCodes() {
    setRecoveryCodes(null);
    setMessage({ type: 'success', text: t('twoFactor.enabled') });
  }

  async function confirmDisable() {
    setBusy(true);
    setMessage(null);
    try {
      const response = await apiRequest<SessionResponse>('/api/auth/2fa/disable', { method: 'POST', body: JSON.stringify({ code }) });
      setShowDisable(false);
      setCode('');
      auth.loginWithToken(response.token);
      setMessage({ type: 'success', text: t('twoFactor.disabled') });
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('twoFactor.invalidCode') });
    } finally {
      setBusy(false);
    }
  }

  async function confirmRegenerate() {
    setBusy(true);
    setMessage(null);
    try {
      const response = await apiRequest<RecoveryCodesResponse>('/api/auth/2fa/recovery-codes/regenerate', { method: 'POST', body: JSON.stringify({ code: regenerateCode }) });
      setShowRegenerate(false);
      setRegenerateCode('');
      setRecoveryCodes(response.recoveryCodes);
      setRemainingCodes(response.remainingUnused);
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('twoFactor.regenerateFailed') });
    } finally {
      setBusy(false);
    }
  }

  if (recoveryCodes) {
    return (
      <Card>
        <div className="sectionTitle"><div><h2>{t('twoFactor.recoveryCodesTitle')}</h2><p>{t('twoFactor.recoveryCodesDesc')}</p></div></div>
        <div className="listRows">
          {recoveryCodes.map(item => <div className="listRow" key={item}><code>{item}</code></div>)}
        </div>
        <div className="formActions formActions--split">
          <Button variant="secondary" type="button" onClick={() => downloadRecoveryCodes(recoveryCodes, t('twoFactor.recoveryCodesFileHeader'), t('twoFactor.recoveryCodesFileName'))} icon={<Download size={16} />}>{t('twoFactor.recoveryCodesDownload')}</Button>
          <Button type="button" onClick={acknowledgeRecoveryCodes} icon={<ShieldCheck size={16} />}>{t('twoFactor.recoveryCodesConfirm')}</Button>
        </div>
      </Card>
    );
  }

  return (
    <Card>
      <div className="sectionTitle"><div><h2>{t('twoFactor.title')}</h2><p>{t('twoFactor.description')}</p></div></div>
      {message ? <p className={`formMessage formMessage--${message.type}`}>{message.text}</p> : null}

      {auth.isTwoFactorEnabled ? (
        showDisable ? (
          <div className="formGrid">
            <Field label={t('twoFactor.codeLabel')}><TextInput value={code} onChange={e => setCode(e.target.value)} inputMode="numeric" maxLength={6} autoFocus autoComplete="one-time-code" /></Field>
            <div className="formActions formActions--split">
              <Button variant="ghost" type="button" onClick={() => { setShowDisable(false); setCode(''); }}>{t('common.cancel')}</Button>
              <Button variant="danger" type="button" disabled={busy || code.length !== 6} onClick={confirmDisable} icon={<ShieldOff size={16} />}>{t('twoFactor.disableButton')}</Button>
            </div>
          </div>
        ) : (
          <>
            <div className="formActions">
              <span className="muted">{t('twoFactor.statusEnabled')}</span>
              <Button variant="danger" type="button" onClick={() => setShowDisable(true)} icon={<ShieldOff size={16} />}>{t('twoFactor.disableButton')}</Button>
            </div>
            {remainingCodes !== null && (
              <div className="formActions">
                <span className={remainingCodes <= 2 ? 'formMessage formMessage--error' : 'muted'}>
                  {remainingCodes <= 2 ? t('twoFactor.lowCodesWarning', { count: remainingCodes }) : t('twoFactor.recoveryCodesRemaining', { count: remainingCodes })}
                </span>
                <Button variant="secondary" type="button" onClick={() => setShowRegenerate(true)} icon={<KeyRound size={16} />}>{t('twoFactor.regenerateButton')}</Button>
              </div>
            )}
          </>
        )
      ) : setup ? (
        <div className="twoFactorSetup">
          <p>{t('twoFactor.scanPrompt')}</p>
          <div className="twoFactorSetup__qr"><img src={`data:image/svg+xml;charset=utf-8,${encodeURIComponent(setup.qrSvg)}`} alt="TOTP QR" /></div>
          <p className="muted">{t('twoFactor.manualEntry')}</p>
          <code className="twoFactorSetup__secret">{setup.secret}</code>
          <Field label={t('twoFactor.codeLabel')}><TextInput value={code} onChange={e => setCode(e.target.value)} inputMode="numeric" maxLength={6} autoFocus autoComplete="one-time-code" /></Field>
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

      <ConfirmDialog
        open={showRegenerate}
        title={t('twoFactor.regenerateConfirmTitle')}
        description={t('twoFactor.regenerateConfirmDesc')}
        confirmLabel={t('twoFactor.regenerateButton')}
        confirmDisabled={busy || regenerateCode.length !== 6}
        onConfirm={confirmRegenerate}
        onClose={() => { setShowRegenerate(false); setRegenerateCode(''); }}
      >
        <Field label={t('twoFactor.codeLabel')}><TextInput value={regenerateCode} onChange={e => setRegenerateCode(e.target.value)} inputMode="numeric" maxLength={6} autoFocus autoComplete="one-time-code" /></Field>
      </ConfirmDialog>
    </Card>
  );
}
