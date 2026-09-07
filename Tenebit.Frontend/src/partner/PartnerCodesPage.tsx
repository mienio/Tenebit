import { Check, Copy, Plus, Power } from 'lucide-react';
import { FormEvent, useEffect, useState } from 'react';
import { Button } from '../components/Button';
import { Field, TextInput } from '../components/FormFields';
import { LoadingState } from '../components/StateViews';
import { PartnerLayout, PartnerPageHeader } from './PartnerLayout';
import { createMyCode, listMyCodes, setMyCodeActive, type AffiliateCode } from './partnerApi';

function CopyButton({ text }: { text: string }) {
  const [copied, setCopied] = useState(false);
  return (
    <Button
      variant="secondary"
      icon={copied ? <Check size={14} /> : <Copy size={14} />}
      onClick={() => { navigator.clipboard.writeText(text).then(() => { setCopied(true); setTimeout(() => setCopied(false), 1500); }); }}
    >{copied ? 'Skopiowano' : 'Kopiuj link'}</Button>
  );
}

export function PartnerCodesPage() {
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
      .catch(err => { if (!cancelled) setError(err instanceof Error ? err.message : 'Nie udało się pobrać kodów.'); });
    return () => { cancelled = true; };
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
      setError(err instanceof Error ? err.message : 'Nie udało się utworzyć kodu.');
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
      setError(err instanceof Error ? err.message : 'Nie udało się zmienić statusu kodu.');
    } finally {
      setBusyId(null);
    }
  }

  const activeCount = codes?.filter(c => c.isActive).length ?? 0;

  return (
    <PartnerLayout>
      <PartnerPageHeader title="Twoje kody" description="Każdy kod ma osobny, gotowy do skopiowania link. Kody nie wygasają." />
      {error ? <p className="formMessage formMessage--error">{error}</p> : null}

      <div className="card" style={{ marginBottom: 16 }}>
        <form className="formGrid" onSubmit={handleCreate}>
          <Field label={`Własny kod (opcjonalnie) - ${activeCount} aktywnych`} info="Zostaw puste, aby wygenerować kod automatycznie.">
            <TextInput value={customCode} onChange={e => setCustomCode(e.target.value.toUpperCase())} placeholder="np. DAMIAN20" />
          </Field>
          <div className="formActions">
            <Button type="submit" icon={<Plus size={16} />} disabled={creating}>{creating ? 'Tworzenie…' : 'Utwórz kod'}</Button>
          </div>
        </form>
      </div>

      {!codes ? <LoadingState /> : (
        <div className="card adminTableCard">
          <table className="adminTable">
            <thead><tr><th>Kod</th><th>Link</th><th>Kliknięcia</th><th>Status</th><th /></tr></thead>
            <tbody>
              {codes.map(c => (
                <tr key={c.id}>
                  <td><code>{c.code}</code></td>
                  <td><CopyButton text={c.trackingUrl} /></td>
                  <td>{c.clickCount}</td>
                  <td>{c.isActive ? <span className="adminTag adminTag--ok">Aktywny</span> : <span className="adminTag">Wyłączony</span>}</td>
                  <td>
                    <Button variant="secondary" icon={<Power size={14} />} disabled={busyId === c.id} onClick={() => handleToggle(c)}>
                      {c.isActive ? 'Wyłącz' : 'Włącz'}
                    </Button>
                  </td>
                </tr>
              ))}
              {codes.length === 0 ? <tr><td colSpan={5} className="adminMuted">Brak kodów - utwórz pierwszy powyżej.</td></tr> : null}
            </tbody>
          </table>
        </div>
      )}
    </PartnerLayout>
  );
}
