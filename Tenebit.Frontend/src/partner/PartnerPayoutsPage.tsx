import { useEffect, useState } from 'react';
import { LoadingState } from '../components/StateViews';
import { PartnerLayout, PartnerPageHeader } from './PartnerLayout';
import { getMyPayouts, type AffiliatePayoutSummary } from './partnerApi';
import { usePartnerLocale, type PartnerLocale } from './i18n';

function money(value: number, currency: string): string {
  return `${value.toFixed(2)} ${currency === 'EUR' ? '€' : currency}`;
}

const content: Record<PartnerLocale, {
  title: string; description: string; fetchError: string;
  colDate: string; colAmount: string; colReference: string; empty: string; dateLocale: string;
}> = {
  en: {
    title: 'Payout history', description: 'Transfers made to your Revolut account.',
    fetchError: 'Could not fetch payouts.',
    colDate: 'Date', colAmount: 'Amount', colReference: 'Reference', empty: 'No payouts made yet.', dateLocale: 'en-GB',
  },
  pl: {
    title: 'Historia wypłat', description: 'Przelewy zrealizowane na Twoje konto Revolut.',
    fetchError: 'Nie udało się pobrać wypłat.',
    colDate: 'Data', colAmount: 'Kwota', colReference: 'Referencja', empty: 'Brak wypłat jeszcze zrealizowanych.', dateLocale: 'pl-PL',
  },
};

export function PartnerPayoutsPage() {
  const { locale } = usePartnerLocale();
  const t = content[locale];
  const [payouts, setPayouts] = useState<AffiliatePayoutSummary[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    getMyPayouts()
      .then(result => { if (!cancelled) setPayouts(result); })
      .catch(err => { if (!cancelled) setError(err instanceof Error ? err.message : t.fetchError); });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <PartnerLayout>
      <PartnerPageHeader title={t.title} description={t.description} />
      {error ? <p className="formMessage formMessage--error">{error}</p> : null}
      {!payouts ? <LoadingState /> : (
        <div className="card adminTableCard">
          <table className="adminTable">
            <thead><tr><th>{t.colDate}</th><th>{t.colAmount}</th><th>{t.colReference}</th></tr></thead>
            <tbody>
              {payouts.map((p, i) => (
                <tr key={i}>
                  <td>{new Date(p.markedPaidAt).toLocaleDateString(t.dateLocale)}</td>
                  <td>{money(p.amount, p.currency)}</td>
                  <td>{p.paymentReference ?? '—'}</td>
                </tr>
              ))}
              {payouts.length === 0 ? <tr><td colSpan={3} className="adminMuted">{t.empty}</td></tr> : null}
            </tbody>
          </table>
        </div>
      )}
    </PartnerLayout>
  );
}
