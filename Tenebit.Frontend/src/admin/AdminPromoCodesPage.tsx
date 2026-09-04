import { Plus, Power, Trash2 } from 'lucide-react';
import { useEffect, useState } from 'react';
import { PLANS } from '../components/PricingCards';
import { Button } from '../components/Button';
import { Field, SelectInput, TextInput } from '../components/FormFields';
import { LoadingState } from '../components/StateViews';
import {
  createPromoCodes,
  deletePromoCode,
  listPromoCodes,
  setPromoCodeActive,
  type AdminPromoCode,
} from './adminApi';
import { AdminPageHeader, AdminShell } from './AdminShell';

const PAID_PLANS = PLANS.filter(plan => plan.key !== 'free');

function isExpired(code: AdminPromoCode): boolean {
  return code.expiresAt !== null && new Date(code.expiresAt).getTime() <= Date.now();
}

function isExhausted(code: AdminPromoCode): boolean {
  return code.maxRedemptions !== null && code.timesRedeemed >= code.maxRedemptions;
}

export function AdminPromoCodesPage() {
  const [codes, setCodes] = useState<AdminPromoCode[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);

  const [planKey, setPlanKey] = useState(PAID_PLANS[0]?.key ?? 'starter');
  const [discountType, setDiscountType] = useState<'Percentage' | 'FixedAmount'>('Percentage');
  const [discountValue, setDiscountValue] = useState('10');
  const [quantity, setQuantity] = useState('1');
  const [code, setCode] = useState('');
  const [maxRedemptions, setMaxRedemptions] = useState('1');
  const [expiresAt, setExpiresAt] = useState('');

  useEffect(() => {
    let cancelled = false;
    setCodes(null);
    listPromoCodes()
      .then(result => { if (!cancelled) setCodes(result); })
      .catch(err => { if (!cancelled) setError(err instanceof Error ? err.message : 'Nie udało się pobrać kodów.'); });
    return () => { cancelled = true; };
  }, [reloadKey]);

  async function handleCreate(event: React.FormEvent) {
    event.preventDefault();
    setError(null);
    setSuccess(null);
    setCreating(true);
    try {
      const created = await createPromoCodes({
        planKey,
        discountType,
        discountValue: Number(discountValue),
        quantity: Number(quantity),
        code: code.trim() || undefined,
        maxRedemptions: maxRedemptions.trim() ? Number(maxRedemptions) : null,
        expiresAt: expiresAt ? new Date(expiresAt).toISOString() : null,
      });
      setSuccess(created.length === 1
        ? `Utworzono kod ${created[0].code}.`
        : `Utworzono ${created.length} kodów: ${created.map(c => c.code).join(', ')}.`);
      setCode('');
      setReloadKey(key => key + 1);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Nie udało się utworzyć kodu.');
    } finally {
      setCreating(false);
    }
  }

  async function handleToggleActive(item: AdminPromoCode) {
    setBusyId(item.id);
    setError(null);
    try {
      await setPromoCodeActive(item.id, !item.isActive);
      setReloadKey(key => key + 1);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Nie udało się zmienić statusu kodu.');
    } finally {
      setBusyId(null);
    }
  }

  async function handleDelete(item: AdminPromoCode) {
    if (!window.confirm(`Usunąć kod ${item.code}? Tej operacji nie można cofnąć.`)) return;
    setBusyId(item.id);
    setError(null);
    try {
      await deletePromoCode(item.id);
      setReloadKey(key => key + 1);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Nie udało się usunąć kodu.');
    } finally {
      setBusyId(null);
    }
  }

  return (
    <AdminShell>
      <AdminPageHeader
        title="Kody promocyjne"
        description="Zniżki dla poszczególnych planów. Jeden kod może mieć limit użyć i datę wygaśnięcia. Podaj ilość większą niż 1, żeby wygenerować od razu wiele unikalnych kodów (opcjonalnie z własnym prefiksem)."
      />

      {error ? <p className="formMessage formMessage--error">{error}</p> : null}
      {success ? <p className="formMessage formMessage--success">{success}</p> : null}

      <div className="card" style={{ marginBottom: 20 }}>
        <form className="formGrid" onSubmit={handleCreate}>
          <Field label="Plan">
            <SelectInput value={planKey} onChange={e => setPlanKey(e.target.value)}>
              {PAID_PLANS.map(plan => (
                <option key={plan.key} value={plan.key}>{plan.name}</option>
              ))}
            </SelectInput>
          </Field>

          <Field label="Rodzaj zniżki">
            <SelectInput value={discountType} onChange={e => setDiscountType(e.target.value as 'Percentage' | 'FixedAmount')}>
              <option value="Percentage">Procentowa (%)</option>
              <option value="FixedAmount">Kwotowa (€)</option>
            </SelectInput>
          </Field>

          <Field label={discountType === 'Percentage' ? 'Wysokość zniżki (%)' : 'Wysokość zniżki (€)'}>
            <TextInput type="number" min="0.01" step="0.01" value={discountValue} onChange={e => setDiscountValue(e.target.value)} required />
          </Field>

          <Field label="Liczba kodów do wygenerowania" info="Ustaw >1, żeby od razu wygenerować wiele różnych kodów dla tego planu.">
            <TextInput type="number" min="1" max="200" value={quantity} onChange={e => setQuantity(e.target.value)} required />
          </Field>

          <Field label={Number(quantity) > 1 ? 'Prefiks kodu (opcjonalnie)' : 'Kod (puste = wygeneruj losowy)'}>
            <TextInput value={code} onChange={e => setCode(e.target.value.toUpperCase())} placeholder={Number(quantity) > 1 ? 'np. LATO' : 'np. LATO2026'} />
          </Field>

          <Field label="Limit użyć (puste = bez limitu)">
            <TextInput type="number" min="1" value={maxRedemptions} onChange={e => setMaxRedemptions(e.target.value)} />
          </Field>

          <Field label="Data wygaśnięcia (opcjonalnie)">
            <TextInput type="date" value={expiresAt} onChange={e => setExpiresAt(e.target.value)} />
          </Field>

          <div className="formActions">
            <Button type="submit" icon={<Plus size={16} />} disabled={creating}>
              {creating ? 'Tworzenie…' : 'Utwórz kod'}
            </Button>
          </div>
        </form>
      </div>

      {!codes ? <LoadingState /> : (
        <div className="card adminTableCard">
          <table className="adminTable">
            <thead>
              <tr>
                <th>Kod</th>
                <th>Plan</th>
                <th>Zniżka</th>
                <th>Użycia</th>
                <th>Wygasa</th>
                <th>Status</th>
                <th aria-label="Akcje" />
              </tr>
            </thead>
            <tbody>
              {codes.map(item => {
                const planName = PLANS.find(plan => plan.key === item.planKey)?.name ?? item.planKey;
                const expired = isExpired(item);
                const exhausted = isExhausted(item);
                return (
                  <tr key={item.id} className={!item.isActive ? 'adminTable__row--muted' : undefined}>
                    <td><code>{item.code}</code></td>
                    <td>{planName}</td>
                    <td>{item.discountType === 'Percentage' ? `${item.discountValue}%` : `${item.discountValue} €`}</td>
                    <td>{item.timesRedeemed}{item.maxRedemptions !== null ? ` / ${item.maxRedemptions}` : ''}</td>
                    <td>{item.expiresAt ? new Date(item.expiresAt).toLocaleDateString('pl-PL') : '—'}</td>
                    <td>
                      {!item.isActive ? <span className="adminTag">Wyłączony</span>
                        : expired ? <span className="adminTag adminTag--danger">Wygasł</span>
                        : exhausted ? <span className="adminTag adminTag--danger">Wyczerpany</span>
                        : <span className="adminTag adminTag--ok">Aktywny</span>}
                    </td>
                    <td className="adminTable__actions">
                      <Button
                        variant="secondary"
                        icon={<Power size={14} />}
                        disabled={busyId === item.id}
                        onClick={() => handleToggleActive(item)}
                      >{item.isActive ? 'Wyłącz' : 'Włącz'}</Button>
                      <Button
                        variant="danger"
                        icon={<Trash2 size={14} />}
                        disabled={busyId === item.id}
                        onClick={() => handleDelete(item)}
                      >Usuń</Button>
                    </td>
                  </tr>
                );
              })}
              {codes.length === 0 ? <tr><td colSpan={7} className="adminMuted">Brak kodów promocyjnych.</td></tr> : null}
            </tbody>
          </table>
        </div>
      )}
    </AdminShell>
  );
}
