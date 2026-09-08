import { FormEvent, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { Button } from '../components/Button';
import { Field, TextArea, TextInput } from '../components/FormFields';
import { LoadingState } from '../components/StateViews';
import { AdminActionDialog, type AdminActionRequest } from './AdminActionDialog';
import { AdminPageHeader, AdminShell } from './AdminShell';
import {
  approveAffiliate,
  blockAffiliate,
  getAffiliate,
  markAffiliatePayoutPaid,
  overrideAffiliateCommission,
  reactivateAffiliate,
  type AffiliateAdminDetail,
} from './adminApi';

function money(value: number, currency = 'EUR'): string {
  return `${value.toFixed(2)} ${currency === 'EUR' ? '€' : currency}`;
}

function date(value: string): string {
  return new Date(value).toLocaleDateString('pl-PL');
}

function MarkPaidDialog({
  affiliateId, detail, onClose, onDone,
}: { affiliateId: string; detail: AffiliateAdminDetail; onClose: () => void; onDone: () => void }) {
  const awaiting = detail.payoutPeriods.filter(p => p.status === 'AwaitingPayout');
  const [selected, setSelected] = useState<string[]>(awaiting.map(p => p.id));
  const [amount, setAmount] = useState(String(awaiting.reduce((sum, p) => sum + p.totalCommission, 0).toFixed(2)));
  const [reference, setReference] = useState('');
  const [note, setNote] = useState('');
  const [confirmed, setConfirmed] = useState(false);
  const [totpCode, setTotpCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  function toggle(id: string) {
    setSelected(current => current.includes(id) ? current.filter(x => x !== id) : [...current, id]);
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!confirmed) { setError('Potwierdź, że przelew został wykonany.'); return; }
    setError(null);
    setSubmitting(true);
    try {
      await markAffiliatePayoutPaid(affiliateId, {
        periodIds: selected,
        amount: Number(amount),
        currency: 'EUR',
        paymentReference: reference.trim() || null,
        note: note.trim() || null,
        totpCode,
      });
      onDone();
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Nie udało się oznaczyć wypłaty jako zapłaconej.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="adminDialog" role="dialog" aria-modal="true" aria-label="Oznacz jako zapłacone">
      <div className="adminDialog__panel">
        <div className="adminDialog__head">
          <h2>Oznacz wypłatę jako zapłaconą</h2>
        </div>
        <p className="adminDialog__text">
          Zaznacz okresy, które obejmuje ten przelew ({detail.payoutMethod === 'PayPal' ? 'PayPal' : 'Revolut'}
          {detail.payoutAccountTag ? <>: <strong>{detail.payoutAccountTag}</strong></> : null}).
          Ta operacja jest nieodwracalna.
        </p>
        <form className="formGrid" onSubmit={handleSubmit}>
          <div>
            {awaiting.map(p => (
              <label key={p.id} style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 6 }}>
                <input type="checkbox" checked={selected.includes(p.id)} onChange={() => toggle(p.id)} />
                <span>{date(p.periodStart)} – {date(p.periodEnd)}: {money(p.totalCommission)}</span>
              </label>
            ))}
            {awaiting.length === 0 ? <p className="adminMuted">Brak okresów oczekujących na wypłatę.</p> : null}
          </div>
          <Field label="Kwota przelewu (€)" info="Domyślnie suma zaznaczonych okresów - można skorygować przy zaokrągleniach.">
            <TextInput type="number" step="0.01" min="0.01" value={amount} onChange={e => setAmount(e.target.value)} required />
          </Field>
          <Field label="Referencja przelewu (opcjonalnie)">
            <TextInput value={reference} onChange={e => setReference(e.target.value)} placeholder="np. numer transakcji PayPal/Revolut" />
          </Field>
          <Field label="Notatka (opcjonalnie)">
            <TextArea value={note} onChange={e => setNote(e.target.value)} rows={2} />
          </Field>
          <label style={{ display: 'flex', gap: 8, alignItems: 'flex-start' }}>
            <input type="checkbox" checked={confirmed} onChange={e => setConfirmed(e.target.checked)} style={{ marginTop: 3 }} />
            <span>Potwierdzam, że przelew został wykonany na wskazane konto {detail.payoutMethod === 'PayPal' ? 'PayPal' : 'Revolut'}.</span>
          </label>
          <Field label="Kod 2FA">
            <TextInput inputMode="numeric" maxLength={6} minLength={6} required value={totpCode} onChange={e => setTotpCode(e.target.value)} autoComplete="one-time-code" />
          </Field>
          {error ? <p className="formMessage formMessage--error">{error}</p> : null}
          <div className="adminDialog__actions">
            <Button type="button" variant="secondary" onClick={onClose} disabled={submitting}>Anuluj</Button>
            <Button variant="danger" disabled={submitting || selected.length === 0}>{submitting ? 'Zapisywanie…' : 'Oznacz jako zapłacone'}</Button>
          </div>
        </form>
      </div>
    </div>
  );
}

export function AdminAffiliateDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [detail, setDetail] = useState<AffiliateAdminDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);
  const [dialogRequest, setDialogRequest] = useState<AdminActionRequest | null>(null);
  const [showMarkPaid, setShowMarkPaid] = useState(false);
  const [commissionPercent, setCommissionPercent] = useState('');
  const [maxCodes, setMaxCodes] = useState('');
  const [commissionTotp, setCommissionTotp] = useState('');
  const [commissionError, setCommissionError] = useState<string | null>(null);
  const [commissionSaving, setCommissionSaving] = useState(false);

  useEffect(() => {
    if (!id) return;
    let cancelled = false;
    setDetail(null);
    getAffiliate(id)
      .then(result => {
        if (cancelled) return;
        setDetail(result);
        setCommissionPercent(result.commissionPercentOverride?.toString() ?? '');
        setMaxCodes(result.maxActiveCodesOverride?.toString() ?? '');
      })
      .catch(err => { if (!cancelled) setError(err instanceof Error ? err.message : 'Nie udało się pobrać danych afilianta.'); });
    return () => { cancelled = true; };
  }, [id, reloadKey]);

  async function handleCommissionSubmit(event: FormEvent) {
    event.preventDefault();
    if (!id) return;
    setCommissionError(null);
    setCommissionSaving(true);
    try {
      await overrideAffiliateCommission(
        id,
        commissionPercent.trim() ? Number(commissionPercent) : null,
        maxCodes.trim() ? Number(maxCodes) : null,
        commissionTotp,
      );
      setCommissionTotp('');
      setReloadKey(k => k + 1);
    } catch (err) {
      setCommissionError(err instanceof Error ? err.message : 'Nie udało się zapisać nadpisania.');
    } finally {
      setCommissionSaving(false);
    }
  }

  if (error) return <AdminShell><p className="formMessage formMessage--error">{error}</p></AdminShell>;
  if (!detail || !id) return <AdminShell><LoadingState /></AdminShell>;

  return (
    <AdminShell>
      <AdminPageHeader
        title={`${detail.firstName} ${detail.lastName}`}
        description={detail.email}
        actions={
          <>
            {detail.status === 'PendingApproval' && (
              <Button onClick={() => setDialogRequest({
                title: 'Zatwierdź afilianta',
                description: 'Konto stanie się aktywne i będzie mogło generować kody oraz zbierać prowizję.',
                confirmLabel: 'Zatwierdź',
                requiresReason: false,
                run: async (_reason, totp) => { await approveAffiliate(id, totp); setReloadKey(k => k + 1); },
              })}>Zatwierdź</Button>
            )}
            {detail.status !== 'Blocked' && (
              <Button variant="danger" onClick={() => setDialogRequest({
                title: 'Zablokuj afilianta',
                description: 'Konto straci dostęp natychmiast (sesje zostaną unieważnione). Historia kodów i konwersji zostaje zachowana.',
                confirmLabel: 'Zablokuj',
                requiresReason: true,
                run: async (reason, totp) => { await blockAffiliate(id, reason, totp); setReloadKey(k => k + 1); },
              })}>Zablokuj</Button>
            )}
            {detail.status === 'Blocked' && (
              <Button onClick={() => setDialogRequest({
                title: 'Odblokuj afilianta',
                description: 'Konto odzyska dostęp do panelu partnera.',
                confirmLabel: 'Odblokuj',
                requiresReason: false,
                run: async (_reason, totp) => { await reactivateAffiliate(id, totp); setReloadKey(k => k + 1); },
              })}>Odblokuj</Button>
            )}
          </>
        }
      />

      <div className="card" style={{ marginBottom: 16 }}>
        <h3 style={{ marginTop: 0 }}>Dane kontaktowe i wypłatowe</h3>
        <div className="adminMuted">
          Kraj: {detail.countryCode ?? '—'} · Telefon: {detail.phoneNumber ?? '—'} · Firma: {detail.companyName ?? '—'} · NIP: {detail.taxId ?? '—'}
        </div>
        <div style={{ marginTop: 8 }}>
          Metoda wypłaty: <strong>{detail.payoutMethod === 'PayPal' ? 'PayPal' : 'Revolut'}</strong> ·{' '}
          Konto: {detail.payoutAccountTag ? <strong>{detail.payoutAccountTag}</strong> : <span className="adminTag adminTag--danger">nie uzupełniono</span>}
        </div>
        {detail.blockedReason ? <p className="formMessage formMessage--error" style={{ marginTop: 12 }}>Powód blokady: {detail.blockedReason}</p> : null}
      </div>

      <div className="card" style={{ marginBottom: 16 }}>
        <h3 style={{ marginTop: 0 }}>Prowizja i limity (nadpisanie indywidualne)</h3>
        <p className="adminMuted">Puste pole = użyj domyślnych ustawień globalnych programu.</p>
        <form className="formGrid" onSubmit={handleCommissionSubmit}>
          <Field label={`Prowizja % (aktualnie stosowana: ${detail.resolvedCommissionPercent}%)`}>
            <TextInput type="number" min="0" max="100" step="0.1" value={commissionPercent} onChange={e => setCommissionPercent(e.target.value)} placeholder="domyślna" />
          </Field>
          <Field label={`Limit aktywnych kodów (aktualnie: ${detail.resolvedMaxActiveCodes})`}>
            <TextInput type="number" min="1" value={maxCodes} onChange={e => setMaxCodes(e.target.value)} placeholder="domyślny" />
          </Field>
          <Field label="Kod 2FA">
            <TextInput inputMode="numeric" maxLength={6} minLength={6} required value={commissionTotp} onChange={e => setCommissionTotp(e.target.value)} autoComplete="one-time-code" />
          </Field>
          {commissionError ? <p className="formMessage formMessage--error">{commissionError}</p> : null}
          <div className="formActions">
            <Button type="submit" disabled={commissionSaving}>{commissionSaving ? 'Zapisywanie…' : 'Zapisz nadpisanie'}</Button>
          </div>
        </form>
      </div>

      <div className="card adminTableCard" style={{ marginBottom: 16 }}>
        <h3 style={{ marginTop: 0, paddingLeft: 16 }}>Kody afiliacyjne</h3>
        <table className="adminTable">
          <thead><tr><th>Kod</th><th>Kraj</th><th>Kliknięcia</th><th>Status</th><th>Utworzono</th></tr></thead>
          <tbody>
            {detail.codes.map(c => (
              <tr key={c.id}>
                <td><code>{c.code}</code></td>
                <td>{c.countryCode ?? '—'}</td>
                <td>{c.clickCount}</td>
                <td>{c.isActive ? <span className="adminTag adminTag--ok">Aktywny</span> : <span className="adminTag">Wyłączony</span>}</td>
                <td>{date(c.createdAt)}</td>
              </tr>
            ))}
            {detail.codes.length === 0 ? <tr><td colSpan={5} className="adminMuted">Brak kodów.</td></tr> : null}
          </tbody>
        </table>
      </div>

      <div className="card adminTableCard" style={{ marginBottom: 16 }}>
        <h3 style={{ marginTop: 0, paddingLeft: 16 }}>Konwersje - kto i kiedy wpłacił</h3>
        <table className="adminTable">
          <thead><tr><th>Data</th><th>Klient</th><th>Kod</th><th>Typ</th><th>Kwota brutto</th><th>Prowizja</th><th /></tr></thead>
          <tbody>
            {detail.conversions.map(c => (
              <tr key={c.id}>
                <td>{date(c.occurredAt)}</td>
                <td>{c.organizationId ? <Link to={`/admin/organizations/${c.organizationId}`}>Zobacz organizację</Link> : '—'}</td>
                <td><code>{c.code}</code></td>
                <td>{c.eventType === 'InitialSale' ? 'Pierwsza sprzedaż' : 'Odnowienie'}</td>
                <td>{money(c.grossAmount, c.currency)}</td>
                <td>{money(c.commissionAmount, c.currency)}</td>
                <td>{c.requiresReview ? <span className="adminTag adminTag--danger">Wymaga przeglądu</span> : !c.isWithinCommissionWindow ? <span className="adminTag">Poza oknem prowizyjnym</span> : null}</td>
              </tr>
            ))}
            {detail.conversions.length === 0 ? <tr><td colSpan={7} className="adminMuted">Brak konwersji.</td></tr> : null}
          </tbody>
        </table>
      </div>

      <div className="card" style={{ marginBottom: 16 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <h3 style={{ margin: 0 }}>Okresy rozliczeniowe i wypłaty</h3>
          {detail.payoutPeriods.some(p => p.status === 'AwaitingPayout') && (
            <Button onClick={() => setShowMarkPaid(true)}>Oznacz jako zapłacone</Button>
          )}
        </div>
        <table className="adminTable" style={{ marginTop: 12 }}>
          <thead><tr><th>Okres</th><th>Suma prowizji</th><th>Status</th></tr></thead>
          <tbody>
            {detail.payoutPeriods.map(p => (
              <tr key={p.id}>
                <td>{date(p.periodStart)} – {date(p.periodEnd)}</td>
                <td>{money(p.totalCommission)}</td>
                <td>
                  {p.status === 'Paid' ? <span className="adminTag adminTag--ok">Opłacony</span>
                    : p.status === 'AwaitingPayout' ? <span className="adminTag adminTag--danger">Oczekuje na wypłatę</span>
                    : <span className="adminTag">Otwarty (bieżący miesiąc)</span>}
                </td>
              </tr>
            ))}
            {detail.payoutPeriods.length === 0 ? <tr><td colSpan={3} className="adminMuted">Brak okresów.</td></tr> : null}
          </tbody>
        </table>

        <h4>Historia wypłat</h4>
        <table className="adminTable">
          <thead><tr><th>Data</th><th>Kwota</th><th>Referencja</th></tr></thead>
          <tbody>
            {detail.payouts.map(p => (
              <tr key={p.id}><td>{date(p.markedPaidAt)}</td><td>{money(p.amount, p.currency)}</td><td>{p.paymentReference ?? '—'}</td></tr>
            ))}
            {detail.payouts.length === 0 ? <tr><td colSpan={3} className="adminMuted">Brak wypłat.</td></tr> : null}
          </tbody>
        </table>
      </div>

      <AdminActionDialog request={dialogRequest} onClose={() => setDialogRequest(null)} onDone={() => setReloadKey(k => k + 1)} />
      {showMarkPaid && detail && (
        <MarkPaidDialog affiliateId={id} detail={detail} onClose={() => setShowMarkPaid(false)} onDone={() => setReloadKey(k => k + 1)} />
      )}
    </AdminShell>
  );
}
