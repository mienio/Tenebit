import { FormEvent, useEffect, useState } from 'react';
import { Button } from '../components/Button';
import { Field, SelectInput, TextInput } from '../components/FormFields';
import { LoadingState } from '../components/StateViews';
import { AdminPageHeader, AdminShell } from './AdminShell';
import {
  getAffiliateSettings,
  listAffiliateCountryRules,
  removeAffiliateCountryRule,
  updateAffiliateSettings,
  upsertAffiliateCountryRule,
  type AffiliateCountryDiscountRule,
  type AffiliateProgramSettings,
} from './adminApi';

export function AdminAffiliateSettingsPage() {
  const [settings, setSettings] = useState<AffiliateProgramSettings | null>(null);
  const [rules, setRules] = useState<AffiliateCountryDiscountRule[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);

  const [countryCode, setCountryCode] = useState('');
  const [discountPercent, setDiscountPercent] = useState('10');
  const [durationMonths, setDurationMonths] = useState('');

  useEffect(() => {
    let cancelled = false;
    Promise.all([getAffiliateSettings(), listAffiliateCountryRules()])
      .then(([s, r]) => { if (!cancelled) { setSettings(s); setRules(r); } })
      .catch(err => { if (!cancelled) setError(err instanceof Error ? err.message : 'Nie udało się pobrać ustawień.'); });
    return () => { cancelled = true; };
  }, [reloadKey]);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!settings) return;
    setError(null);
    setSuccess(null);
    setSaving(true);
    try {
      const updated = await updateAffiliateSettings(settings);
      setSettings(updated);
      setSuccess('Zapisano ustawienia programu partnerskiego.');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Nie udało się zapisać ustawień.');
    } finally {
      setSaving(false);
    }
  }

  async function handleAddRule(event: FormEvent) {
    event.preventDefault();
    setError(null);
    try {
      await upsertAffiliateCountryRule(countryCode.toUpperCase(), Number(discountPercent), durationMonths.trim() ? Number(durationMonths) : null);
      setCountryCode('');
      setReloadKey(k => k + 1);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Nie udało się zapisać reguły.');
    }
  }

  async function handleRemoveRule(id: string) {
    try {
      await removeAffiliateCountryRule(id);
      setReloadKey(k => k + 1);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Nie udało się usunąć reguły.');
    }
  }

  if (!settings || !rules) return <AdminShell><LoadingState /></AdminShell>;

  return (
    <AdminShell>
      <AdminPageHeader
        title="Ustawienia programu partnerskiego"
        description="Domyślne stawki i harmonogram wypłat. Zmiana tutaj nie przelicza już naliczonych prowizji - obowiązuje od kolejnych konwersji."
      />

      {error ? <p className="formMessage formMessage--error">{error}</p> : null}
      {success ? <p className="formMessage formMessage--success">{success}</p> : null}

      <div className="card" style={{ marginBottom: 16 }}>
        <form className="formGrid" onSubmit={handleSubmit}>
          <Field label="Domyślna prowizja (%)">
            <TextInput type="number" min="0" max="100" step="0.1" value={settings.defaultCommissionPercent}
              onChange={e => setSettings({ ...settings, defaultCommissionPercent: Number(e.target.value) })} required />
          </Field>
          <Field label="Podstawa naliczania prowizji" info="Netto = po odjęciu prowizji Paddle (rekomendowane, tak realnie wpływa do Tenebit).">
            <SelectInput value={settings.commissionBase} onChange={e => setSettings({ ...settings, commissionBase: e.target.value as 'Gross' | 'Net' })}>
              <option value="Net">Netto (po prowizji Paddle)</option>
              <option value="Gross">Brutto (cena klienta)</option>
            </SelectInput>
          </Field>
          <Field label="Okno prowizyjne (miesiące, puste = dożywotnio)">
            <TextInput type="number" min="1" value={settings.defaultCommissionWindowMonths ?? ''}
              onChange={e => setSettings({ ...settings, defaultCommissionWindowMonths: e.target.value.trim() ? Number(e.target.value) : null })} />
          </Field>
          <Field label="Domyślny limit aktywnych kodów">
            <TextInput type="number" min="1" value={settings.defaultMaxCodesPerAffiliate}
              onChange={e => setSettings({ ...settings, defaultMaxCodesPerAffiliate: Number(e.target.value) })} required />
          </Field>
          <Field label="Dzień rozliczeniowy (docelowy dzień wypłaty)">
            <TextInput type="number" min="1" max="28" value={settings.payoutDayOfMonth}
              onChange={e => setSettings({ ...settings, payoutDayOfMonth: Number(e.target.value) })} required />
          </Field>
          <Field label="Maks. dni poślizgu po dniu rozliczeniowym" info="To zobowiązanie widnieje też w regulaminie partnerskim.">
            <TextInput type="number" min="0" max="28" value={settings.payoutGraceDays}
              onChange={e => setSettings({ ...settings, payoutGraceDays: Number(e.target.value) })} required />
          </Field>
          <Field label="Minimalna kwota wypłaty (€, puste = brak progu)">
            <TextInput type="number" min="0" step="0.01" value={settings.minimumPayoutAmount ?? ''}
              onChange={e => setSettings({ ...settings, minimumPayoutAmount: e.target.value.trim() ? Number(e.target.value) : null })} />
          </Field>
          <label style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
            <input type="checkbox" checked={settings.codeGrantsCustomerDiscountByDefault}
              onChange={e => setSettings({
                ...settings, codeGrantsCustomerDiscountByDefault: e.target.checked,
                defaultCustomerDiscountPercent: e.target.checked ? (settings.defaultCustomerDiscountPercent ?? 20) : null,
                defaultCustomerDiscountDurationMonths: e.target.checked ? (settings.defaultCustomerDiscountDurationMonths ?? 3) : null,
              })} />
            <span>Kod afiliacyjny domyślnie daje zniżkę klientowi</span>
          </label>
          {settings.codeGrantsCustomerDiscountByDefault ? (
            <>
              <Field label="Zniżka dla klienta (%)" info="Stosowana też, gdy klient ręcznie wpisze kod afiliacyjny w polu 'kod promocyjny' przy checkout - klient nigdy nie widzi, że to kod afiliacyjny.">
                <TextInput type="number" min="1" max="100" step="0.1" value={settings.defaultCustomerDiscountPercent ?? ''}
                  onChange={e => setSettings({ ...settings, defaultCustomerDiscountPercent: e.target.value.trim() ? Number(e.target.value) : null })} required />
              </Field>
              <Field label="Czas trwania zniżki (miesiące, puste = dożywotnio)">
                <TextInput type="number" min="1" value={settings.defaultCustomerDiscountDurationMonths ?? ''}
                  onChange={e => setSettings({ ...settings, defaultCustomerDiscountDurationMonths: e.target.value.trim() ? Number(e.target.value) : null })} />
              </Field>
            </>
          ) : null}
          <label style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
            <input type="checkbox" checked={settings.publicLeaderboardEnabled}
              onChange={e => setSettings({ ...settings, publicLeaderboardEnabled: e.target.checked })} />
            <span>Włącz ranking widoczny dla afiliantów</span>
          </label>
          <div className="formActions">
            <Button type="submit" disabled={saving}>{saving ? 'Zapisywanie…' : 'Zapisz ustawienia'}</Button>
          </div>
        </form>
      </div>

      <div className="card adminTableCard">
        <h3 style={{ marginTop: 0, paddingLeft: 16 }}>Rabaty per-kraj</h3>
        <table className="adminTable">
          <thead><tr><th>Kraj</th><th>Zniżka</th><th>Czas trwania</th><th /></tr></thead>
          <tbody>
            {rules.map(r => (
              <tr key={r.id}>
                <td>{r.countryCode}</td>
                <td>{r.discountPercent}%</td>
                <td>{r.durationMonths ? `${r.durationMonths} mies.` : 'Bezterminowo'}</td>
                <td><Button variant="danger" onClick={() => handleRemoveRule(r.id)}>Usuń</Button></td>
              </tr>
            ))}
          </tbody>
        </table>
        <form className="formGrid" onSubmit={handleAddRule} style={{ padding: 16 }}>
          <Field label="Kod kraju (ISO, np. DE)">
            <TextInput value={countryCode} onChange={e => setCountryCode(e.target.value.toUpperCase())} maxLength={2} required />
          </Field>
          <Field label="Zniżka (%)">
            <TextInput type="number" min="0.01" max="100" step="0.01" value={discountPercent} onChange={e => setDiscountPercent(e.target.value)} required />
          </Field>
          <Field label="Czas trwania (miesiące, puste = bezterminowo)">
            <TextInput type="number" min="1" value={durationMonths} onChange={e => setDurationMonths(e.target.value)} />
          </Field>
          <div className="formActions">
            <Button type="submit">Dodaj regułę</Button>
          </div>
        </form>
      </div>
    </AdminShell>
  );
}
