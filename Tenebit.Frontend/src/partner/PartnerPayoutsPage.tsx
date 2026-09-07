import { useEffect, useState } from 'react';
import { LoadingState } from '../components/StateViews';
import { PartnerLayout, PartnerPageHeader } from './PartnerLayout';
import { getMyPayouts, type AffiliatePayoutSummary } from './partnerApi';

function money(value: number, currency: string): string {
  return `${value.toFixed(2)} ${currency === 'EUR' ? '€' : currency}`;
}

export function PartnerPayoutsPage() {
  const [payouts, setPayouts] = useState<AffiliatePayoutSummary[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    getMyPayouts()
      .then(result => { if (!cancelled) setPayouts(result); })
      .catch(err => { if (!cancelled) setError(err instanceof Error ? err.message : 'Nie udało się pobrać wypłat.'); });
    return () => { cancelled = true; };
  }, []);

  return (
    <PartnerLayout>
      <PartnerPageHeader title="Historia wypłat" description="Przelewy zrealizowane na Twoje konto Revolut." />
      {error ? <p className="formMessage formMessage--error">{error}</p> : null}
      {!payouts ? <LoadingState /> : (
        <div className="card adminTableCard">
          <table className="adminTable">
            <thead><tr><th>Data</th><th>Kwota</th><th>Referencja</th></tr></thead>
            <tbody>
              {payouts.map((p, i) => (
                <tr key={i}>
                  <td>{new Date(p.markedPaidAt).toLocaleDateString('pl-PL')}</td>
                  <td>{money(p.amount, p.currency)}</td>
                  <td>{p.paymentReference ?? '—'}</td>
                </tr>
              ))}
              {payouts.length === 0 ? <tr><td colSpan={3} className="adminMuted">Brak wypłat jeszcze zrealizowanych.</td></tr> : null}
            </tbody>
          </table>
        </div>
      )}
    </PartnerLayout>
  );
}
