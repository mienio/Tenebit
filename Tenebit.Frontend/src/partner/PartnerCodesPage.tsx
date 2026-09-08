import { Check, Copy, Plus, Power } from 'lucide-react';
import { FormEvent, useEffect, useState } from 'react';
import { Button } from '../components/Button';
import { Field, TextInput } from '../components/FormFields';
import { LoadingState } from '../components/StateViews';
import { PartnerLayout, PartnerPageHeader } from './PartnerLayout';
import { createMyCode, listMyCodes, setMyCodeActive, type AffiliateCode } from './partnerApi';
import { usePartnerLocale, type PartnerLocale } from './i18n';

const content: Record<PartnerLocale, {
  copyCode: string; copied: string;
  title: string; description: string; fetchError: string; createError: string; toggleError: string;
  customCodeLabel: (activeCount: number) => string; customCodeInfo: string; customCodePlaceholder: string;
  create: string; creating: string;
  colCode: string; colStatus: string;
  active: string; disabled: string; disable: string; enable: string;
  empty: string;
}> = {
  en: {
    copyCode: 'Copy code', copied: 'Copied',
    title: 'Your codes', description: 'Share a code directly with customers - it’s applied at checkout and counted toward your commission there. Codes never expire.',
    fetchError: 'Could not fetch codes.', createError: 'Could not create the code.', toggleError: 'Could not change the code status.',
    customCodeLabel: activeCount => `Custom code (optional) - ${activeCount} active`,
    customCodeInfo: 'Leave blank to generate a code automatically.',
    customCodePlaceholder: 'e.g. DAMIAN20',
    create: 'Create code', creating: 'Creating…',
    colCode: 'Code', colStatus: 'Status',
    active: 'Active', disabled: 'Disabled', disable: 'Disable', enable: 'Enable',
    empty: 'No codes yet - create your first one above.',
  },
  pl: {
    copyCode: 'Kopiuj kod', copied: 'Skopiowano',
    title: 'Twoje kody', description: 'Udostępniaj kod bezpośrednio klientom - jest stosowany przy checkout i tam liczy się do Twojej prowizji. Kody nie wygasają.',
    fetchError: 'Nie udało się pobrać kodów.', createError: 'Nie udało się utworzyć kodu.', toggleError: 'Nie udało się zmienić statusu kodu.',
    customCodeLabel: activeCount => `Własny kod (opcjonalnie) - ${activeCount} aktywnych`,
    customCodeInfo: 'Zostaw puste, aby wygenerować kod automatycznie.',
    customCodePlaceholder: 'np. DAMIAN20',
    create: 'Utwórz kod', creating: 'Tworzenie…',
    colCode: 'Kod', colStatus: 'Status',
    active: 'Aktywny', disabled: 'Wyłączony', disable: 'Wyłącz', enable: 'Włącz',
    empty: 'Brak kodów - utwórz pierwszy powyżej.',
  },
};

function CopyButton({ text, copyLabel, copiedLabel }: { text: string; copyLabel: string; copiedLabel: string }) {
  const [copied, setCopied] = useState(false);
  return (
    <Button
      variant="secondary"
      iconOnly
      title={copied ? copiedLabel : copyLabel}
      aria-label={copied ? copiedLabel : copyLabel}
      icon={copied ? <Check size={14} /> : <Copy size={14} />}
      onClick={() => { navigator.clipboard.writeText(text).then(() => { setCopied(true); setTimeout(() => setCopied(false), 1500); }); }}
    />
  );
}

export function PartnerCodesPage() {
  const { locale } = usePartnerLocale();
  const t = content[locale];
  const [codes, setCodes] = useState<AffiliateCode[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [customCode, setCustomCode] = useState('');
  const [creating, setCreating] = useState(false);

  useEffect(() => {
    let cancelled = false;
    listMyCodes()
      .then(result => { if (!cancelled) setCodes(result); })
      .catch(err => { if (!cancelled) setError(err instanceof Error ? err.message : t.fetchError); });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [reloadKey]);

  async function handleCreate(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setCreating(true);
    try {
      await createMyCode(customCode.trim() || null, null);
      setCustomCode('');
      setReloadKey(k => k + 1);
    } catch (err) {
      setError(err instanceof Error ? err.message : t.createError);
    } finally {
      setCreating(false);
    }
  }

  async function handleToggle(code: AffiliateCode) {
    setBusyId(code.id);
    try {
      await setMyCodeActive(code.id, !code.isActive);
      setReloadKey(k => k + 1);
    } catch (err) {
      setError(err instanceof Error ? err.message : t.toggleError);
    } finally {
      setBusyId(null);
    }
  }

  const activeCount = codes?.filter(c => c.isActive).length ?? 0;

  return (
    <PartnerLayout>
      <PartnerPageHeader title={t.title} description={t.description} />
      {error ? <p className="formMessage formMessage--error">{error}</p> : null}

      <div className="card" style={{ marginBottom: 16 }}>
        <form className="formGrid" onSubmit={handleCreate}>
          <Field label={t.customCodeLabel(activeCount)} info={t.customCodeInfo}>
            <TextInput value={customCode} onChange={e => setCustomCode(e.target.value.toUpperCase())} placeholder={t.customCodePlaceholder} minLength={7} maxLength={20} />
          </Field>
          <div className="formActions">
            <Button type="submit" icon={<Plus size={16} />} disabled={creating}>{creating ? t.creating : t.create}</Button>
          </div>
        </form>
      </div>

      {!codes ? <LoadingState /> : (
        <div className="card adminTableCard">
          <table className="adminTable">
            <thead><tr><th>{t.colCode}</th><th>{t.colStatus}</th><th /></tr></thead>
            <tbody>
              {codes.map(c => (
                <tr key={c.id}>
                  <td>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                      <code>{c.code}</code>
                      <CopyButton text={c.code} copyLabel={t.copyCode} copiedLabel={t.copied} />
                    </div>
                  </td>
                  <td>{c.isActive ? <span className="adminTag adminTag--ok">{t.active}</span> : <span className="adminTag">{t.disabled}</span>}</td>
                  <td className="adminTable__actions">
                    <Button variant="secondary" icon={<Power size={14} />} disabled={busyId === c.id} onClick={() => handleToggle(c)}>
                      {c.isActive ? t.disable : t.enable}
                    </Button>
                  </td>
                </tr>
              ))}
              {codes.length === 0 ? <tr><td colSpan={3} className="adminMuted">{t.empty}</td></tr> : null}
            </tbody>
          </table>
        </div>
      )}
    </PartnerLayout>
  );
}
