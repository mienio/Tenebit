import { useEffect, useState } from 'react';
import { LoadingState } from '../components/StateViews';
import { PartnerLayout, PartnerPageHeader } from './PartnerLayout';
import { getMyConversions, type AffiliateConversionSummary } from './partnerApi';

function money(value: number, currency: string): string {
  return `${value.toFixed(2)} ${currency === 'EUR' ? '€' : currency}`;
}

export function PartnerConversionsPage() {
  const [items, setItems] = useState<AffiliateConversionSummary[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    getMyConversions(1, 100)
      .then(result => { if (!cancelled) setItems(result.items); })
      .catch(err => { if (!cancelled) setError(err instanceof Error ? err.message : 'Nie udało się pobrać sprzedaży.'); });
    return () => { cancelled = true; };
  }, []);

  return (
    <PartnerLayout>
      <PartnerPageHeader
        title="Sprzedaże"
        description="Zanonimizowana lista - widzisz datę, kwotę prowizji i użyty kod, nigdy dane kupującego."
      />
      {error ? <p className="formMessage formMessage--error">{error}</p> : null}
      {!items ? <LoadingState /> : (
        <div className="card adminTableCard">
          <table className="adminTable">
            <thead><tr><th>Data</th><th>Kod</th><th>Typ</th><th>Prowizja</th><th>Status</th></tr></thead>
            <tbody>
              {items.map((c, i) => (
                <tr key={i}>
                  <td>{new Date(c.occurredAt).toLocaleDateString('pl-PL')}</td>
                  <td><code>{c.code}</code></td>
                  <td>{c.eventType === 'InitialSale' ? 'Pierwsza sprzedaż' : 'Odnowienie'}</td>
                  <td>{money(c.commissionAmount, c.currency)}</td>
                  <td>
                    {c.requiresReview ? <span className="adminTag">W weryfikacji</span>
                      : !c.isWithinCommissionWindow ? <span className="adminTag">Poza oknem prowizyjnym</span>
                      : <span className="adminTag adminTag--ok">Naliczono</span>}
                  </td>
                </tr>
              ))}
              {items.length === 0 ? <tr><td colSpan={5} className="adminMuted">Brak sprzedaży jeszcze zarejestrowanej na Twoich kodach.</td></tr> : null}
            </tbody>
          </table>
        </div>
      )}
    </PartnerLayout>
  );
}
